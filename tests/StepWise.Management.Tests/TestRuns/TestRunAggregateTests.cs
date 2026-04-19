using StepWise.Management.Domain.TestRuns;
using Xunit;

namespace StepWise.Management.Tests.TestRuns;

public class TestRunAggregateTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly RecordRun SampleRun = new(
        Id: "run-1",
        WorkflowId: "wf-1",
        WorkflowName: "My Workflow",
        Passed: true,
        ResultJson: "{}",
        StartedAt: DateTimeOffset.UtcNow,
        DurationMs: 123);

    // ── RecordRun ─────────────────────────────────────────────────────────────

    [Fact]
    public void RecordRun_succeeds_on_new_stream()
    {
        var result = TestRunAggregate.Dispatch(null, SampleRun);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.IsType<RunRecorded>(result.Value.First());
    }

    [Fact]
    public void RecordRun_fails_when_run_already_recorded()
    {
        var state = TestRunAggregate.Apply(null, new RunRecorded(
            SampleRun.Id, SampleRun.WorkflowId, SampleRun.WorkflowName,
            SampleRun.Passed, SampleRun.ResultJson, SampleRun.StartedAt, SampleRun.DurationMs));

        var result = TestRunAggregate.Dispatch(state, SampleRun);

        Assert.True(result.IsError);
    }

    [Fact]
    public void RecordRun_fails_when_id_is_empty()
    {
        var cmd = SampleRun with { Id = "" };
        var result = TestRunAggregate.Dispatch(null, cmd);

        Assert.True(result.IsError);
    }

    [Fact]
    public void RecordRun_fails_when_workflow_id_is_empty()
    {
        var cmd = SampleRun with { WorkflowId = "" };
        var result = TestRunAggregate.Dispatch(null, cmd);

        Assert.True(result.IsError);
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_RunRecorded_sets_all_fields()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var state = TestRunAggregate.Apply(null, new RunRecorded(
            "run-1", "wf-1", "My Workflow", true, "{\"passed\":true}", startedAt, 456));

        Assert.Equal("run-1", state.Id);
        Assert.Equal("wf-1", state.WorkflowId);
        Assert.Equal("My Workflow", state.WorkflowName);
        Assert.True(state.Passed);
        Assert.Equal("{\"passed\":true}", state.ResultJson);
        Assert.Equal(startedAt, state.StartedAt);
        Assert.Equal(456, state.DurationMs);
    }

    [Fact]
    public void Apply_RunRecorded_preserves_failed_result()
    {
        var state = TestRunAggregate.Apply(null, new RunRecorded(
            "run-1", "wf-1", "My Workflow", false, "{}", DateTimeOffset.UtcNow, 0));

        Assert.False(state.Passed);
    }
}
