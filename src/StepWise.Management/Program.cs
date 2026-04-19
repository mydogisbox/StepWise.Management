using System.Text.Json;
using CommandFramework.Core;
using CommandFramework.Http;
using CommandFramework.Postgres;
using Npgsql;
using StepWise.Management.Domain.Catalogs;
using StepWise.Management.Domain.CatalogSteps;
using StepWise.Management.Domain.Targets;
using StepWise.Management.Domain.TestRuns;
using StepWise.Management.Domain.Workflows;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

builder.Services.AddSingleton<IEventStore>(_ => new PostgresEventStore(connectionString));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var eventStore = app.Services.GetRequiredService<IEventStore>();

// --- Target aggregate ---
var targetProcessor = new EventProcessor(TargetReactions.All);
var targetHandler = new AggregateHandler<TargetState, TargetEvent>(
    TargetAggregate.Definition,
    eventStore,
    "targets",
    targetProcessor);

app.MapAggregate(
    name: "targets",
    handler: targetHandler,
    deserializeCommand: TargetAggregate.DeserializeCommand,
    deserializeEvent: TargetAggregate.DeserializeEvent);

// --- Catalog aggregate ---
var catalogProcessor = new EventProcessor(CatalogReactions.All);
var catalogHandler = new AggregateHandler<CatalogState, CatalogEvent>(
    CatalogAggregate.Definition,
    eventStore,
    "catalogs",
    catalogProcessor);

app.MapAggregate(
    name: "catalogs",
    handler: catalogHandler,
    deserializeCommand: CatalogAggregate.DeserializeCommand,
    deserializeEvent: CatalogAggregate.DeserializeEvent);

// --- CatalogStep aggregate ---
var catalogStepProcessor = new EventProcessor(CatalogStepReactions.All);
var catalogStepHandler = new AggregateHandler<CatalogStepState, CatalogStepEvent>(
    CatalogStepAggregate.Definition,
    eventStore,
    "catalog-steps",
    catalogStepProcessor);

app.MapAggregate(
    name: "catalog-steps",
    handler: catalogStepHandler,
    deserializeCommand: CatalogStepAggregate.DeserializeCommand,
    deserializeEvent: CatalogStepAggregate.DeserializeEvent);

// --- Workflow aggregate ---
var workflowProcessor = new EventProcessor(WorkflowReactions.All);
var workflowHandler = new AggregateHandler<WorkflowState, WorkflowEvent>(
    WorkflowAggregate.Definition,
    eventStore,
    "workflows",
    workflowProcessor);

app.MapAggregate(
    name: "workflows",
    handler: workflowHandler,
    deserializeCommand: WorkflowAggregate.DeserializeCommand,
    deserializeEvent: WorkflowAggregate.DeserializeEvent);

// --- TestRun aggregate ---
var testRunProcessor = new EventProcessor(TestRunReactions.All);
var testRunHandler = new AggregateHandler<TestRunState, TestRunEvent>(
    TestRunAggregate.Definition,
    eventStore,
    "runs",
    testRunProcessor);

app.MapAggregate(
    name: "runs",
    handler: testRunHandler,
    deserializeCommand: TestRunAggregate.DeserializeCommand,
    deserializeEvent: TestRunAggregate.DeserializeEvent);

// ── List endpoints (projection queries) ──────────────────────────────────────

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
};

app.MapGet("/targets", async () =>
{
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, name, base_url, is_archived FROM target_summaries";
    var results = new List<object>();
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        results.Add(new { id = reader.GetString(0), name = reader.GetString(1), baseUrl = reader.GetString(2), isArchived = reader.GetBoolean(3) });
    return Results.Ok(results);
});

app.MapGet("/catalogs", async () =>
{
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, name, is_archived FROM catalog_summaries";
    var results = new List<object>();
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        results.Add(new { id = reader.GetString(0), name = reader.GetString(1), isArchived = reader.GetBoolean(2) });
    return Results.Ok(results);
});

app.MapGet("/catalog-steps", async (string? catalogId) =>
{
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    if (catalogId != null)
    {
        cmd.CommandText = "SELECT id, catalog_id, target_id, step_name, method, path, defaults::text, is_archived FROM catalog_step_summaries WHERE catalog_id = $1";
        cmd.Parameters.Add(new NpgsqlParameter { Value = catalogId });
    }
    else
    {
        cmd.CommandText = "SELECT id, catalog_id, target_id, step_name, method, path, defaults::text, is_archived FROM catalog_step_summaries";
    }
    var results = new List<object>();
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var defaultsStr = reader.IsDBNull(6) ? null : reader.GetString(6);
        results.Add(new
        {
            id = reader.GetString(0),
            catalogId = reader.GetString(1),
            targetId = reader.GetString(2),
            stepName = reader.GetString(3),
            method = reader.GetString(4),
            path = reader.GetString(5),
            defaults = defaultsStr != null ? JsonSerializer.Deserialize<JsonElement>(defaultsStr) : (JsonElement?)null,
            isArchived = reader.GetBoolean(7)
        });
    }
    return Results.Ok(results);
});

app.MapGet("/workflows", async () =>
{
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, name, archived FROM workflow_summaries";
    var results = new List<object>();
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        results.Add(new { id = reader.GetString(0), name = reader.GetString(1), isArchived = reader.GetBoolean(2) });
    return Results.Ok(results);
});

