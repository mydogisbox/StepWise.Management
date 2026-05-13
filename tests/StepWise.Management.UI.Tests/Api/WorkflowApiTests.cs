using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public abstract class WorkflowTestBase : CatalogStepTestBase
{
    protected async Task<UpsertStepOutput> SetupCatalogWithStepAsync()
    {
        await SetupAsync();
        var step = await BuildAsync(new UpsertStepCommand());
        await ExecuteAsync(new PostCatalogStepCommandsRequest());
        return step;
    }
}

public class Workflow_06_Create_NameAssertedAndStepsEmpty : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateWorkflowCommand() with { Name = Static("Test Workflow 6") });
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        var workflow = await ExecuteAsync(new GetWorkflowRequest());

        Assert.Equal("Test Workflow 6", workflow.Name);
        Assert.Empty(workflow.Steps);
    }
}

public class Workflow_07_Rename_NameUpdated : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new RenameWorkflowCommand() with { Name = Static("Renamed Workflow 7") });
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        var workflow = await ExecuteAsync(new GetWorkflowRequest());

        Assert.Equal("Renamed Workflow 7", workflow.Name);
    }
}

public class Workflow_08_AppendStep_OrderAndDefaultsAsserted : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        await SetupCatalogWithStepAsync();

        await BuildAsync(new CreateWorkflowCommand());
        var stepA = await BuildAsync(new AppendStepCommand() with { Defaults = Static<object?>(new Dictionary<string, object?> { ["param"] = "value1" }) });
        var stepB = await BuildAsync(new AppendStepCommand() with { Defaults = Static<object?>(new Dictionary<string, object?> { ["param"] = "value2" }) });
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        var workflow = await ExecuteAsync(new GetWorkflowRequest());

        Assert.Equal(stepA.Id, workflow.Steps[0].Id);
        Assert.Equal("value1", workflow.Steps[0].Defaults?.GetProperty("param").GetString());
        Assert.Equal(stepB.Id, workflow.Steps[1].Id);
        Assert.Equal("value2", workflow.Steps[1].Defaults?.GetProperty("param").GetString());
    }
}

public class Workflow_09_InsertStepBefore_OrderAsserted : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        await SetupCatalogWithStepAsync();

        await BuildAsync(new CreateWorkflowCommand());
        var stepA    = await BuildAsync(new AppendStepCommand() with { Defaults = Static<object?>(new Dictionary<string, object?> { ["param"] = "value1" }) });
        var stepB    = await BuildAsync(new AppendStepCommand() with { Defaults = Static<object?>(new Dictionary<string, object?> { ["param"] = "value2" }) });
        var inserted = await BuildAsync(new InsertStepBeforeCommand() with
        {
            BeforeId = Static(stepB.Id),
            Defaults = Static<object?>(new Dictionary<string, object?> { ["param"] = "value3" })
        });
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        var workflow = await ExecuteAsync(new GetWorkflowRequest());

        Assert.Equal(stepA.Id,    workflow.Steps[0].Id);
        Assert.Equal("value1",    workflow.Steps[0].Defaults?.GetProperty("param").GetString());
        Assert.Equal(inserted.Id, workflow.Steps[1].Id);
        Assert.Equal("value3",    workflow.Steps[1].Defaults?.GetProperty("param").GetString());
        Assert.Equal(stepB.Id,    workflow.Steps[2].Id);
        Assert.Equal("value2",    workflow.Steps[2].Defaults?.GetProperty("param").GetString());
    }
}

public class Workflow_10_RemoveStep_OneStepRemains : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        await SetupCatalogWithStepAsync();

        await BuildAsync(new CreateWorkflowCommand());
        var stepA = await BuildAsync(new AppendStepCommand());
        var stepB = await BuildAsync(new AppendStepCommand());
        await BuildAsync(new RemoveStepCommand() with { Id = Static(stepA.Id) });
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        var workflow = await ExecuteAsync(new GetWorkflowRequest());

        Assert.Single(workflow.Steps);
        Assert.Equal(stepB.Id, workflow.Steps[0].Id);
    }
}

public class Workflow_11_SetStepDefaults_DefaultsAsserted : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        await SetupCatalogWithStepAsync();

        await BuildAsync(new CreateWorkflowCommand());
        var step = await BuildAsync(new AppendStepCommand());
        await BuildAsync(new SetStepDefaultsCommand() with
        {
            Defaults = Static<object?>(new Dictionary<string, object?> { ["param"] = "value1" })
        });
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        var workflow = await ExecuteAsync(new GetWorkflowRequest());

        Assert.Single(workflow.Steps);
        Assert.Equal(step.Id, workflow.Steps[0].Id);
        Assert.Equal("value1", workflow.Steps[0].Defaults?.GetProperty("param").GetString());
    }
}

public class Workflow_12_BadAssertion_StoredSuccessfully : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        await SetupCatalogWithStepAsync();

        await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new AppendStepCommand());
        await BuildAsync(new AddAssertionCommand() with
        {
            Assertion = Static<object>(new { equal = new[] { "nonExistentStep.id", "appendStep.payload.id" } })
        });
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        var workflow = await ExecuteAsync(new GetWorkflowRequest());

        Assert.Single(workflow.Assertions);
    }
}

public class Workflow_13_AddAssertion_StoredSuccessfully : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        await SetupCatalogWithStepAsync();

        var step = await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new AppendStepCommand());
        await BuildAsync(new AddAssertionCommand() with
        {
            Assertion = Static<object>(new { notEmpty = step.Id })
        });
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        var workflow = await ExecuteAsync(new GetWorkflowRequest());

        Assert.Single(workflow.Assertions);
    }
}

public class Workflow_14_ArchiveWorkflow_IsArchivedTrue : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new ArchiveWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        var workflow = await ExecuteAsync(new GetWorkflowRequest());

        Assert.True(workflow.IsArchived);
    }
}

public class Workflow_15_UnarchiveWorkflow_IsArchivedFalse : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new ArchiveWorkflowCommand());
        await BuildAsync(new UnarchiveWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        var workflow = await ExecuteAsync(new GetWorkflowRequest());

        Assert.False(workflow.IsArchived);
    }
}

public class Workflow_16_UpdateDescription_DescriptionAsserted : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new UpdateDescriptionCommand() with { Description = Static("A workflow description") });
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        var workflow = await ExecuteAsync(new GetWorkflowRequest());

        Assert.Equal("A workflow description", workflow.Description);
    }
}

public class Workflow_17_Archive_ExcludedFromList : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new ArchiveWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        var workflows = await ExecuteAsync(new ListWorkflowsRequest());

        Assert.DoesNotContain(workflows, w => w.Name == create.Name);
    }
}

public class Workflow_18_Archive_IncludedInListWhenShowArchived : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new ArchiveWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        var workflows = await ExecuteAsync(new ListWorkflowsRequest() with { ShowArchived = Static("true") });

        var workflow = workflows.Single(w => w.Id == create.Id);
        Assert.True(workflow.IsArchived);
    }
}

public class Runs_01_List_ShowsCompletedRun : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        await SetupCatalogWithStepAsync();

        var create = await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new AppendStepCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        await ExecuteAsync(new RunWorkflowRequest());
        await PollAsync(new GetRunRequest(), r => r.Status == "completed");
        var runs = await ExecuteAsync(new ListRunsRequest());

        var run = runs.Single(r => r.WorkflowId == create.Id);
        Assert.True(run.Passed);
    }
}
