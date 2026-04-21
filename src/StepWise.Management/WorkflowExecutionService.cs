using System.Diagnostics;
using System.Text.Json;
using CommandFramework.Core;
using CommandFramework.Http;
using Dapper;
using Npgsql;
using StepWise.Management.Domain.CatalogSteps;
using StepWise.Management.Domain.Targets;
using StepWise.Management.Domain.TestRuns;
using StepWise.Management.Domain.Workflows;

namespace StepWise.Management;

public class WorkflowExecutionService : BackgroundService
{
    private readonly string _connectionString;
    private readonly IEventStore _eventStore;
    private readonly AggregateHandler<WorkflowRunState, WorkflowRunEvent> _runHandler;
    private readonly ILogger<WorkflowExecutionService> _logger;
    private readonly int _maxAttempts;
    private readonly int _pollingIntervalMs;

    public WorkflowExecutionService(
        IConfiguration configuration,
        IEventStore eventStore,
        AggregateHandler<WorkflowRunState, WorkflowRunEvent> runHandler,
        ILogger<WorkflowExecutionService> logger)
    {
        _connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");
        _eventStore = eventStore;
        _runHandler = runHandler;
        _logger = logger;
        _maxAttempts = configuration.GetValue("WorkflowExecution:MaxAttempts", 3);
        _pollingIntervalMs = configuration.GetValue("WorkflowExecution:PollingIntervalMs", 1000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in workflow execution loop.");
            }

            try
            {
                await Task.Delay(_pollingIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessNextAsync(CancellationToken cancellationToken)
    {
        long outboxId;
        string runId;
        string workflowId;

        // Claim one outbox row
        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync(cancellationToken);
            await using var tx = await conn.BeginTransactionAsync(cancellationToken);

            var row = await conn.QueryFirstOrDefaultAsync<(long Id, string Payload)>(
                @"SELECT id, payload::text FROM outbox
                  WHERE processed_at IS NULL AND attempts < @maxAttempts
                  ORDER BY id
                  LIMIT 1
                  FOR UPDATE SKIP LOCKED",
                new { maxAttempts = _maxAttempts }, tx);

            if (row == default)
            {
                await tx.RollbackAsync(cancellationToken);
                return;
            }

            await conn.ExecuteAsync(
                "UPDATE outbox SET attempts = attempts + 1 WHERE id = @id",
                new { id = row.Id }, tx);

            await tx.CommitAsync(cancellationToken);

            outboxId = row.Id;
            var payload = JsonSerializer.Deserialize<OutboxPayload>(row.Payload, JsonConfig.Options)!;
            runId = payload.RunId;
            workflowId = payload.WorkflowId;
        }

        _logger.LogInformation(
            "Processing outbox row {OutboxId}: run {RunId} for workflow {WorkflowId}",
            outboxId, runId, workflowId);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await ExecuteWorkflowAsync(workflowId, cancellationToken);
            stopwatch.Stop();

            var batch = new CommandBatch(runId, [new CommandEnvelope(
                nameof(RecordResult),
                JsonSerializer.SerializeToElement(new RecordResult(
                    result.Passed,
                    JsonSerializer.SerializeToElement(result, JsonConfig.Options),
                    stopwatch.ElapsedMilliseconds), JsonConfig.Options))]);

            await _runHandler.ExecuteAsync(
                batch,
                WorkflowRunAggregate.DeserializeCommand,
                WorkflowRunAggregate.DeserializeEvent);

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await conn.ExecuteAsync(
                "UPDATE outbox SET processed_at = now() WHERE id = @id",
                new { id = outboxId });

            _logger.LogInformation("Run {RunId} completed. Passed={Passed}", runId, result.Passed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Run {RunId} failed.", runId);

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            var attempts = await conn.ExecuteScalarAsync<int>(
                "SELECT attempts FROM outbox WHERE id = @id",
                new { id = outboxId });

            if (attempts >= _maxAttempts)
            {
                _logger.LogError(
                    "Run {RunId} exhausted all {MaxAttempts} attempts. Recording failure.",
                    runId, _maxAttempts);

                var batch = new CommandBatch(runId, [new CommandEnvelope(
                    nameof(RecordFailure),
                    JsonSerializer.SerializeToElement(new RecordFailure(
                        ex.Message,
                        stopwatch.ElapsedMilliseconds), JsonConfig.Options))]);

                await _runHandler.ExecuteAsync(
                    batch,
                    WorkflowRunAggregate.DeserializeCommand,
                    WorkflowRunAggregate.DeserializeEvent);

                await conn.ExecuteAsync(
                    "UPDATE outbox SET processed_at = now(), last_error = @error WHERE id = @id",
                    new { id = outboxId, error = ex.Message });
            }
            else
            {
                await conn.ExecuteAsync(
                    "UPDATE outbox SET last_error = @error WHERE id = @id",
                    new { id = outboxId, error = ex.Message });
            }
        }
    }

    private async Task<StepWise.Json.WorkflowResult> ExecuteWorkflowAsync(
        string workflowId, CancellationToken cancellationToken)
    {
        var workflowEvents = await _eventStore.LoadAsync($"workflows/{workflowId}");
        if (workflowEvents.Count == 0)
            throw new InvalidOperationException($"Workflow '{workflowId}' not found.");

        var workflowState = Aggregate.Fold<WorkflowState, WorkflowEvent>(
            workflowEvents.Select(e => WorkflowAggregate.DeserializeEvent(e.EventType, e.Payload)),
            WorkflowAggregate.Apply)!;

        var stepDefs = new Dictionary<string, StepWise.Json.StepDefinition>(StringComparer.OrdinalIgnoreCase);
        var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var workflowStep in workflowState.Steps)
        {
            var catalogStepEvents = await _eventStore.LoadAsync($"catalog-steps/{workflowStep.CatalogStepId}");
            if (catalogStepEvents.Count == 0) continue;

            var catalogStep = Aggregate.Fold<CatalogStepState, CatalogStepEvent>(
                catalogStepEvents.Select(e => CatalogStepAggregate.DeserializeEvent(e.EventType, e.Payload)),
                CatalogStepAggregate.Apply);
            if (catalogStep == null) continue;

            var targetEvents = await _eventStore.LoadAsync($"targets/{catalogStep.TargetId}");
            if (targetEvents.Count > 0)
            {
                var targetState = Aggregate.Fold<TargetState, TargetEvent>(
                    targetEvents.Select(e => TargetAggregate.DeserializeEvent(e.EventType, e.Payload)),
                    TargetAggregate.Apply);
                if (targetState != null)
                    targets[catalogStep.TargetId] = targetState.BaseUrl;
            }

            var workflowDefaults = workflowStep.Defaults.HasValue
                ? JsonSerializer.Deserialize<Dictionary<string, StepWise.Json.FieldValueDefinition>>(
                    workflowStep.Defaults.Value.GetRawText(), JsonConfig.Options)
                : null;

            var catalogDefaults = catalogStep.Defaults.HasValue
                ? JsonSerializer.Deserialize<Dictionary<string, StepWise.Json.FieldValueDefinition>>(
                    catalogStep.Defaults.Value.GetRawText(), JsonConfig.Options)
                : null;

            var mergedDefaults = catalogDefaults != null
                ? new Dictionary<string, StepWise.Json.FieldValueDefinition>(catalogDefaults)
                : new Dictionary<string, StepWise.Json.FieldValueDefinition>();
            if (workflowDefaults != null)
                foreach (var (k, v) in workflowDefaults)
                    mergedDefaults[k] = v;

            stepDefs[workflowStep.Id] = new StepWise.Json.StepDefinition
            {
                Target = catalogStep.TargetId,
                Method = catalogStep.Method,
                Path = catalogStep.Path,
                Defaults = mergedDefaults.Count > 0 ? mergedDefaults : null
            };
        }

        var workflowDef = new StepWise.Json.WorkflowDefinition(
            Name: workflowState.Name,
            Steps: workflowState.Steps
                .Select(s => new StepWise.Json.StepInvocation { Step = s.Id })
                .ToList(),
            Assertions: workflowState.Assertions.Count > 0 ? workflowState.Assertions : null);

        return await StepWise.Json.JsonWorkflowRunner.RunAsync(workflowDef, stepDefs, targets);
    }

    private record OutboxPayload(string RunId, string WorkflowId);
}
