using System.Text.Json;
using CommandFramework.Core;

namespace StepWise.Management.Domain.TestRuns;

// State
public record TestRunState(
    string Id,
    string WorkflowId,
    string WorkflowName,
    bool Passed,
    string ResultJson,
    DateTimeOffset StartedAt,
    long DurationMs);

// Events
public abstract record TestRunEvent;
public record RunRecorded(
    string Id,
    string WorkflowId,
    string WorkflowName,
    bool Passed,
    string ResultJson,
    DateTimeOffset StartedAt,
    long DurationMs) : TestRunEvent;

// Commands
public record RecordRun(
    string Id,
    string WorkflowId,
    string WorkflowName,
    bool Passed,
    string ResultJson,
    DateTimeOffset StartedAt,
    long DurationMs);

public static class TestRunAggregate
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static Result<IEnumerable<TestRunEvent>> Handle(TestRunState? state, RecordRun cmd)
    {
        if (state != null) return $"Run '{cmd.Id}' has already been recorded.";
        if (string.IsNullOrWhiteSpace(cmd.Id)) return "Run Id is required.";
        if (string.IsNullOrWhiteSpace(cmd.WorkflowId)) return "WorkflowId is required.";

        return new TestRunEvent[]
        {
            new RunRecorded(
                cmd.Id,
                cmd.WorkflowId,
                cmd.WorkflowName,
                cmd.Passed,
                cmd.ResultJson,
                cmd.StartedAt,
                cmd.DurationMs)
        };
    }

    public static TestRunState Apply(TestRunState? state, TestRunEvent e)
        => e switch
        {
            RunRecorded evt => new TestRunState(
                evt.Id,
                evt.WorkflowId,
                evt.WorkflowName,
                evt.Passed,
                evt.ResultJson,
                evt.StartedAt,
                evt.DurationMs),
            _ => throw new InvalidOperationException($"Unknown event: {e.GetType().Name}")
        };

    public static Result<IEnumerable<TestRunEvent>> Dispatch(TestRunState? state, object command)
        => command switch
        {
            RecordRun cmd => Handle(state, cmd),
            _ => $"Unknown command '{command.GetType().Name}'."
        };

    public static object DeserializeCommand(string type, JsonElement payload)
        => type switch
        {
            nameof(RecordRun) => payload.Deserialize<RecordRun>(JsonOptions)!,
            _ => throw new InvalidOperationException($"Unknown command type '{type}'.")
        };

    public static TestRunEvent DeserializeEvent(string type, string payload)
        => type switch
        {
            nameof(RunRecorded) => JsonSerializer.Deserialize<RunRecorded>(payload, JsonOptions)!,
            _ => throw new InvalidOperationException($"Unknown event type '{type}'.")
        };

    public static readonly AggregateDefinition<TestRunState, TestRunEvent> Definition = new(
        Dispatch: Dispatch,
        Apply: Apply);
}
