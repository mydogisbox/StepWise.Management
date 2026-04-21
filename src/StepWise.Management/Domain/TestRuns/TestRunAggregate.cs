using System.Text.Json;
using CommandFramework.Core;
using StepWise.Management;

namespace StepWise.Management.Domain.TestRuns;

// State
public record WorkflowRunState(
    string Id,
    string WorkflowId,
    string Status,       // "pending" | "completed" | "failed"
    bool? Passed,
    JsonElement? Result,
    string? Error,
    DateTimeOffset TriggeredAt,
    long? DurationMs);

// Events
public abstract record WorkflowRunEvent;
public record RunTriggered(string Id, string WorkflowId, DateTimeOffset TriggeredAt) : WorkflowRunEvent;
public record RunCompleted(string Id, bool Passed, JsonElement Result, long DurationMs) : WorkflowRunEvent;
public record RunFailed(string Id, string Error, long DurationMs) : WorkflowRunEvent;

// Commands
public record TriggerRun(string Id, string WorkflowId);
public record RecordResult(bool Passed, JsonElement Result, long DurationMs);
public record RecordFailure(string Error, long DurationMs);

public static class WorkflowRunAggregate
{
    public static Result<IEnumerable<WorkflowRunEvent>> Handle(WorkflowRunState? state, TriggerRun cmd)
    {
        if (state != null) return $"Run '{cmd.Id}' already exists.";
        if (string.IsNullOrWhiteSpace(cmd.Id)) return "Run Id is required.";
        if (string.IsNullOrWhiteSpace(cmd.WorkflowId)) return "WorkflowId is required.";
        return new WorkflowRunEvent[] { new RunTriggered(cmd.Id, cmd.WorkflowId, DateTimeOffset.UtcNow) };
    }

    public static Result<IEnumerable<WorkflowRunEvent>> Handle(WorkflowRunState state, RecordResult cmd)
    {
        if (state.Status != "pending") return $"Run '{state.Id}' is not pending (status: {state.Status}).";
        return new WorkflowRunEvent[] { new RunCompleted(state.Id, cmd.Passed, cmd.Result, cmd.DurationMs) };
    }

    public static Result<IEnumerable<WorkflowRunEvent>> Handle(WorkflowRunState state, RecordFailure cmd)
    {
        if (state.Status != "pending") return $"Run '{state.Id}' is not pending (status: {state.Status}).";
        if (string.IsNullOrWhiteSpace(cmd.Error)) return "Error message is required.";
        return new WorkflowRunEvent[] { new RunFailed(state.Id, cmd.Error, cmd.DurationMs) };
    }

    public static WorkflowRunState Apply(WorkflowRunState? state, WorkflowRunEvent e) => e switch
    {
        RunTriggered evt => new WorkflowRunState(
            evt.Id, evt.WorkflowId, "pending",
            null, null, null, evt.TriggeredAt, null),

        RunCompleted evt => state! with
        {
            Status = "completed",
            Passed = evt.Passed,
            Result = evt.Result,
            DurationMs = evt.DurationMs
        },

        RunFailed evt => state! with
        {
            Status = "failed",
            Error = evt.Error,
            DurationMs = evt.DurationMs
        },

        _ => throw new InvalidOperationException($"Unknown event: {e.GetType().Name}")
    };

    public static Result<IEnumerable<WorkflowRunEvent>> Dispatch(WorkflowRunState? state, object command)
        => command switch
        {
            TriggerRun cmd => Handle(state, cmd),
            RecordResult cmd when state != null => Handle(state, cmd),
            RecordFailure cmd when state != null => Handle(state, cmd),
            _ when state == null => "Run does not exist.",
            _ => $"Unknown command '{command.GetType().Name}'."
        };

    public static object DeserializeCommand(string type, JsonElement payload)
        => type switch
        {
            nameof(TriggerRun) => payload.Deserialize<TriggerRun>(JsonConfig.Options)!,
            nameof(RecordResult) => payload.Deserialize<RecordResult>(JsonConfig.Options)!,
            nameof(RecordFailure) => payload.Deserialize<RecordFailure>(JsonConfig.Options)!,
            _ => throw new InvalidOperationException($"Unknown command type '{type}'.")
        };

    public static WorkflowRunEvent DeserializeEvent(string type, string payload)
        => type switch
        {
            nameof(RunTriggered) => JsonSerializer.Deserialize<RunTriggered>(payload, JsonConfig.Options)!,
            nameof(RunCompleted) => JsonSerializer.Deserialize<RunCompleted>(payload, JsonConfig.Options)!,
            nameof(RunFailed) => JsonSerializer.Deserialize<RunFailed>(payload, JsonConfig.Options)!,
            _ => throw new InvalidOperationException($"Unknown event type '{type}'.")
        };

    public static readonly AggregateDefinition<WorkflowRunState, WorkflowRunEvent> Definition = new(
        Dispatch: Dispatch,
        Apply: Apply);
}
