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
using static StepWise.Management.Domain.TestRuns.WorkflowRunCommands;

namespace StepWise.Management;

public class WorkflowExecutionService : BackgroundService
{
    private readonly string _connectionString;
    private readonly IEventStore _eventStore;
    private readonly AggregateHandler<WorkflowRunState, WorkflowRunEvent> _runHandler;
    private readonly ILogger<WorkflowExecutionService> _logger;
    private readonly int _maxAttempts;
    private readonly int _pollingIntervalMs;
    private readonly int _concurrency;

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
        _pollingIntervalMs = configuration.GetValue("WorkflowExecution:PollingIntervalMs", 200);
        _concurrency = configuration.GetValue("WorkflowExecution:Concurrency", 4);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(0, _concurrency).Select(_ => RunWorkerAsync(stoppingToken)));

    private async Task RunWorkerAsync(CancellationToken stoppingToken)
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
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        // Keep FOR UPDATE held for the entire method so concurrent workers can't claim the same row.
        var row = await conn.QueryFirstOrDefaultAsync<(long Id, string Payload, int NewAttempts)>(
            @"SELECT id, payload::text, attempts + 1 AS new_attempts FROM outbox
              WHERE processed_at IS NULL AND attempts < @maxAttempts
              ORDER BY id
              LIMIT 1
              FOR UPDATE SKIP LOCKED",
            new { maxAttempts = _maxAttempts }, tx);

        if (row == default)
            return;

        await conn.ExecuteAsync(
            "UPDATE outbox SET attempts = @attempts WHERE id = @id",
            new { id = row.Id, attempts = row.NewAttempts }, tx);

        var outboxId   = row.Id;
        var newAttempts = row.NewAttempts;
        var payload    = JsonSerializer.Deserialize<OutboxPayload>(row.Payload, JsonConfig.Options)!;
        var runId      = payload.RunId;
        var workflowId = payload.WorkflowId;

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
                ReflectionDeserializer.ForCommands<WorkflowRunCommands>(),
                ReflectionDeserializer.ForEvents<WorkflowRunEvent>());

            await conn.ExecuteAsync(
                "UPDATE outbox SET processed_at = now() WHERE id = @id",
                new { id = outboxId }, tx);

            await tx.CommitAsync(cancellationToken);

            _logger.LogInformation("Run {RunId} completed. Passed={Passed}", runId, result.Passed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Run {RunId} failed (attempt {Attempts}/{MaxAttempts}).", runId, newAttempts, _maxAttempts);

            if (newAttempts >= _maxAttempts)
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
                    ReflectionDeserializer.ForCommands<WorkflowRunCommands>(),
                    ReflectionDeserializer.ForEvents<WorkflowRunEvent>());

                await conn.ExecuteAsync(
                    "UPDATE outbox SET processed_at = now(), last_error = @error WHERE id = @id",
                    new { id = outboxId, error = ex.Message }, tx);
            }
            else
            {
                await conn.ExecuteAsync(
                    "UPDATE outbox SET last_error = @error WHERE id = @id",
                    new { id = outboxId, error = ex.Message }, tx);
            }

            await tx.CommitAsync(cancellationToken);
        }
    }

    private async Task<Walkthrough.Json.WorkflowResult> ExecuteWorkflowAsync(
        string workflowId, CancellationToken cancellationToken)
    {
        var workflowEvents = await _eventStore.LoadAsync($"workflows/{workflowId}");
        if (workflowEvents.Count == 0)
            throw new InvalidOperationException($"Workflow '{workflowId}' not found.");

        var deserializeWorkflowEvent    = ReflectionDeserializer.ForEvents<WorkflowEvent>();
        var deserializeCatalogStepEvent = ReflectionDeserializer.ForEvents<CatalogStepEvent>();
        var deserializeTargetEvent      = ReflectionDeserializer.ForEvents<TargetEvent>();

        var workflowState = Aggregate.Fold<WorkflowState, WorkflowEvent>(
            workflowEvents.Select(e => deserializeWorkflowEvent(e.EventType, e.Payload)),
            WorkflowAggregate.Apply)!;

        var contracts = new Dictionary<string, Walkthrough.Json.StepContractDefinition>(StringComparer.OrdinalIgnoreCase);
        var targetBaseUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var targetSteps = new Dictionary<string, Dictionary<string, Walkthrough.Json.TargetStepDefinition>>(StringComparer.OrdinalIgnoreCase);
        var stepNames = new Dictionary<string, string>(); // workflowStep.Id → catalogStep.StepName

        foreach (var workflowStep in workflowState.Steps)
        {
            var catalogStepEvents = await _eventStore.LoadAsync($"catalog-steps/{workflowStep.CatalogStepId}");
            if (catalogStepEvents.Count == 0)
                throw new InvalidOperationException($"Catalog step '{workflowStep.CatalogStepId}' not found.");

            var catalogStep = Aggregate.Fold<CatalogStepState, CatalogStepEvent>(
                catalogStepEvents.Select(e => deserializeCatalogStepEvent(e.EventType, e.Payload)),
                CatalogStepAggregate.Apply);
            if (catalogStep == null)
                throw new InvalidOperationException($"Catalog step '{workflowStep.CatalogStepId}' could not be folded.");

            stepNames[workflowStep.Id] = catalogStep.StepName;

            var targetEvents = await _eventStore.LoadAsync($"targets/{catalogStep.TargetId}");
            if (targetEvents.Count > 0 && !targetBaseUrls.ContainsKey(catalogStep.TargetId))
            {
                var targetState = Aggregate.Fold<TargetState, TargetEvent>(
                    targetEvents.Select(e => deserializeTargetEvent(e.EventType, e.Payload)),
                    TargetAggregate.Apply);
                if (targetState != null)
                    targetBaseUrls[catalogStep.TargetId] = targetState.BaseUrl;
            }

            var workflowDefaults = workflowStep.Defaults.HasValue
                ? JsonSerializer.Deserialize<Dictionary<string, Walkthrough.Json.FieldValueDefinition>>(
                    workflowStep.Defaults.Value.GetRawText(), JsonConfig.Options)
                : null;

            var catalogDefaults = catalogStep.Defaults.HasValue
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    catalogStep.Defaults.Value.GetRawText(), JsonConfig.Options)
                    ?.ToDictionary(kvp => kvp.Key,
                        kvp => new Walkthrough.Json.FieldValueDefinition { Static = kvp.Value })
                : null;

            var mergedDefaults = catalogDefaults != null
                ? new Dictionary<string, Walkthrough.Json.FieldValueDefinition>(catalogDefaults)
                : new Dictionary<string, Walkthrough.Json.FieldValueDefinition>();
            if (workflowDefaults != null)
                foreach (var (k, v) in workflowDefaults)
                    mergedDefaults[k] = v;

            var headers = catalogStep.Headers.HasValue
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    catalogStep.Headers.Value.GetRawText(), JsonConfig.Options)
                    ?.ToDictionary(kvp => kvp.Key,
                        kvp => new Walkthrough.Json.FieldValueDefinition { Static = kvp.Value })
                : null;

            contracts[workflowStep.Id] = new Walkthrough.Json.StepContractDefinition
            {
                Defaults = mergedDefaults.Count > 0 ? mergedDefaults : null
            };

            if (!targetSteps.TryGetValue(catalogStep.TargetId, out var stepsForTarget))
            {
                stepsForTarget = new Dictionary<string, Walkthrough.Json.TargetStepDefinition>(StringComparer.OrdinalIgnoreCase);
                targetSteps[catalogStep.TargetId] = stepsForTarget;
            }

            stepsForTarget[workflowStep.Id] = new Walkthrough.Json.TargetStepDefinition
            {
                Method = catalogStep.Method,
                Path = catalogStep.Path,
                Headers = headers
            };
        }

        var targets = targetSteps.Select(kvp => new Walkthrough.Json.TargetDefinition
        {
            BaseUrl = targetBaseUrls.TryGetValue(kvp.Key, out var url) ? url : "",
            Steps = kvp.Value
        }).ToList();

        var workflowDef = new Walkthrough.Json.WorkflowDefinition(
            Name: workflowState.Name,
            Steps: workflowState.Steps
                // CaptureAs = step name so assertion paths like "$list-products.total" resolve correctly
                .Select(s => new Walkthrough.Json.StepInvocation { Step = s.Id, CaptureAs = stepNames[s.Id] })
                .ToList(),
            Assertions: workflowState.Assertions.Count > 0 ? workflowState.Assertions : null);

        return await Walkthrough.Json.JsonWorkflowRunner.RunAsync(workflowDef, contracts, targets);
    }

    private record OutboxPayload(string RunId, string WorkflowId);
}
