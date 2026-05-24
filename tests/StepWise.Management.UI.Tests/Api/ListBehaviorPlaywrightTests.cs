using Microsoft.Playwright;
using Walkthrough.Core;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public class Workflow_Archive_ExcludedFromList_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.ArchivedWorkflowAsync(Runner);

        await UiHelper.NavigateToListAsync(Page, "Workflows", "#workflow-list");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-workflows", async () =>
        {
            var content = await Page.InnerTextAsync("#workflow-list");
            return content.Contains(workflowName);
        });
        Assert.False(found, $"Archived workflow '{workflowName}' should not appear in the default list");
    }
}

public class Workflow_Archive_IncludedWhenShowArchived_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.ArchivedWorkflowAsync(Runner);

        await Page.GotoAsync("http://localhost:5020/index.html");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Workflows" }).ClickAsync();
        await Page.CheckAsync("#workflows-show-archived");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var found = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-workflows", async () =>
        {
            var content = await Page.InnerTextAsync("#workflow-list");
            return content.Contains(workflowName);
        });
        Assert.True(found, $"Workflow '{workflowName}' not found on any page");
        var archivedVisible = await Page.QuerySelectorAsync("#workflow-list span.text-yellow-700");
        Assert.NotNull(archivedVisible);
    }
}

public class Workflow_Paging_PagerAppearsAfterPageSize_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        for (var i = 0; i < 11; i++)
        {
            await BuildAsync(new CreateWorkflowCommand());
            await ExecuteAsync(new PostWorkflowCommandsRequest());
        }

        await UiHelper.NavigateToListAsync(Page, "Workflows", "#workflow-list");
        await Page.WaitForFunctionAsync("document.querySelector('#pager-workflows').textContent.trim().length > 0");

        var pagerText = await Page.InnerTextAsync("#pager-workflows");
        Assert.Contains("Page 1 of", pagerText);

        await Page.ClickAsync("#pager-workflows button:last-child");
        await Page.WaitForFunctionAsync("document.querySelector('#pager-workflows').textContent.includes('Page 2 of')");

        var afterNavText = await Page.InnerTextAsync("#pager-workflows");
        Assert.Contains("Page 2 of", afterNavText);
    }
}

public class Workflow_CreateViaForm_DetailOpens_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var name = $"playwright-workflow-{Guid.NewGuid():N}";

        await UiHelper.NavigateToSectionAsync(Page, "Workflows", "Workflows");

        await Page.GetByRole(AriaRole.Button, new() { Name = "+ New Workflow" }).ClickAsync();
        await Page.WaitForSelectorAsync("h3:text('New Workflow')");

        await Page.FillAsync("#new-workflow-name", name);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        await Assertions.Expect(Page.Locator("h3:text('New Workflow')")).Not.ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("#workflow-detail")).ToBeVisibleAsync();
        var nameInput = await Page.InputValueAsync("#workflow-name-input");
        Assert.Equal(name, nameInput);
    }
}

public class Workflow_Archive_DisappearsFromList_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var workflow = await BuildAsync(new CreateWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await UiHelper.NavigateToListAsync(Page, "Workflows", "#workflow-list");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-workflows", async () =>
            (await Page.InnerTextAsync("#workflow-list")).Contains(workflow.Name));
        Assert.True(found);

        var row = Page.Locator("#workflow-list tr").Filter(new LocatorFilterOptions { HasText = workflow.Name });
        await row.GetByText("Edit").ClickAsync();
        await Assertions.Expect(Page.Locator("#workflow-detail")).ToBeVisibleAsync();

        await Page.ClickAsync("#workflow-archive-btn");
        await Assertions.Expect(Page.Locator("#workflow-archive-btn")).ToHaveTextAsync("Unarchive");
        await Page.WaitForFunctionAsync(
            $"!document.querySelector('#workflow-list')?.innerText?.includes('{workflow.Name}')");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var stillThere = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-workflows", async () =>
            (await Page.InnerTextAsync("#workflow-list")).Contains(workflow.Name));
        Assert.False(stillThere, $"Archived workflow '{workflow.Name}' should not appear in the default list");
    }
}

