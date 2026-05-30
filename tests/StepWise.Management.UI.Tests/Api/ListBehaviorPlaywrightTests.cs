using Microsoft.Playwright;
using Walkthrough.Core;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public class Workflow_Archive_ExcludedFromList_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        var workflow = await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new ArchiveWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        var listed = await ExecuteAsync(new ListWorkflowsRequest() with { Name = Static(workflow.Name) });
        Assert.Empty(listed.Items);
    }
}

public class Workflow_Archive_IncludedWhenShowArchived_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        var workflow = await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new ArchiveWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        var listed = await ExecuteAsync(new ListWorkflowsRequest() with { ShowArchived = Static("true"), Name = Static(workflow.Name) });
        var item = Assert.Single(listed.Items);
        Assert.True(item.IsArchived);
    }
}

public class Workflow_Paging_PagerAppearsAfterPageSize_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        for (var i = 0; i < 11; i++)
        {
            await BuildAsync(new CreateWorkflowCommand());
            await ExecuteAsync(new PostWorkflowCommandsRequest());
        }

        await ExecuteAsync(new ListWorkflowsRequest());
        var page2 = await ExecuteAsync(new NextWorkflowsPageRequest());
        Assert.Equal(2, page2.CurrentPage);
    }
}

public class Workflow_CreateViaForm_DetailOpens_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        await ExecuteAsync(new ListWorkflowsRequest());
        var created = await ExecuteAsync(new CreateWorkflowViaFormRequest());

        await Assertions.Expect(Page.Locator("#workflow-detail")).ToBeVisibleAsync();
        var nameInput = await Page.InputValueAsync("#workflow-name-input");
        Assert.Equal(created.Name, nameInput);
    }
}

public class Workflow_Archive_DisappearsFromList_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        var workflow = await BuildAsync(new CreateWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await ExecuteAsync(new ListWorkflowsRequest());
        await ExecuteAsync(new OpenWorkflowDetailRequest());
        await ExecuteAsync(new ArchiveWorkflowViaUiRequest());

        var listed = await ExecuteAsync(new ListWorkflowsRequest() with { Name = Static(workflow.Name) });
        Assert.Empty(listed.Items);
    }
}

public class Workflow_AddStep_AppearsInStepsList_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.ExampleCatalogAsync(Runner);
        var adminStep = await Setups.BuildAdminCreateProductStepAsync(Runner);

        await BuildAsync(new CreateWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await ExecuteAsync(new ListWorkflowsRequest());
        await ExecuteAsync(new OpenWorkflowDetailRequest());

        await Page.Locator("#workflow-detail").GetByRole(AriaRole.Button, new() { Name = "+ Add Step" }).ClickAsync();
        await Page.WaitForSelectorAsync("h3:text('Add Step')");

        await Page.Locator($"#step-picker-list button[onclick*='{adminStep.Id}']").ClickAsync();

        await Assertions.Expect(Page.Locator("#workflow-steps-list").GetByText(adminStep.StepName)).ToBeVisibleAsync();
    }
}

public class Workflow_RemoveStep_DisappearsFromList_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.ExampleCatalogAsync(Runner);
        var adminStep = await Setups.BuildAdminCreateProductStepAsync(Runner);
        await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new AppendStepCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await ExecuteAsync(new ListWorkflowsRequest());
        await ExecuteAsync(new OpenWorkflowDetailRequest());
        await Assertions.Expect(Page.Locator("#workflow-steps-list").GetByText(adminStep.StepName)).ToBeVisibleAsync();

        await Page.Locator("#workflow-steps-list").GetByText("Remove").ClickAsync();

        await Assertions.Expect(Page.Locator("#workflow-steps-list").GetByText(adminStep.StepName)).Not.ToBeVisibleAsync();
    }
}

public class Workflow_AddEqualAssertion_AppearsInList_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await ExecuteAsync(new ListWorkflowsRequest());
        await ExecuteAsync(new OpenWorkflowDetailRequest());

        await Page.Locator("#workflow-detail button[onclick*=\"showWorkflowTab('assertions')\"]").ClickAsync();
        await Assertions.Expect(Page.Locator("#wf-tab-assertions")).ToBeVisibleAsync();

        await Page.SelectOptionAsync("#assertion-type", "equal");
        await Page.FillAsync("#assertion-val1", "$step.field");
        await Page.FillAsync("#assertion-val2", "expected");
        await Page.Locator("#workflow-detail").GetByRole(AriaRole.Button, new() { Name = "Add" }).ClickAsync();

        // Wait for openWorkflowDetail to complete (it resets to steps tab), then switch to assertions
        await Assertions.Expect(Page.Locator("#wf-tab-steps")).ToBeVisibleAsync();
        await Page.Locator("#workflow-detail button[onclick*=\"showWorkflowTab('assertions')\"]").ClickAsync();

        await Assertions.Expect(Page.Locator("#workflow-assertions-list").GetByText("equal: $step.field == expected")).ToBeVisibleAsync();
    }
}

