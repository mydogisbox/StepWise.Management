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
    deserializeCommand: ReflectionDeserializer.ForCommands<TargetCommands>(),
    deserializeEvent: ReflectionDeserializer.ForEvents<TargetEvent>());

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
    deserializeCommand: ReflectionDeserializer.ForCommands<CatalogCommands>(),
    deserializeEvent: ReflectionDeserializer.ForEvents<CatalogEvent>());

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
    deserializeCommand: ReflectionDeserializer.ForCommands<CatalogStepCommands>(),
    deserializeEvent: ReflectionDeserializer.ForEvents<CatalogStepEvent>());

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
    deserializeCommand: ReflectionDeserializer.ForCommands<WorkflowCommands>(),
    deserializeEvent: ReflectionDeserializer.ForEvents<WorkflowEvent>());

// --- WorkflowRun aggregate ---
var runHandler = app.Services.GetRequiredService<AggregateHandler<WorkflowRunState, WorkflowRunEvent>>();

app.MapAggregate(
    name: "runs",
    handler: runHandler,
    deserializeCommand: ReflectionDeserializer.ForCommands<WorkflowRunCommands>(),
    deserializeEvent: ReflectionDeserializer.ForEvents<WorkflowRunEvent>());

// ── List endpoints (projection queries) ──────────────────────────────────────

var jsonOptions = JsonConfig.Options;

app.MapGet("/targets", async (bool showArchived = false, int page = 1, int pageSize = 10, string? name = null) =>
{
    page     = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 100);
    var filterName = !string.IsNullOrEmpty(name);
    var conditions = new List<string>();
    if (!showArchived) conditions.Add("is_archived = false");
    if (filterName)    conditions.Add("name = $1");
    var where = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    if (filterName) cmd.Parameters.Add(new NpgsqlParameter { Value = name });
    var p = cmd.Parameters.Count + 1;
    cmd.CommandText = $"SELECT id, name, base_url, is_archived, created_at, COUNT(*) OVER() FROM target_summaries{where} ORDER BY created_at DESC LIMIT ${p} OFFSET ${p + 1}";
    cmd.Parameters.Add(new NpgsqlParameter { Value = pageSize });
    cmd.Parameters.Add(new NpgsqlParameter { Value = (page - 1) * pageSize });
    var items = new List<object>();
    long total = 0;
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        items.Add(new { id = reader.GetString(0), name = reader.GetString(1), baseUrl = reader.GetString(2), isArchived = reader.GetBoolean(3), createdAt = reader.GetFieldValue<DateTimeOffset>(4) });
        total = reader.GetInt64(5);
    }
    return Results.Ok(new { items, total, page, pageSize, totalPages = (int)Math.Ceiling((double)total / pageSize) });
});

app.MapGet("/catalogs", async (bool showArchived = false, int page = 1, int pageSize = 10, string? name = null) =>
{
    page     = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 100);
    var filterName = !string.IsNullOrEmpty(name);
    var conditions = new List<string>();
    if (!showArchived) conditions.Add("is_archived = false");
    if (filterName)    conditions.Add("name = $1");
    var where = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    if (filterName) cmd.Parameters.Add(new NpgsqlParameter { Value = name });
    var p = cmd.Parameters.Count + 1;
    cmd.CommandText = $"SELECT id, name, description, is_archived, COUNT(*) OVER() FROM catalog_summaries{where} ORDER BY created_at DESC LIMIT ${p} OFFSET ${p + 1}";
    cmd.Parameters.Add(new NpgsqlParameter { Value = pageSize });
    cmd.Parameters.Add(new NpgsqlParameter { Value = (page - 1) * pageSize });
    var items = new List<object>();
    long total = 0;
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        items.Add(new { id = reader.GetString(0), name = reader.GetString(1), description = reader.GetString(2), isArchived = reader.GetBoolean(3) });
        total = reader.GetInt64(4);
    }
    return Results.Ok(new { items, total, page, pageSize, totalPages = (int)Math.Ceiling((double)total / pageSize) });
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
    cmd.CommandText = $"SELECT id, catalog_id, target_id, step_name, method, path, defaults::text, is_archived, request_shape::text, response_shape::text, is_polling, retry_count, retry_duration_ms FROM catalog_step_summaries{where}";
    var results = new List<object>();
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var defaultsStr = reader.IsDBNull(6) ? null : reader.GetString(6);
        var requestShapeStr = reader.IsDBNull(8) ? null : reader.GetString(8);
        var responseShapeStr = reader.IsDBNull(9) ? null : reader.GetString(9);
        results.Add(new
        {
            id = reader.GetString(0),
            catalogId = reader.GetString(1),
            targetId = reader.GetString(2),
            stepName = reader.GetString(3),
            method = reader.GetString(4),
            path = reader.GetString(5),
            defaults = defaultsStr != null ? JsonSerializer.Deserialize<JsonElement>(defaultsStr) : (JsonElement?)null,
            isArchived = reader.GetBoolean(7),
            requestShape = requestShapeStr != null ? JsonSerializer.Deserialize<JsonElement>(requestShapeStr) : (JsonElement?)null,
            responseShape = responseShapeStr != null ? JsonSerializer.Deserialize<JsonElement>(responseShapeStr) : (JsonElement?)null,
            isPolling = reader.GetBoolean(10),
            retryCount = reader.IsDBNull(11) ? (int?)null : reader.GetInt32(11),
            retryDurationMs = reader.IsDBNull(12) ? (int?)null : reader.GetInt32(12)
        });
    }
    return Results.Ok(results);
});