public class Workflow_AddStep_AppearsInStepsList_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var (adminStep, _) = await Setups.ExampleCatalogAsync(Runner);

        var workflow = await BuildAsync(new CreateWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await UiHelper.NavigateToListAsync(Page, "Workflows", "#workflow-list");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-workflows", async () =>
            (await Page.InnerTextAsync("#workflow-list")).Contains(workflow.Name));
        Assert.True(found);

        var row = Page.Locator("#workflow-list tr").Filter(new LocatorFilterOptions { HasText = workflow.Name });
        await row.GetByText("Edit").ClickAsync();
        await Assertions.Expect(Page.Locator("#workflow-detail")).ToBeVisibleAsync();

        await Page.Locator("#workflow-detail").GetByRole(AriaRole.Button, new() { Name = "+ Add Step" }).ClickAsync();
        await Page.WaitForSelectorAsync("h3:text('Add Step')");

        await Page.Locator($"#step-picker-list button[onclick*='{adminStep.Id}']").ClickAsync();

        await Assertions.Expect(Page.Locator("#workflow-steps-list").GetByText(adminStep.StepName)).ToBeVisibleAsync();
    }
}

public class Workflow_RemoveStep_DisappearsFromList_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var (adminStep, _) = await Setups.ExampleCatalogAsync(Runner);
        var workflow = await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(adminStep.Id) });
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await UiHelper.NavigateToListAsync(Page, "Workflows", "#workflow-list");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-workflows", async () =>
            (await Page.InnerTextAsync("#workflow-list")).Contains(workflow.Name));
        Assert.True(found);

        var row = Page.Locator("#workflow-list tr").Filter(new LocatorFilterOptions { HasText = workflow.Name });
        await row.GetByText("Edit").ClickAsync();
        await Assertions.Expect(Page.Locator("#workflow-detail")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("#workflow-steps-list").GetByText(adminStep.StepName)).ToBeVisibleAsync();

        await Page.Locator("#workflow-steps-list").GetByText("Remove").ClickAsync();

        await Assertions.Expect(Page.Locator("#workflow-steps-list").GetByText(adminStep.StepName)).Not.ToBeVisibleAsync();
    }
}

public class Workflow_AddEqualAssertion_AppearsInList_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var workflow = await BuildAsync(new CreateWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await UiHelper.NavigateToListAsync(Page, "Workflows", "#workflow-list");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-workflows", async () =>
            (await Page.InnerTextAsync("#workflow-list")).Contains(workflow.Name));
        Assert.True(found);

        var row = Page.Locator("#workflow-list tr").Filter(new LocatorFilterOptions { HasText = workflow.Name });
        await row.GetByText("Edit").ClickAsync();
        await Assertions.Expect(Page.Locator("#workflow-detail")).ToBeVisibleAsync();

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

public class Workflow_AddNotEmptyAssertion_AppearsInList_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var workflow = await BuildAsync(new CreateWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await UiHelper.NavigateToListAsync(Page, "Workflows", "#workflow-list");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-workflows", async () =>
            (await Page.InnerTextAsync("#workflow-list")).Contains(workflow.Name));
        Assert.True(found);

        var row = Page.Locator("#workflow-list tr").Filter(new LocatorFilterOptions { HasText = workflow.Name });
        await row.GetByText("Edit").ClickAsync();
        await Assertions.Expect(Page.Locator("#workflow-detail")).ToBeVisibleAsync();

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

        var listed = await ExecuteAsync(new ListWorkflowsRequest() with { Name = Static(workflowName) });
        Assert.Single(listed.Items);
        await ExecuteAsync(new RunWorkflowRequest());
        await PollAsync(new GetRunRequest(), r => r.Status == "completed", intervalMs: 500, timeoutMs: 15000);

        var runs = await ExecuteAsync(new ListRunsRequest() with { WorkflowName = Static(workflowName) });
        var run = Assert.Single(runs.Items);
        Assert.True(run.Passed);
    }
}

public class Catalog_CreateViaForm_AppearsInList_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var name = $"playwright-catalog-{Guid.NewGuid():N}";

        await UiHelper.NavigateToSectionAsync(Page, "Catalogs", "Catalogs");

        await Page.GetByRole(AriaRole.Button, new() { Name = "+ New Catalog" }).ClickAsync();
        await Page.WaitForSelectorAsync("h3:text('New Catalog')");

        await Page.FillAsync("#new-catalog-name", name);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        await Assertions.Expect(Page.Locator("h3:text('New Catalog')")).Not.ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("#catalog-list").GetByText(name)).ToBeVisibleAsync();
    }
}

public class Catalog_Paging_PagerAppearsAfterPageSize_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        for (var i = 0; i < 11; i++)
        {
            await BuildAsync(new CreateCatalogCommand());
            await ExecuteAsync(new PostCatalogCommandsRequest());
        }

        await UiHelper.NavigateToListAsync(Page, "Catalogs", "#catalog-list");
        await Page.WaitForFunctionAsync("document.querySelector('#pager-catalogs').textContent.trim().length > 0");

        var pagerText = await Page.InnerTextAsync("#pager-catalogs");
        Assert.Contains("Page 1 of", pagerText);

        await Page.ClickAsync("#pager-catalogs button:last-child");
        await Page.WaitForFunctionAsync("document.querySelector('#pager-catalogs').textContent.includes('Page 2 of')");

        var afterNavText = await Page.InnerTextAsync("#pager-catalogs");
        Assert.Contains("Page 2 of", afterNavText);
    }
}