public class Workflow_AddNotEmptyAssertion_AppearsInList_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await ExecuteAsync(new ListWorkflowsRequest());
        await ExecuteAsync(new OpenWorkflowDetailRequest());

        await Page.Locator("#workflow-detail button[onclick*=\"showWorkflowTab('assertions')\"]").ClickAsync();
        await Assertions.Expect(Page.Locator("#wf-tab-assertions")).ToBeVisibleAsync();

        await Page.SelectOptionAsync("#assertion-type", "notEmpty");
        await Page.FillAsync("#assertion-val1", "$step");
        await Page.Locator("#workflow-detail").GetByRole(AriaRole.Button, new() { Name = "Add" }).ClickAsync();

        // Wait for openWorkflowDetail to complete (it resets to steps tab), then switch to assertions
        await Assertions.Expect(Page.Locator("#wf-tab-steps")).ToBeVisibleAsync();
        await Page.Locator("#workflow-detail button[onclick*=\"showWorkflowTab('assertions')\"]").ClickAsync();

        await Assertions.Expect(Page.Locator("#workflow-assertions-list").GetByText("notEmpty: $step")).ToBeVisibleAsync();
    }
}

public class Runs_List_ShowsCompletedRun_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.TwoStepWorkflowAsync(Runner);
        await ExecuteAsync(new RunWorkflowRequest());
        await PollAsync(new GetRunRequest(), r => r.Status == "completed", intervalMs: 200, timeoutMs: 15000);

        var runs = await ExecuteAsync(new ListRunsRequest() with { WorkflowName = Static(workflowName) });
        var run = Assert.Single(runs.Items);
        Assert.True(run.Passed);
    }
}

public class Catalog_CreateViaForm_AppearsInList_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        await ExecuteAsync(new ListCatalogsRequest());
        var created = await ExecuteAsync(new CreateCatalogViaFormRequest());

        await Assertions.Expect(Page.Locator("#catalog-list").GetByText(created.Name)).ToBeVisibleAsync();
    }
}

public class Catalog_Paging_PagerAppearsAfterPageSize_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        for (var i = 0; i < 11; i++)
        {
            await BuildAsync(new CreateCatalogCommand());
            await ExecuteAsync(new PostCatalogCommandsRequest());
        }

        await ExecuteAsync(new ListCatalogsRequest());
        var page2 = await ExecuteAsync(new NextCatalogsPageRequest());
        Assert.Equal(2, page2.CurrentPage);
    }
}

public class Catalog_OpenDetail_ShowsNamePrefilled_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());

        await ExecuteAsync(new ListCatalogsRequest());
        await ExecuteAsync(new OpenCatalogDetailRequest());

        var nameInput = await Page.InputValueAsync("#catalog-name-input");
        Assert.Equal(create.Name, nameInput);
    }
}

public class Catalog_Edit_SaveUpdatesTitle_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());
        var updatedName = $"updated-{Guid.NewGuid():N}";

        await ExecuteAsync(new ListCatalogsRequest());
        await ExecuteAsync(new OpenCatalogDetailRequest());

        await Page.FillAsync("#catalog-name-input", updatedName);
        await Page.Locator("#catalog-detail").GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        await Assertions.Expect(Page.Locator("#catalog-detail-title")).ToHaveTextAsync(updatedName);
    }
}

public class Catalog_Archive_DisappearsFromList_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());

        await ExecuteAsync(new ListCatalogsRequest());
        await ExecuteAsync(new OpenCatalogDetailRequest());
        await ExecuteAsync(new ArchiveCatalogViaUiRequest());

        var listed = await ExecuteAsync(new ListCatalogsRequest() with { Name = Static(create.Name) });
        Assert.Empty(listed.Items);
    }
}

public class Catalog_AddStep_AppearsInStepsList_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());

        var stepName = $"step-{Guid.NewGuid():N}";

        await ExecuteAsync(new ListCatalogsRequest());
        await ExecuteAsync(new OpenCatalogDetailRequest());

        await Page.GetByRole(AriaRole.Button, new() { Name = "+ Add Step" }).ClickAsync();
        await Assertions.Expect(Page.Locator("#step-form")).ToBeVisibleAsync();

        await Page.FillAsync("#step-form-name", stepName);
        await Page.FillAsync("#step-form-path", "/api/test");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save Step" }).ClickAsync();

        await Assertions.Expect(Page.Locator("#catalog-steps-list").GetByText(stepName)).ToBeVisibleAsync();
    }
}