// ── Workflow execution ────────────────────────────────────────────────────────

app.MapPost("/api/workflows/{id}/run", async (string id) =>
{
    // Load WorkflowState
    var workflowEvents = await eventStore.LoadAsync($"workflows/{id}");
    if (workflowEvents.Count == 0)
        return Results.NotFound($"Workflow '{id}' not found.");

    var workflowState = Aggregate.Fold<WorkflowState, WorkflowEvent>(
        workflowEvents.Select(e => WorkflowAggregate.DeserializeEvent(e.EventType, e.Payload)),
        WorkflowAggregate.Apply)!;

    // Resolve step definitions and targets from catalog steps
    var stepDefs = new Dictionary<string, StepWise.Json.StepDefinition>(StringComparer.OrdinalIgnoreCase);
    var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var workflowStep in workflowState.Steps)
    {
        var catalogStepEvents = await eventStore.LoadAsync($"catalog-steps/{workflowStep.CatalogStepId}");
        if (catalogStepEvents.Count == 0) continue;

        var catalogStep = Aggregate.Fold<CatalogStepState, CatalogStepEvent>(
            catalogStepEvents.Select(e => CatalogStepAggregate.DeserializeEvent(e.EventType, e.Payload)),
            CatalogStepAggregate.Apply);
        if (catalogStep == null) continue;

        // Load target for base URL
        var targetEvents = await eventStore.LoadAsync($"targets/{catalogStep.TargetId}");
        if (targetEvents.Count > 0)
        {
            var targetState = Aggregate.Fold<TargetState, TargetEvent>(
                targetEvents.Select(e => TargetAggregate.DeserializeEvent(e.EventType, e.Payload)),
                TargetAggregate.Apply);
            if (targetState != null)
                targets[catalogStep.TargetId] = targetState.BaseUrl;
        }

        // Build step definition keyed by workflow step ID
        var defaults = workflowStep.Defaults.HasValue
            ? JsonSerializer.Deserialize<Dictionary<string, StepWise.Json.FieldValueDefinition>>(
                workflowStep.Defaults.Value.GetRawText(), jsonOptions)
            : null;

        var catalogDefaults = catalogStep.Defaults.HasValue
            ? JsonSerializer.Deserialize<Dictionary<string, StepWise.Json.FieldValueDefinition>>(
                catalogStep.Defaults.Value.GetRawText(), jsonOptions)
            : null;

        // Merge catalog defaults with workflow step defaults
        var mergedDefaults = catalogDefaults != null
            ? new Dictionary<string, StepWise.Json.FieldValueDefinition>(catalogDefaults)
            : new Dictionary<string, StepWise.Json.FieldValueDefinition>();
        if (defaults != null)
            foreach (var (k, v) in defaults)
                mergedDefaults[k] = v;

        stepDefs[workflowStep.Id] = new StepWise.Json.StepDefinition
        {
            Target = catalogStep.TargetId,
            Method = catalogStep.Method,
            Path = catalogStep.Path,
            Defaults = mergedDefaults.Count > 0 ? mergedDefaults : null
        };
    }

    // Build WorkflowDefinition with step invocations
    var workflowDef = new StepWise.Json.WorkflowDefinition(
        Name: workflowState.Name,
        Steps: workflowState.Steps
            .Select(s => new StepWise.Json.StepInvocation { Step = s.Id })
            .ToList(),
        Assertions: workflowState.Assertions.Count > 0 ? workflowState.Assertions : null);

    // Run the workflow
    var startedAt = DateTimeOffset.UtcNow;
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    StepWise.Json.WorkflowResult result;
    try
    {
        result = await StepWise.Json.JsonWorkflowRunner.RunAsync(workflowDef, stepDefs, targets);
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        result = new StepWise.Json.WorkflowResult(
            workflowState.Name,
            false,
            new List<StepWise.Json.StepResult>(),
            new List<string> { ex.Message },
            new Dictionary<string, object?>());
    }
    finally
    {
        stopwatch.Stop();
    }

    var durationMs = stopwatch.ElapsedMilliseconds;
    var runId = Guid.NewGuid().ToString();
    var resultJson = JsonSerializer.Serialize(result, jsonOptions);

    var recordBatch = new CommandBatch(
        AggregateId: runId,
        Commands: new List<CommandEnvelope>
        {
            new CommandEnvelope(
                Type: nameof(RecordRun),
                Payload: JsonSerializer.SerializeToElement(new RecordRun(
                    Id: runId,
                    WorkflowId: id,
                    WorkflowName: workflowState.Name,
                    Passed: result.Passed,
                    ResultJson: resultJson,
                    StartedAt: startedAt,
                    DurationMs: durationMs), jsonOptions))
        });

    await testRunHandler.ExecuteAsync(recordBatch,
        TestRunAggregate.DeserializeCommand,
        TestRunAggregate.DeserializeEvent);

    return Results.Ok(new { runId, result });
});

// ── Ping ──────────────────────────────────────────────────────────────────────

app.MapGet("/api/ping", () => Results.Ok(new { pong = true, service = "StepWise.Management" }));

app.Run();

public partial class Program { }