public class Catalog_OpenDetail_ShowsNamePrefilled_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());

        await UiHelper.NavigateToListAsync(Page, "Catalogs", "#catalog-list");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-catalogs", async () =>
        {
            var content = await Page.InnerTextAsync("#catalog-list");
            return content.Contains(create.Name);
        });
        Assert.True(found, $"Catalog '{create.Name}' not found on any page");

        await Page.Locator("#catalog-list").GetByText(create.Name).ClickAsync();

        await Assertions.Expect(Page.Locator("#catalog-detail")).ToBeVisibleAsync();
        var nameInput = await Page.InputValueAsync("#catalog-name-input");
        Assert.Equal(create.Name, nameInput);
    }
}

public class Catalog_Edit_SaveUpdatesTitle_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());
        var updatedName = $"updated-{Guid.NewGuid():N}";

        await UiHelper.NavigateToListAsync(Page, "Catalogs", "#catalog-list");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-catalogs", async () =>
            (await Page.InnerTextAsync("#catalog-list")).Contains(create.Name));
        Assert.True(found);

        await Page.Locator("#catalog-list").GetByText(create.Name).ClickAsync();
        await Assertions.Expect(Page.Locator("#catalog-detail")).ToBeVisibleAsync();

        await Page.FillAsync("#catalog-name-input", updatedName);
        await Page.Locator("#catalog-detail").GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        await Assertions.Expect(Page.Locator("#catalog-detail-title")).ToHaveTextAsync(updatedName);
    }
}

public class Catalog_Archive_DisappearsFromList_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());

        await UiHelper.NavigateToListAsync(Page, "Catalogs", "#catalog-list");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-catalogs", async () =>
            (await Page.InnerTextAsync("#catalog-list")).Contains(create.Name));
        Assert.True(found);

        await Page.Locator("#catalog-list").GetByText(create.Name).ClickAsync();
        await Assertions.Expect(Page.Locator("#catalog-detail")).ToBeVisibleAsync();

        await Page.ClickAsync("#catalog-archive-btn");
        await Assertions.Expect(Page.Locator("#catalog-archive-btn")).ToHaveTextAsync("Unarchive");
        await Page.WaitForFunctionAsync(
            $"!document.querySelector('#catalog-list')?.innerText?.includes('{create.Name}')");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var stillThere = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-catalogs", async () =>
            (await Page.InnerTextAsync("#catalog-list")).Contains(create.Name));
        Assert.False(stillThere, $"Archived catalog '{create.Name}' should not appear in the default list");
    }
}

public class Catalog_AddStep_AppearsInStepsList_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        var catalog = await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());

        var stepName = $"step-{Guid.NewGuid():N}";

        await UiHelper.NavigateToListAsync(Page, "Catalogs", "#catalog-list");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-catalogs", async () =>
            (await Page.InnerTextAsync("#catalog-list")).Contains(catalog.Name));
        Assert.True(found);

        await Page.Locator("#catalog-list").GetByText(catalog.Name).ClickAsync();
        await Assertions.Expect(Page.Locator("#catalog-detail")).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "+ Add Step" }).ClickAsync();
        await Assertions.Expect(Page.Locator("#step-form")).ToBeVisibleAsync();

        await Page.FillAsync("#step-form-name", stepName);
        await Page.FillAsync("#step-form-path", "/api/test");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save Step" }).ClickAsync();

        await Assertions.Expect(Page.Locator("#catalog-steps-list").GetByText(stepName)).ToBeVisibleAsync();
    }
}

public class Catalog_EditStep_FormPreFills_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var target = await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());
        await ExecuteAsync(new ListTargetsRequest() with { Name = Static(target.Name) });

        var catalog = await BuildAsync(new CreateCatalogCommand());
        var stepName = $"step-{Guid.NewGuid():N}";
        await BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static(stepName),
            Method   = Static("POST"),
            Path     = Static("/api/test")
        });
        await ExecuteAsync(new PostCatalogCommandsRequest());
        await ExecuteAsync(new PostCatalogStepCommandsRequest());

        await UiHelper.NavigateToListAsync(Page, "Catalogs", "#catalog-list");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-catalogs", async () =>
            (await Page.InnerTextAsync("#catalog-list")).Contains(catalog.Name));
        Assert.True(found);

        await Page.Locator("#catalog-list").GetByText(catalog.Name).ClickAsync();
        await Assertions.Expect(Page.Locator("#catalog-detail")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("#catalog-steps-list").GetByText(stepName)).ToBeVisibleAsync();

        await Page.Locator("#catalog-steps-list").GetByText("Edit").ClickAsync();
        await Assertions.Expect(Page.Locator("#step-form")).ToBeVisibleAsync();

        var nameValue = await Page.InputValueAsync("#step-form-name");
        Assert.Equal(stepName, nameValue);
        var pathValue = await Page.InputValueAsync("#step-form-path");
        Assert.Equal("/api/test", pathValue);
    }
}

