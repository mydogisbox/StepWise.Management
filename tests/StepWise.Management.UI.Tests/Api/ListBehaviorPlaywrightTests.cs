using Microsoft.Playwright;
using Walkthrough.Core;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public class Workflow_34_Archive_ExcludedFromList_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.ArchivedWorkflowAsync(Runner);

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Workflows" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#workflow-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-workflows", async () =>
        {
            var content = await _pw.Page.InnerTextAsync("#workflow-list");
            return content.Contains(workflowName);
        });
        Assert.False(found, $"Archived workflow '{workflowName}' should not appear in the default list");
    }
}

public class Workflow_35_Archive_IncludedWhenShowArchived_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.ArchivedWorkflowAsync(Runner);

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Workflows" }).ClickAsync();
        await _pw.Page.CheckAsync("#workflows-show-archived");
        await _pw.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-workflows", async () =>
        {
            var content = await _pw.Page.InnerTextAsync("#workflow-list");
            return content.Contains(workflowName);
        });
        Assert.True(found, $"Workflow '{workflowName}' not found on any page");
        var archivedVisible = await _pw.Page.QuerySelectorAsync("#workflow-list span.text-yellow-700");
        Assert.NotNull(archivedVisible);
    }
}


public class Workflow_37_Paging_PagerAppearsAfterPageSize_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        for (var i = 0; i < 11; i++)
        {
            await BuildAsync(new CreateWorkflowCommand());
            await ExecuteAsync(new PostWorkflowCommandsRequest());
        }

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Workflows" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#workflow-list').textContent.trim().length > 0");
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#pager-workflows').textContent.trim().length > 0");

        var pagerText = await _pw.Page.InnerTextAsync("#pager-workflows");
        Assert.Contains("Page 1 of", pagerText);

        await _pw.Page.ClickAsync("#pager-workflows button:last-child");
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#pager-workflows').textContent.includes('Page 2 of')");

        var afterNavText = await _pw.Page.InnerTextAsync("#pager-workflows");
        Assert.Contains("Page 2 of", afterNavText);
    }
}


public class Runs_02_List_ShowsCompletedRun_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), _pw.Target, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.TwoStepWorkflowAsync(Runner);

        var listed = await ExecuteAsync(new ListWorkflowsRequest() with { Name = Static(workflowName) });
        Assert.Single(listed.Items);
        await ExecuteAsync(new RunWorkflowRequest());
        await PollAsync(new GetRunRequest(), r => r.Status == "completed", timeoutMs: 15000);

        var runs = await ExecuteAsync(new ListRunsRequest() with { WorkflowName = Static(workflowName) });
        var run = Assert.Single(runs.Items);
        Assert.True(run.Passed);
    }
}

public class Catalog_23_CreateViaForm_AppearsInList_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var name = $"playwright-catalog-{Guid.NewGuid():N}";

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Catalogs" }).ClickAsync();
        await _pw.Page.WaitForSelectorAsync("h2:text('Catalogs')");

        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "+ New Catalog" }).ClickAsync();
        await _pw.Page.WaitForSelectorAsync("h3:text('New Catalog')");

        await _pw.Page.FillAsync("#new-catalog-name", name);
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        await Assertions.Expect(_pw.Page.Locator("h3:text('New Catalog')")).Not.ToBeVisibleAsync();
        await Assertions.Expect(_pw.Page.Locator("#catalog-list").GetByText(name)).ToBeVisibleAsync();
    }
}

public class Catalog_25_OpenDetail_ShowsNamePrefilled_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Catalogs" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#catalog-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-catalogs", async () =>
        {
            var content = await _pw.Page.InnerTextAsync("#catalog-list");
            return content.Contains(create.Name);
        });
        Assert.True(found, $"Catalog '{create.Name}' not found on any page");

        await _pw.Page.Locator("#catalog-list").GetByText(create.Name).ClickAsync();

        await Assertions.Expect(_pw.Page.Locator("#catalog-detail")).ToBeVisibleAsync();
        var nameInput = await _pw.Page.InputValueAsync("#catalog-name-input");
        Assert.Equal(create.Name, nameInput);
    }
}

