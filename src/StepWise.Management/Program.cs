using System.Text.Json;
using CommandFramework.Core;
using CommandFramework.Http;
using CommandFramework.Postgres;
using Npgsql;
using StepWise.Management;
using StepWise.Management.Domain.Catalogs;
using StepWise.Management.Domain.CatalogSteps;
using StepWise.Management.Domain.Targets;
using StepWise.Management.Domain.TestRuns;
using StepWise.Management.Domain.Workflows;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

builder.Services.AddSingleton<IEventStore>(_ => new PostgresEventStore(connectionString));

builder.Services.AddSingleton(sp => new AggregateHandler<WorkflowRunState, WorkflowRunEvent>(
    WorkflowRunAggregate.Definition,
    sp.GetRequiredService<IEventStore>(),
    "runs",
    new EventProcessor(WorkflowRunReactions.All)));

builder.Services.AddHostedService<WorkflowExecutionService>();

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

// --- WorkflowRun aggregate ---
var runHandler = app.Services.GetRequiredService<AggregateHandler<WorkflowRunState, WorkflowRunEvent>>();

app.MapAggregate(
    name: "runs",
    handler: runHandler,
    deserializeCommand: WorkflowRunAggregate.DeserializeCommand,
    deserializeEvent: WorkflowRunAggregate.DeserializeEvent);

// ── List endpoints (projection queries) ──────────────────────────────────────

var jsonOptions = JsonConfig.Options;

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

app.MapGet("/catalog-steps", async (string? catalogId, bool showArchived = false) =>
{
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    var conditions = new List<string>();
    if (catalogId != null)
    {
        conditions.Add($"catalog_id = ${cmd.Parameters.Count + 1}");
        cmd.Parameters.Add(new NpgsqlParameter { Value = catalogId });
    }
    if (!showArchived)
        conditions.Add("is_archived = false");
    var where = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";
    cmd.CommandText = $"SELECT id, catalog_id, target_id, step_name, method, path, defaults::text, is_archived FROM catalog_step_summaries{where}";
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

app.MapPost("/api/workflows/{id}/run", async (string id, TriggerRunRequest body) =>
{
    if (string.IsNullOrWhiteSpace(body?.RunId))
        return Results.BadRequest("runId is required.");

    var workflowEvents = await eventStore.LoadAsync($"workflows/{id}");
    if (workflowEvents.Count == 0)
        return Results.NotFound($"Workflow '{id}' not found.");

    var runId = body.RunId;
    var batch = new CommandBatch(
        AggregateId: runId,
        Commands: [new CommandEnvelope(
            Type: nameof(TriggerRun),
            Payload: JsonSerializer.SerializeToElement(
                new TriggerRun(runId, id), jsonOptions))]);

    var result = await runHandler.ExecuteAsync(
        batch,
        WorkflowRunAggregate.DeserializeCommand,
        WorkflowRunAggregate.DeserializeEvent);

    if (result.IsError)
    {
        // Idempotent: if the run stream already exists, a repeat call succeeds
        var existing = await eventStore.LoadAsync($"runs/{runId}");
        if (existing.Count > 0)
            return Results.Ok(new { runId });
        return Results.UnprocessableEntity(result.Error);
    }

    return Results.Ok(new { runId });
});

// ── Ping ──────────────────────────────────────────────────────────────────────

app.MapGet("/api/ping", () => Results.Ok(new { pong = true, service = "StepWise.Management" }));

app.Run();

record TriggerRunRequest(string RunId);

public partial class Program { }