public class Catalog_ArchiveStep_BadgeAppearsAndDisappears_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var target = await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());
        await ExecuteAsync(new ListTargetsRequest() with { Name = Static(target.Name) });

        var catalog = await BuildAsync(new CreateCatalogCommand());
        var stepName = $"step-{Guid.NewGuid():N}";
        await BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static(stepName),
            Path     = Static("/api/test")
        });
        await ExecuteAsync(new PostCatalogCommandsRequest());
        await ExecuteAsync(new PostCatalogStepCommandsRequest());

        await UiHelper.NavigateToListAsync(Page, "Catalogs", "#catalog-list");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-catalogs", async () =>
            (await Page.InnerTextAsync("#catalog-list")).Contains(catalog.Name));
        Assert.True(found);

        await Page.Locator("#catalog-list").GetByText(catalog.Name).ClickAsync();
        await Assertions.Expect(Page.Locator("#catalog-detail")).ToBeVisibleAsync();

        var stepRow = Page.Locator("#catalog-steps-list > div").Filter(new LocatorFilterOptions { HasText = stepName });
        await stepRow.GetByText("Archive").ClickAsync();

        await Assertions.Expect(stepRow.Locator("span.text-yellow-700")).ToBeVisibleAsync();

        await stepRow.GetByText("Unarchive").ClickAsync();
        await Assertions.Expect(stepRow.Locator("span.text-yellow-700")).Not.ToBeVisibleAsync();
    }
}

public class Runs_ViewDetail_ShowsPassBadgeAndSteps_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.TwoStepWorkflowAsync(Runner);
        await ExecuteAsync(new RunWorkflowRequest());
        await PollAsync(new GetRunRequest(), r => r.Status == "completed", intervalMs: 500, timeoutMs: 15000);

        await UiHelper.NavigateToListAsync(Page, "Runs", "#runs-list");

        var row = Page.Locator("#runs-list tr").Filter(new LocatorFilterOptions { HasText = workflowName });
        await row.GetByText("View").ClickAsync();

        await Assertions.Expect(Page.Locator("#run-detail")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("#run-detail").GetByText("PASS")).ToBeVisibleAsync();

        var stepDetails = Page.Locator("#run-detail details");
        await Assertions.Expect(stepDetails.First).ToBeVisibleAsync();
    }
}

public class Workflow_RunStats_PassedRun_ShowsCountAndRate_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.TwoStepWorkflowAsync(Runner);
        await ExecuteAsync(new RunWorkflowRequest());
        await PollAsync(new GetRunRequest(), r => r.Status == "completed", intervalMs: 500, timeoutMs: 15000);

        await UiHelper.NavigateToListAsync(Page, "Workflows", "#workflow-list");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-workflows", async () =>
            (await Page.InnerTextAsync("#workflow-list")).Contains(workflowName));
        Assert.True(found);

        var row = Page.Locator("#workflow-list tr").Filter(new LocatorFilterOptions { HasText = workflowName });
        await Assertions.Expect(row.Locator("td:nth-child(3)")).ToHaveTextAsync("1");
        await Assertions.Expect(row.Locator("td:nth-child(4)")).ToHaveTextAsync("100%");
    }
}

public class Workflow_RunStats_FailedRun_ShowsZeroPassRate_ViaUI : PlaywrightTestBase
{


    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.RunFailedWorkflowAsync(Runner);
        await ExecuteAsync(new RunWorkflowRequest());
        await PollAsync(new GetRunRequest(), r => r.Status != "pending", intervalMs: 500, timeoutMs: 15000);

        await UiHelper.NavigateToListAsync(Page, "Workflows", "#workflow-list");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(Page, "pager-workflows", async () =>
            (await Page.InnerTextAsync("#workflow-list")).Contains(workflowName));
        Assert.True(found);

        var row = Page.Locator("#workflow-list tr").Filter(new LocatorFilterOptions { HasText = workflowName });
        await Assertions.Expect(row.Locator("td:nth-child(3)")).ToHaveTextAsync("1");
        await Assertions.Expect(row.Locator("td:nth-child(4)")).ToHaveTextAsync("0%");
    }
}