public class Catalog_24_Paging_PagerAppearsAfterPageSize_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        for (var i = 0; i < 11; i++)
        {
            await BuildAsync(new CreateCatalogCommand());
            await ExecuteAsync(new PostCatalogCommandsRequest());
        }

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Catalogs" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#catalog-list').textContent.trim().length > 0");
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#pager-catalogs').textContent.trim().length > 0");

        var pagerText = await _pw.Page.InnerTextAsync("#pager-catalogs");
        Assert.Contains("Page 1 of", pagerText);

        await _pw.Page.ClickAsync("#pager-catalogs button:last-child");
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#pager-catalogs').textContent.includes('Page 2 of')");

        var afterNavText = await _pw.Page.InnerTextAsync("#pager-catalogs");
        Assert.Contains("Page 2 of", afterNavText);
    }
}


public class Workflow_38_CreateViaForm_DetailOpens_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var name = $"playwright-workflow-{Guid.NewGuid():N}";

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Workflows" }).ClickAsync();
        await _pw.Page.WaitForSelectorAsync("h2:text('Workflows')");

        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "+ New Workflow" }).ClickAsync();
        await _pw.Page.WaitForSelectorAsync("h3:text('New Workflow')");

        await _pw.Page.FillAsync("#new-workflow-name", name);
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        await Assertions.Expect(_pw.Page.Locator("h3:text('New Workflow')")).Not.ToBeVisibleAsync();
        await Assertions.Expect(_pw.Page.Locator("#workflow-detail")).ToBeVisibleAsync();
        var nameInput = await _pw.Page.InputValueAsync("#workflow-name-input");
        Assert.Equal(name, nameInput);
    }
}


public class Catalog_26_Edit_SaveUpdatesTitle_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());
        var updatedName = $"updated-{Guid.NewGuid():N}";

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Catalogs" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#catalog-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-catalogs", async () =>
            (await _pw.Page.InnerTextAsync("#catalog-list")).Contains(create.Name));
        Assert.True(found);

        await _pw.Page.Locator("#catalog-list").GetByText(create.Name).ClickAsync();
        await Assertions.Expect(_pw.Page.Locator("#catalog-detail")).ToBeVisibleAsync();

        await _pw.Page.FillAsync("#catalog-name-input", updatedName);
        await _pw.Page.Locator("#catalog-detail").GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        await Assertions.Expect(_pw.Page.Locator("#catalog-detail-title")).ToHaveTextAsync(updatedName);
    }
}

public class Catalog_27_Archive_DisappearsFromList_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Catalogs" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#catalog-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-catalogs", async () =>
            (await _pw.Page.InnerTextAsync("#catalog-list")).Contains(create.Name));
        Assert.True(found);

        await _pw.Page.Locator("#catalog-list").GetByText(create.Name).ClickAsync();
        await Assertions.Expect(_pw.Page.Locator("#catalog-detail")).ToBeVisibleAsync();

        await _pw.Page.ClickAsync("#catalog-archive-btn");
        await Assertions.Expect(_pw.Page.Locator("#catalog-archive-btn")).ToHaveTextAsync("Unarchive");
        await _pw.Page.WaitForFunctionAsync(
            $"!document.querySelector('#catalog-list')?.innerText?.includes('{create.Name}')");
        await _pw.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var stillThere = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-catalogs", async () =>
            (await _pw.Page.InnerTextAsync("#catalog-list")).Contains(create.Name));
        Assert.False(stillThere, $"Archived catalog '{create.Name}' should not appear in the default list");
    }
}

