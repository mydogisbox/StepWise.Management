using System.Text.Json;
using StepWise.Management.Domain.TestRuns;
using Xunit;

namespace StepWise.Management.Tests.TestRuns;

public class WorkflowRunAggregateTests
{
    private static readonly JsonElement EmptyResult = JsonDocument.Parse("{}").RootElement;

    // ── TriggerRun ────────────────────────────────────────────────────────────

    [Fact]
    public void TriggerRun_succeeds_on_new_stream()
    {
        var result = WorkflowRunAggregate.Dispatch(null, new TriggerRun("run-1", "wf-1"));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.IsType<RunTriggered>(result.Value.First());
    }

    [Fact]
    public void TriggerRun_fails_when_run_already_exists()
    {
        var state = Apply(new RunTriggered("run-1", "wf-1", DateTimeOffset.UtcNow));

        var result = WorkflowRunAggregate.Dispatch(state, new TriggerRun("run-1", "wf-1"));

        Assert.True(result.IsError);
    }

    [Fact]
    public void TriggerRun_fails_when_id_is_empty()
    {
        var result = WorkflowRunAggregate.Dispatch(null, new TriggerRun("", "wf-1"));

        Assert.True(result.IsError);
    }

    [Fact]
    public void TriggerRun_fails_when_workflow_id_is_empty()
    {
        var result = WorkflowRunAggregate.Dispatch(null, new TriggerRun("run-1", ""));

        Assert.True(result.IsError);
    }

    // ── RecordResult ──────────────────────────────────────────────────────────

    [Fact]
    public void RecordResult_succeeds_when_pending()
    {
        var state = Apply(new RunTriggered("run-1", "wf-1", DateTimeOffset.UtcNow));

        var result = WorkflowRunAggregate.Dispatch(state, new RecordResult(true, EmptyResult, 100));

        Assert.True(result.IsSuccess);
        Assert.IsType<RunCompleted>(result.Value.First());
    }

    [Fact]
    public void RecordResult_fails_when_already_completed()
    {
        var state = Apply(
            new RunTriggered("run-1", "wf-1", DateTimeOffset.UtcNow),
            new RunCompleted("run-1", true, EmptyResult, 100));

        var result = WorkflowRunAggregate.Dispatch(state, new RecordResult(true, EmptyResult, 100));

        Assert.True(result.IsError);
    }

    // ── RecordFailure ─────────────────────────────────────────────────────────

    [Fact]
    public void RecordFailure_succeeds_when_pending()
    {
        var state = Apply(new RunTriggered("run-1", "wf-1", DateTimeOffset.UtcNow));

        var result = WorkflowRunAggregate.Dispatch(state, new RecordFailure("Something exploded.", 50));

        Assert.True(result.IsSuccess);
        Assert.IsType<RunFailed>(result.Value.First());
    }

    [Fact]
    public void RecordFailure_fails_when_already_failed()
    {
        var state = Apply(
            new RunTriggered("run-1", "wf-1", DateTimeOffset.UtcNow),
            new RunFailed("run-1", "error", 50));

        var result = WorkflowRunAggregate.Dispatch(state, new RecordFailure("another error", 50));

        Assert.True(result.IsError);
    }

    [Fact]
    public void RecordFailure_fails_when_error_is_empty()
    {
        var state = Apply(new RunTriggered("run-1", "wf-1", DateTimeOffset.UtcNow));

        var result = WorkflowRunAggregate.Dispatch(state, new RecordFailure("", 50));

        Assert.True(result.IsError);
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_RunTriggered_sets_pending_state()
    {
        var triggeredAt = DateTimeOffset.UtcNow;
        var state = WorkflowRunAggregate.Apply(null, new RunTriggered("run-1", "wf-1", triggeredAt));

        Assert.Equal("run-1", state.Id);
        Assert.Equal("wf-1", state.WorkflowId);
        Assert.Equal("pending", state.Status);
        Assert.Null(state.Passed);
        Assert.Null(state.Result);
        Assert.Null(state.Error);
        Assert.Equal(triggeredAt, state.TriggeredAt);
        Assert.Null(state.DurationMs);
    }

    [Fact]
    public void Apply_RunCompleted_sets_completed_state()
    {
        var result = JsonDocument.Parse("{\"passed\":true}").RootElement;
        var state = Apply(
            new RunTriggered("run-1", "wf-1", DateTimeOffset.UtcNow),
            new RunCompleted("run-1", true, result, 456));

        Assert.Equal("completed", state.Status);
        Assert.True(state.Passed);
        Assert.True(state.Result!.Value.GetProperty("passed").GetBoolean());
        Assert.Equal(456, state.DurationMs);
    }

    [Fact]
    public void Apply_RunFailed_sets_failed_state()
    {
        var state = Apply(
            new RunTriggered("run-1", "wf-1", DateTimeOffset.UtcNow),
            new RunFailed("run-1", "boom", 99));

        Assert.Equal("failed", state.Status);
        Assert.Equal("boom", state.Error);
        Assert.Equal(99, state.DurationMs);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WorkflowRunState Apply(params WorkflowRunEvent[] events)
        => events.Aggregate(
            (WorkflowRunState?)null,
            (state, e) => WorkflowRunAggregate.Apply(state, e))!;
}
