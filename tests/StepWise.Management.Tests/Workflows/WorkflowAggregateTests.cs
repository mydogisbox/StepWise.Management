using System.Text.Json;
using StepWise.Json;
using StepWise.Management.Domain.Workflows;
using Xunit;

namespace StepWise.Management.Tests.Workflows;

public class WorkflowAggregateTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WorkflowState Created(string name = "My Workflow")
        => WorkflowAggregate.Apply(null, new WorkflowCreated("test-id", name));

    private static WorkflowState Apply(WorkflowState state, IEnumerable<WorkflowEvent> events)
        => events.Aggregate(state, WorkflowAggregate.Apply);

    private static (bool isSuccess, WorkflowState? state, string? error) Dispatch(WorkflowState? state, object command)
    {
        var result = WorkflowAggregate.Dispatch(state, command);
        if (result.IsError) return (false, null, result.Error);
        var newState = Apply(state ?? Created(), result.Value);
        return (true, newState, null);
    }

    private static WorkflowStep MakeStep(string id = "step-1") =>
        new WorkflowStep { Id = id, CatalogStepId = "cs-1", CatalogId = "cat-1" };

    // ── CreateWorkflow ────────────────────────────────────────────────────────

    [Fact]
    public void CreateWorkflow_succeeds_on_new_stream()
    {
        var result = WorkflowAggregate.Dispatch(null, new CreateWorkflow("test-id", "My Workflow"));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.IsType<WorkflowCreated>(result.Value.First());
    }

    [Fact]
    public void CreateWorkflow_fails_when_workflow_already_exists()
    {
        var state = Created();
        var result = WorkflowAggregate.Dispatch(state, new CreateWorkflow("test-id", "My Workflow"));

        Assert.True(result.IsError);
    }

    [Fact]
    public void CreateWorkflow_fails_when_name_is_empty()
    {
        var result = WorkflowAggregate.Dispatch(null, new CreateWorkflow("test-id", ""));

        Assert.True(result.IsError);
    }

    [Fact]
    public void Apply_WorkflowCreated_initializes_empty_state()
    {
        var state = Created("My Workflow");

        Assert.Equal("My Workflow", state.Name);
        Assert.Empty(state.Steps);
        Assert.Empty(state.Assertions);
        Assert.False(state.IsArchived);
    }

    // ── RenameWorkflow ────────────────────────────────────────────────────────

    [Fact]
    public void RenameWorkflow_updates_name()
    {
        var state = Created();
        var (ok, newState, _) = Dispatch(state, new RenameWorkflow("New Name"));

        Assert.True(ok);
        Assert.Equal("New Name", newState!.Name);
    }

    [Fact]
    public void RenameWorkflow_fails_when_name_is_empty()
    {
        var state = Created();
        var result = WorkflowAggregate.Dispatch(state, new RenameWorkflow(""));

        Assert.True(result.IsError);
    }

    [Fact]
    public void RenameWorkflow_fails_when_workflow_does_not_exist()
    {
        var result = WorkflowAggregate.Dispatch(null, new RenameWorkflow("New Name"));

        Assert.True(result.IsError);
    }

    // ── AppendStep ────────────────────────────────────────────────────────────

    [Fact]
    public void AppendStep_adds_step_to_end()
    {
        var state = Created();
        var step = MakeStep("step-1");

        var (ok, newState, _) = Dispatch(state, new AppendStep(step.Id, step.CatalogStepId, step.CatalogId));

        Assert.True(ok);
        Assert.Single(newState!.Steps);
        Assert.Equal("step-1", newState.Steps[0].Id);
    }

    [Fact]
    public void AppendStep_preserves_order()
    {
        var state = Created();
        state = WorkflowAggregate.Apply(state, new WorkflowStepAppended(MakeStep("step-1")));

        var (ok, newState, _) = Dispatch(state, new AppendStep("step-2", "cs-1", "cat-1"));

        Assert.True(ok);
        Assert.Equal(2, newState!.Steps.Count);
        Assert.Equal("step-1", newState.Steps[0].Id);
        Assert.Equal("step-2", newState.Steps[1].Id);
    }

    [Fact]
    public void AppendStep_fails_when_id_is_empty()
    {
        var state = Created();
        var result = WorkflowAggregate.Dispatch(state, new AppendStep("", "cs-1", "cat-1"));

        Assert.True(result.IsError);
    }

    // ── InsertStepBefore ──────────────────────────────────────────────────────

    [Fact]
    public void InsertStepBefore_inserts_at_correct_position()
    {
        var state = Created();
        state = WorkflowAggregate.Apply(state, new WorkflowStepAppended(MakeStep("step-1")));
        state = WorkflowAggregate.Apply(state, new WorkflowStepAppended(MakeStep("step-2")));

        var newStep = MakeStep("step-new");
        var (ok, newState, _) = Dispatch(state, new InsertStepBefore("step-2", newStep.Id, newStep.CatalogStepId, newStep.CatalogId));

        Assert.True(ok);
        Assert.Equal(3, newState!.Steps.Count);
        Assert.Equal("step-1", newState.Steps[0].Id);
        Assert.Equal("step-new", newState.Steps[1].Id);
        Assert.Equal("step-2", newState.Steps[2].Id);
    }

    [Fact]
    public void InsertStepBefore_fails_when_before_id_not_found()
    {
        var state = Created();
        var result = WorkflowAggregate.Dispatch(state, new InsertStepBefore("nonexistent", "step-1", "cs-1", "cat-1"));

        Assert.True(result.IsError);
    }

    // ── RemoveStep ────────────────────────────────────────────────────────────

    [Fact]
    public void RemoveStep_removes_step_by_id()
    {
        var state = Created();
        state = WorkflowAggregate.Apply(state, new WorkflowStepAppended(MakeStep("step-1")));
        state = WorkflowAggregate.Apply(state, new WorkflowStepAppended(MakeStep("step-2")));

        var (ok, newState, _) = Dispatch(state, new RemoveStep("step-1"));

        Assert.True(ok);
        Assert.Single(newState!.Steps);
        Assert.Equal("step-2", newState.Steps[0].Id);
    }

    [Fact]
    public void RemoveStep_fails_when_step_not_found()
    {
        var state = Created();
        var result = WorkflowAggregate.Dispatch(state, new RemoveStep("nonexistent"));

        Assert.True(result.IsError);
    }

    // ── SetStepDefaults ───────────────────────────────────────────────────────

    [Fact]
    public void SetStepDefaults_updates_defaults()
    {
        var state = Created();
        state = WorkflowAggregate.Apply(state, new WorkflowStepAppended(MakeStep("step-1")));

        var defaults = JsonDocument.Parse(@"{""param"":""value1""}").RootElement;
        var (ok, newState, _) = Dispatch(state, new SetStepDefaults("step-1", defaults));

        Assert.True(ok);
        Assert.Equal("value1", newState!.Steps[0].Defaults?.GetProperty("param").GetString());
    }

    [Fact]
    public void SetStepDefaults_fails_when_step_not_found()
    {
        var state = Created();
        var result = WorkflowAggregate.Dispatch(state, new SetStepDefaults("nonexistent", null));

        Assert.True(result.IsError);
    }

    // ── AddAssertion ──────────────────────────────────────────────────────────

    [Fact]
    public void AddAssertion_appends_assertion()
    {
        var state = Created();
        var assertion = new AssertionDefinition { NotEmpty = "createUser.id" };

        var (ok, newState, _) = Dispatch(state, new AddAssertion(assertion));

        Assert.True(ok);
        Assert.Single(newState!.Assertions);
        Assert.Equal("createUser.id", newState.Assertions[0].NotEmpty);
    }

    // ── ArchiveWorkflow / UnarchiveWorkflow ───────────────────────────────────

    [Fact]
    public void ArchiveWorkflow_sets_archived_true()
    {
        var state = Created();
        var (ok, newState, _) = Dispatch(state, new ArchiveWorkflow());

        Assert.True(ok);
        Assert.True(newState!.IsArchived);
    }

    [Fact]
    public void ArchiveWorkflow_fails_when_already_archived()
    {
        var state = Created();
        state = WorkflowAggregate.Apply(state, new WorkflowArchived("test-id"));

        var result = WorkflowAggregate.Dispatch(state, new ArchiveWorkflow());

        Assert.True(result.IsError);
    }

    [Fact]
    public void UnarchiveWorkflow_sets_archived_false()
    {
        var state = Created();
        state = WorkflowAggregate.Apply(state, new WorkflowArchived("test-id"));

        var (ok, newState, _) = Dispatch(state, new UnarchiveWorkflow());

        Assert.True(ok);
        Assert.False(newState!.IsArchived);
    }

    [Fact]
    public void UnarchiveWorkflow_fails_when_not_archived()
    {
        var state = Created();
        var result = WorkflowAggregate.Dispatch(state, new UnarchiveWorkflow());

        Assert.True(result.IsError);
    }
}