public class Catalog_28_AddStep_AppearsInStepsList_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        var catalog = await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());

        var stepName = $"step-{Guid.NewGuid():N}";

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Catalogs" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#catalog-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-catalogs", async () =>
            (await _pw.Page.InnerTextAsync("#catalog-list")).Contains(catalog.Name));
        Assert.True(found);

        await _pw.Page.Locator("#catalog-list").GetByText(catalog.Name).ClickAsync();
        await Assertions.Expect(_pw.Page.Locator("#catalog-detail")).ToBeVisibleAsync();

        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "+ Add Step" }).ClickAsync();
        await Assertions.Expect(_pw.Page.Locator("#step-form")).ToBeVisibleAsync();

        await _pw.Page.FillAsync("#step-form-name", stepName);
        await _pw.Page.FillAsync("#step-form-path", "/api/test");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Save Step" }).ClickAsync();

        await Assertions.Expect(_pw.Page.Locator("#catalog-steps-list").GetByText(stepName)).ToBeVisibleAsync();
    }
}

public class Workflow_39_Archive_DisappearsFromList_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var workflow = await BuildAsync(new CreateWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Workflows" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#workflow-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-workflows", async () =>
            (await _pw.Page.InnerTextAsync("#workflow-list")).Contains(workflow.Name));
        Assert.True(found);

        var row = _pw.Page.Locator("#workflow-list tr").Filter(new LocatorFilterOptions { HasText = workflow.Name });
        await row.GetByText("Edit").ClickAsync();
        await Assertions.Expect(_pw.Page.Locator("#workflow-detail")).ToBeVisibleAsync();

        await _pw.Page.ClickAsync("#workflow-archive-btn");
        await Assertions.Expect(_pw.Page.Locator("#workflow-archive-btn")).ToHaveTextAsync("Unarchive");
        await _pw.Page.WaitForFunctionAsync(
            $"!document.querySelector('#workflow-list')?.innerText?.includes('{workflow.Name}')");
        await _pw.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var stillThere = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-workflows", async () =>
            (await _pw.Page.InnerTextAsync("#workflow-list")).Contains(workflow.Name));
        Assert.False(stillThere, $"Archived workflow '{workflow.Name}' should not appear in the default list");
    }
}

public class Workflow_40_AddStep_AppearsInStepsList_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var (adminStep, _) = await Setups.ExampleCatalogAsync(Runner);

        var workflow = await BuildAsync(new CreateWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Workflows" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#workflow-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-workflows", async () =>
            (await _pw.Page.InnerTextAsync("#workflow-list")).Contains(workflow.Name));
        Assert.True(found);

        var row = _pw.Page.Locator("#workflow-list tr").Filter(new LocatorFilterOptions { HasText = workflow.Name });
        await row.GetByText("Edit").ClickAsync();
        await Assertions.Expect(_pw.Page.Locator("#workflow-detail")).ToBeVisibleAsync();

        await _pw.Page.Locator("#workflow-detail").GetByRole(AriaRole.Button, new() { Name = "+ Add Step" }).ClickAsync();
        await _pw.Page.WaitForSelectorAsync("h3:text('Add Step')");

        await _pw.Page.Locator($"#step-picker-list button[onclick*='{adminStep.Id}']").ClickAsync();

        await Assertions.Expect(_pw.Page.Locator("#workflow-steps-list").GetByText(adminStep.StepName)).ToBeVisibleAsync();
    }
}

public class Workflow_41_RemoveStep_DisappearsFromList_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var (adminStep, _) = await Setups.ExampleCatalogAsync(Runner);
        var workflow = await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(adminStep.Id) });
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Workflows" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#workflow-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-workflows", async () =>
            (await _pw.Page.InnerTextAsync("#workflow-list")).Contains(workflow.Name));
        Assert.True(found);

        var row = _pw.Page.Locator("#workflow-list tr").Filter(new LocatorFilterOptions { HasText = workflow.Name });
        await row.GetByText("Edit").ClickAsync();
        await Assertions.Expect(_pw.Page.Locator("#workflow-detail")).ToBeVisibleAsync();
        await Assertions.Expect(_pw.Page.Locator("#workflow-steps-list").GetByText(adminStep.StepName)).ToBeVisibleAsync();

        await _pw.Page.Locator("#workflow-steps-list").GetByText("Remove").ClickAsync();

        await Assertions.Expect(_pw.Page.Locator("#workflow-steps-list").GetByText(adminStep.StepName)).Not.ToBeVisibleAsync();
    }
}