app.MapGet("/workflows", async (bool showArchived = false, int page = 1, int pageSize = 10, string? name = null) =>
{
    page     = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 100);
    var filterName = !string.IsNullOrEmpty(name);
    var conditions = new List<string>();
    if (!showArchived) conditions.Add("w.archived = false");
    if (filterName)    conditions.Add("w.name = $1");
    var where = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    if (filterName) cmd.Parameters.Add(new NpgsqlParameter { Value = name! });
    var p = cmd.Parameters.Count + 1;
    cmd.CommandText = $@"
        SELECT w.id, w.name, w.description, w.archived, COUNT(*) OVER(),
               COALESCE(r.run_count, 0), COALESCE(r.pass_count, 0)
        FROM workflow_summaries w
        LEFT JOIN (
            SELECT workflow_id,
                   COUNT(*) AS run_count,
                   SUM(CASE WHEN passed THEN 1 ELSE 0 END) AS pass_count
            FROM test_run_summaries
            WHERE passed IS NOT NULL
            GROUP BY workflow_id
        ) r ON r.workflow_id = w.id
        {where}
        ORDER BY w.created_at DESC
        LIMIT ${p} OFFSET ${p + 1}";
    cmd.Parameters.Add(new NpgsqlParameter { Value = pageSize });
    cmd.Parameters.Add(new NpgsqlParameter { Value = (page - 1) * pageSize });
    var items = new List<object>();
    long total = 0;
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        items.Add(new { id = reader.GetString(0), name = reader.GetString(1), description = reader.GetString(2), isArchived = reader.GetBoolean(3), runCount = reader.GetInt64(5), passCount = reader.GetInt64(6) });
        total = reader.GetInt64(4);
    }
    return Results.Ok(new { items, total, page, pageSize, totalPages = (int)Math.Ceiling((double)total / pageSize) });
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
            Type: nameof(WorkflowRunCommands.TriggerRun),
            Payload: JsonSerializer.SerializeToElement(
                new WorkflowRunCommands.TriggerRun(runId, id), jsonOptions))]);

    var result = await runHandler.ExecuteAsync(
        batch,
        ReflectionDeserializer.ForCommands<WorkflowRunCommands>(),
        ReflectionDeserializer.ForEvents<WorkflowRunEvent>());

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

app.MapGet("/runs", async (int page = 1, int pageSize = 10) =>
{
    page     = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 100);
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT r.id, r.workflow_id, w.name, r.passed, r.started_at, r.duration_ms, COUNT(*) OVER()
        FROM test_run_summaries r
        LEFT JOIN workflow_summaries w ON w.id = r.workflow_id
        ORDER BY r.started_at DESC
        LIMIT $1 OFFSET $2";
    cmd.Parameters.Add(new NpgsqlParameter { Value = pageSize });
    cmd.Parameters.Add(new NpgsqlParameter { Value = (page - 1) * pageSize });
    var items = new List<object>();
    long total = 0;
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        items.Add(new
        {
            id = reader.GetString(0),
            workflowId = reader.GetString(1),
            workflowName = reader.IsDBNull(2) ? "" : reader.GetString(2),
            passed = reader.IsDBNull(3) ? (bool?)null : reader.GetBoolean(3),
            startedAt = reader.GetFieldValue<DateTimeOffset>(4),
            durationMs = reader.IsDBNull(5) ? (long?)null : reader.GetInt64(5)
        });
        total = reader.GetInt64(6);
    }
    return Results.Ok(new { items, total, page, pageSize, totalPages = (int)Math.Ceiling((double)total / pageSize) });
});

// ── Ping ──────────────────────────────────────────────────────────────────────

app.MapGet("/api/ping", () => Results.Ok(new { pong = true, service = "StepWise.Management" }));

app.Run();

record TriggerRunRequest(string RunId);

public partial class Program { }
