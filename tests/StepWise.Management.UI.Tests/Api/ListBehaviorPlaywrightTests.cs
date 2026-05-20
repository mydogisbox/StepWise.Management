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
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#workflow-list').textContent.trim().length > 0");

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

public class Target_07_Archive_ExcludedFromList_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var targetName = await Setups.ArchivedTargetAsync(Runner);

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Targets" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#target-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-targets", async () =>
        {
            var content = await _pw.Page.InnerTextAsync("#target-list");
            return content.Contains(targetName);
        });
        Assert.False(found, $"Archived target '{targetName}' should not appear in the default list");
    }
}

public class Target_08_Create_HasCreatedAt_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        await Setups.CreatedTargetAsync(Runner);

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Targets" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#target-list').textContent.trim().length > 0");

        var firstRow = _pw.Page.Locator("#target-list table tbody tr").First;
        var createdAt = (await firstRow.Locator("td:nth-child(3)").InnerTextAsync()).Trim();
        Assert.NotEqual("—", createdAt);
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

public class Target_10_Paging_PagerAppearsAfterPageSize_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        for (var i = 0; i < 11; i++)
        {
            await BuildAsync(new CreateTargetCommand());
            await ExecuteAsync(new PostTargetCommandsRequest());
        }

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Targets" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#target-list').textContent.trim().length > 0");
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#pager-targets').textContent.trim().length > 0");

        var pagerText = await _pw.Page.InnerTextAsync("#pager-targets");
        Assert.Contains("Page 1 of", pagerText);

        await _pw.Page.ClickAsync("#pager-targets button:last-child");
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#pager-targets').textContent.includes('Page 2 of')");

        var afterNavText = await _pw.Page.InnerTextAsync("#pager-targets");
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

public class Target_11_CreateViaForm_AppearsInList_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var name = $"playwright-target-{Guid.NewGuid():N}";

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Targets" }).ClickAsync();
        await _pw.Page.WaitForSelectorAsync("h2:text('Targets')");

        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "+ New Target" }).ClickAsync();
        await _pw.Page.WaitForSelectorAsync("h3:text('New Target')");

        await _pw.Page.GetByRole(AriaRole.Textbox, new() { Name = "Name" }).FillAsync(name);
        await _pw.Page.GetByRole(AriaRole.Textbox, new() { Name = "Base URL" }).FillAsync("http://localhost:9999");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        await Assertions.Expect(_pw.Page.Locator("h3:text('New Target')")).Not.ToBeVisibleAsync();
        await Assertions.Expect(_pw.Page.GetByText(name)).ToBeVisibleAsync();
    }
}