public class Catalog_29_EditStep_FormPreFills_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

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

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Catalogs" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#catalog-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-catalogs", async () =>
            (await _pw.Page.InnerTextAsync("#catalog-list")).Contains(catalog.Name));
        Assert.True(found);

        await _pw.Page.Locator("#catalog-list").GetByText(catalog.Name).ClickAsync();
        await Assertions.Expect(_pw.Page.Locator("#catalog-detail")).ToBeVisibleAsync();
        await Assertions.Expect(_pw.Page.Locator("#catalog-steps-list").GetByText(stepName)).ToBeVisibleAsync();

        await _pw.Page.Locator("#catalog-steps-list").GetByText("Edit").ClickAsync();
        await Assertions.Expect(_pw.Page.Locator("#step-form")).ToBeVisibleAsync();

        var nameValue = await _pw.Page.InputValueAsync("#step-form-name");
        Assert.Equal(stepName, nameValue);
        var pathValue = await _pw.Page.InputValueAsync("#step-form-path");
        Assert.Equal("/api/test", pathValue);
    }
}

public class Catalog_30_ArchiveStep_BadgeAppearsAndDisappears_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

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

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Catalogs" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#catalog-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-catalogs", async () =>
            (await _pw.Page.InnerTextAsync("#catalog-list")).Contains(catalog.Name));
        Assert.True(found);

        await _pw.Page.Locator("#catalog-list").GetByText(catalog.Name).ClickAsync();
        await Assertions.Expect(_pw.Page.Locator("#catalog-detail")).ToBeVisibleAsync();

        var stepRow = _pw.Page.Locator("#catalog-steps-list > div").Filter(new LocatorFilterOptions { HasText = stepName });
        await stepRow.GetByText("Archive").ClickAsync();

        await Assertions.Expect(stepRow.Locator("span.text-yellow-700")).ToBeVisibleAsync();

        await stepRow.GetByText("Unarchive").ClickAsync();
        await Assertions.Expect(stepRow.Locator("span.text-yellow-700")).Not.ToBeVisibleAsync();
    }
}

public class Workflow_42_AddEqualAssertion_AppearsInList_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var workflow = await BuildAsync(new CreateWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Workflows" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#workflow-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-workflows", async () =>
            (await _pw.Page.InnerTextAsync("#workflow-list")).Contains(workflow.Name));
        Assert.True(found);

        var row = _pw.Page.Locator("#workflow-list tr").Filter(new LocatorFilterOptions { HasText = workflow.Name });
        await row.GetByText("Edit").ClickAsync();
        await Assertions.Expect(_pw.Page.Locator("#workflow-detail")).ToBeVisibleAsync();

        await _pw.Page.Locator("#workflow-detail button[onclick*=\"showWorkflowTab('assertions')\"]").ClickAsync();
        await Assertions.Expect(_pw.Page.Locator("#wf-tab-assertions")).ToBeVisibleAsync();

        await _pw.Page.SelectOptionAsync("#assertion-type", "equal");
        await _pw.Page.FillAsync("#assertion-val1", "$step.field");
        await _pw.Page.FillAsync("#assertion-val2", "expected");
        await _pw.Page.Locator("#workflow-detail").GetByRole(AriaRole.Button, new() { Name = "Add" }).ClickAsync();

        // Wait for openWorkflowDetail to complete (it resets to steps tab), then switch to assertions
        await Assertions.Expect(_pw.Page.Locator("#wf-tab-steps")).ToBeVisibleAsync();
        await _pw.Page.Locator("#workflow-detail button[onclick*=\"showWorkflowTab('assertions')\"]").ClickAsync();

        await Assertions.Expect(_pw.Page.Locator("#workflow-assertions-list").GetByText("equal: $step.field == expected")).ToBeVisibleAsync();
    }
}