public class Catalog_EditStep_FormPreFills_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        await BuildAsync(new CreateCatalogCommand());
        var step = await BuildAsync(new UpsertStepCommand());

        await ExecuteAsync(new PostCatalogCommandsRequest());
        await ExecuteAsync(new PostCatalogStepCommandsRequest());

        await ExecuteAsync(new ListCatalogsRequest());
        await ExecuteAsync(new OpenCatalogDetailRequest());
        await Assertions.Expect(Page.Locator("#catalog-steps-list").GetByText(step.StepName)).ToBeVisibleAsync();

        await Page.Locator("#catalog-steps-list").GetByText("Edit").ClickAsync();
        await Assertions.Expect(Page.Locator("#step-form")).ToBeVisibleAsync();

        var nameValue = await Page.InputValueAsync("#step-form-name");
        Assert.Equal(step.StepName, nameValue);
        var pathValue = await Page.InputValueAsync("#step-form-path");
        Assert.Equal(step.Path, pathValue);
    }
}

public class Catalog_ArchiveStep_BadgeAppearsAndDisappears_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());
        
        var step = await BuildAsync(new UpsertStepCommand());
        await ExecuteAsync(new PostCatalogStepCommandsRequest());

        await ExecuteAsync(new ListCatalogsRequest());
        await ExecuteAsync(new OpenCatalogDetailRequest());

        var stepRow = Page.Locator("#catalog-steps-list > div").Filter(new LocatorFilterOptions { HasText = step.StepName });
        await stepRow.GetByText("Archive").ClickAsync();

        await Assertions.Expect(stepRow.Locator("span.text-yellow-700")).ToBeVisibleAsync();

        await stepRow.GetByText("Unarchive").ClickAsync();
        await Assertions.Expect(stepRow.Locator("span.text-yellow-700")).Not.ToBeVisibleAsync();
    }
}

public class Workflow_QuickRun_ShowsResultBadge_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.TwoStepWorkflowAsync(Runner);

        await ExecuteAsync(new ListWorkflowsRequest() with { Name = Static(workflowName) });
        await ExecuteAsync(new QuickRunViaUiRequest());

        await Assertions.Expect(Page.Locator("#run-result-badge")).ToHaveTextAsync("PASS",
            new LocatorAssertionsToHaveTextOptions { Timeout = 15000 });
    }
}

public class Runs_ViewDetail_ShowsPassBadgeAndSteps_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.TwoStepWorkflowAsync(Runner);
        await ExecuteAsync(new RunWorkflowRequest());
        await PollAsync(new GetRunRequest(), r => r.Status == "completed", intervalMs: 200, timeoutMs: 15000);

        await ExecuteAsync(new ListRunsRequest());
        await ExecuteAsync(new OpenRunDetailRequest());

        await Assertions.Expect(Page.Locator("#run-detail").GetByText("PASS")).ToBeVisibleAsync();
        var stepDetails = Page.Locator("#run-detail details");
        await Assertions.Expect(stepDetails.First).ToBeVisibleAsync();
    }
}

public class Workflow_RunStats_PassedRun_ShowsCountAndRate_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.TwoStepWorkflowAsync(Runner);
        await ExecuteAsync(new RunWorkflowRequest());
        await PollAsync(new GetRunRequest(), r => r.Status == "completed", intervalMs: 200, timeoutMs: 15000);

        var listed = await ExecuteAsync(new ListWorkflowsRequest() with { Name = Static(workflowName) });
        var item = Assert.Single(listed.Items);
        Assert.Equal(1, item.RunCount);
        Assert.Equal("100%", item.PassRate);
    }
}

public class Workflow_RunStats_FailedRun_ShowsZeroPassRate_ViaUI : PlaywrightWithTargetTestBase
{
    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.RunFailedWorkflowAsync(Runner);
        await ExecuteAsync(new RunWorkflowRequest());
        await PollAsync(new GetRunRequest(), r => r.Status == "completed", intervalMs: 200, timeoutMs: 15000);

        var listed = await ExecuteAsync(new ListWorkflowsRequest() with { Name = Static(workflowName) });
        var item = Assert.Single(listed.Items);
        Assert.Equal(1, item.RunCount);
        Assert.Equal("0%", item.PassRate);
    }
}
