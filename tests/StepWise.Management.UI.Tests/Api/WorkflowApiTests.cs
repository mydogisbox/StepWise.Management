using Walkthrough.Http;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public abstract class WorkflowTestBase : ManagementTestBase
{
    protected async Task SetupCatalogWithStepAsync()
    {
        var target = await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());
        await ExecuteAsync(new ListTargetsRequest() with { Name = Static(target.Name) });

        await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());
        await BuildAsync(new UpsertStepCommand());
        await ExecuteAsync(new PostCatalogStepCommandsRequest());
    }
}

public class Workflow_19_CreateWorkflow_EmptyName_Returns422 : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateWorkflowCommand() with { Name = Static("") });
        var raw = (HttpRawResult)await ExecuteRawAsync(new PostWorkflowCommandsRequest());

        Assert.Equal(422, raw.StatusCode);
    }
}

public class Workflow_20_CreateWorkflow_DuplicateCreate_Returns422 : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await BuildAsync(new CreateWorkflowCommand() with { Id = Static(create.Id) });
        var raw = (HttpRawResult)await ExecuteRawAsync(new PostWorkflowCommandsRequest());

        Assert.Equal(422, raw.StatusCode);
    }
}

public class Workflow_21_RenameWorkflow_EmptyName_Returns422 : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new RenameWorkflowCommand() with { Name = Static("") });
        var raw = (HttpRawResult)await ExecuteRawAsync(new PostWorkflowCommandsRequest());

        Assert.Equal(422, raw.StatusCode);
    }
}

public class Workflow_22_RenameWorkflow_DoesNotExist_Returns422 : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new RenameWorkflowCommand() with { Name = Static("New Name") });
        var raw = (HttpRawResult)await ExecuteRawAsync(new PostWorkflowCommandsRequest() with
        {
            AggregateId = Static(Guid.NewGuid().ToString())
        });

        Assert.Equal(422, raw.StatusCode);
    }
}

public class Workflow_23_AppendStep_EmptyId_Returns422 : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        await SetupCatalogWithStepAsync();

        await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new AppendStepCommand() with { Id = Static("") });
        var raw = (HttpRawResult)await ExecuteRawAsync(new PostWorkflowCommandsRequest());

        Assert.Equal(422, raw.StatusCode);
    }
}

public class Workflow_24_InsertStepBefore_NotFound_Returns422 : WorkflowTestBase
{
    [Fact]
    public async Task Test()
    {
        await SetupCatalogWithStepAsync();

        await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new InsertStepBeforeCommand() with { BeforeId = Static("nonexistent-id") });
        var raw = (HttpRawResult)await ExecuteRawAsync(new PostWorkflowCommandsRequest());

        Assert.Equal(422, raw.StatusCode);
    }
}

public class Workflow_25_RemoveStep_NotFound_Returns422 : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new RemoveStepCommand() with { Id = Static("nonexistent-id") });
        var raw = (HttpRawResult)await ExecuteRawAsync(new PostWorkflowCommandsRequest());

        Assert.Equal(422, raw.StatusCode);
    }
}

public class Workflow_26_SetStepDefaults_NotFound_Returns422 : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new SetStepDefaultsCommand() with { Id = Static("nonexistent-id") });
        var raw = (HttpRawResult)await ExecuteRawAsync(new PostWorkflowCommandsRequest());

        Assert.Equal(422, raw.StatusCode);
    }
}

public class Workflow_27_ArchiveWorkflow_AlreadyArchived_Returns422 : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new ArchiveWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await BuildAsync(new ArchiveWorkflowCommand());
        var raw = (HttpRawResult)await ExecuteRawAsync(new PostWorkflowCommandsRequest());

        Assert.Equal(422, raw.StatusCode);
    }
}

public class Workflow_28_UnarchiveWorkflow_NotArchived_Returns422 : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await BuildAsync(new UnarchiveWorkflowCommand());
        var raw = (HttpRawResult)await ExecuteRawAsync(new PostWorkflowCommandsRequest());

        Assert.Equal(422, raw.StatusCode);
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

        Assert.DoesNotContain(workflows.Items, w => w.Name == create.Name);
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

        var workflow = workflows.Items.Single(w => w.Id == create.Id);
        Assert.True(workflow.IsArchived);
    }
}

public class Workflow_36_Paging_PageSizeIsRespected : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        for (var i = 0; i < 3; i++)
        {
            await BuildAsync(new CreateWorkflowCommand());
            await ExecuteAsync(new PostWorkflowCommandsRequest());
        }

        var page1 = await ExecuteAsync(new ListWorkflowsRequest() with { PageSize = Static(2) });
        Assert.Equal(2, page1.Items.Length);
        Assert.True(page1.TotalPages >= 2);
        Assert.Equal(2, page1.PageSize);

        var page2 = await ExecuteAsync(new ListWorkflowsRequest() with { Page = Static(2), PageSize = Static(2) });
        Assert.True(page2.Items.Length >= 1);
    }
}