public class Workflow_43_AddNotEmptyAssertion_AppearsInList_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var workflow = await BuildAsync(new CreateWorkflowCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Workflows" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#workflow-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-workflows", async () =>
            (await _pw.Page.InnerTextAsync("#workflow-list")).Contains(workflow.Name));
        Assert.True(found);

        var row = _pw.Page.Locator("#workflow-list tr").Filter(new LocatorFilterOptions { HasText = workflow.Name });
        await row.GetByText("Edit").ClickAsync();
        await Assertions.Expect(_pw.Page.Locator("#workflow-detail")).ToBeVisibleAsync();

        await _pw.Page.Locator("#workflow-detail button[onclick*=\"showWorkflowTab('assertions')\"]").ClickAsync();
        await Assertions.Expect(_pw.Page.Locator("#wf-tab-assertions")).ToBeVisibleAsync();

        await _pw.Page.SelectOptionAsync("#assertion-type", "notEmpty");
        await _pw.Page.FillAsync("#assertion-val1", "$step");
        await _pw.Page.Locator("#workflow-detail").GetByRole(AriaRole.Button, new() { Name = "Add" }).ClickAsync();

        // Wait for openWorkflowDetail to complete (it resets to steps tab), then switch to assertions
        await Assertions.Expect(_pw.Page.Locator("#wf-tab-steps")).ToBeVisibleAsync();
        await _pw.Page.Locator("#workflow-detail button[onclick*=\"showWorkflowTab('assertions')\"]").ClickAsync();

        await Assertions.Expect(_pw.Page.Locator("#workflow-assertions-list").GetByText("notEmpty: $step")).ToBeVisibleAsync();
    }
}

public class Runs_03_ViewDetail_ShowsPassBadgeAndSteps_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.TwoStepWorkflowAsync(Runner);
        await ExecuteAsync(new RunWorkflowRequest());
        await PollAsync(new GetRunRequest(), r => r.Status == "completed", timeoutMs: 15000);

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Runs" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#runs-list').textContent.trim().length > 0");

        var row = _pw.Page.Locator("#runs-list tr").Filter(new LocatorFilterOptions { HasText = workflowName });
        await row.GetByText("View").ClickAsync();

        await Assertions.Expect(_pw.Page.Locator("#run-detail")).ToBeVisibleAsync();
        await Assertions.Expect(_pw.Page.Locator("#run-detail").GetByText("PASS")).ToBeVisibleAsync();

        var stepDetails = _pw.Page.Locator("#run-detail details");
        await Assertions.Expect(stepDetails.First).ToBeVisibleAsync();
    }
}

public class Workflow_44_RunStats_PassedRun_ShowsCountAndRate_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.TwoStepWorkflowAsync(Runner);
        await ExecuteAsync(new RunWorkflowRequest());
        await PollAsync(new GetRunRequest(), r => r.Status == "completed", timeoutMs: 15000);

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Workflows" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#workflow-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-workflows", async () =>
            (await _pw.Page.InnerTextAsync("#workflow-list")).Contains(workflowName));
        Assert.True(found);

        var row = _pw.Page.Locator("#workflow-list tr").Filter(new LocatorFilterOptions { HasText = workflowName });
        await Assertions.Expect(row.Locator("td:nth-child(3)")).ToHaveTextAsync("1");
        await Assertions.Expect(row.Locator("td:nth-child(4)")).ToHaveTextAsync("100%");
    }
}

public class Workflow_45_RunStats_FailedRun_ShowsZeroPassRate_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.RunFailedWorkflowAsync(Runner);
        await ExecuteAsync(new RunWorkflowRequest());
        await PollAsync(new GetRunRequest(), r => r.Status != "pending", timeoutMs: 15000);

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Workflows" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#workflow-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-workflows", async () =>
            (await _pw.Page.InnerTextAsync("#workflow-list")).Contains(workflowName));
        Assert.True(found);

        var row = _pw.Page.Locator("#workflow-list tr").Filter(new LocatorFilterOptions { HasText = workflowName });
        await Assertions.Expect(row.Locator("td:nth-child(3)")).ToHaveTextAsync("1");
        await Assertions.Expect(row.Locator("td:nth-child(4)")).ToHaveTextAsync("0%");
    }
}
