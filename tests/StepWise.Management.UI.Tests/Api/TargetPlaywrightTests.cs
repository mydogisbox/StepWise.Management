using Microsoft.Playwright;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

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

        await Assertions.Expect(_pw.Page.Locator("h3:text('New Target')")).Not.ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 15000 });
        await Assertions.Expect(_pw.Page.GetByText(name)).ToBeVisibleAsync();
    }
}

public class Target_12_Edit_UpdatesInList_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var target = await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());
        var updatedName = $"updated-{Guid.NewGuid():N}";

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Targets" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#target-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-targets", async () =>
        {
            var content = await _pw.Page.InnerTextAsync("#target-list");
            return content.Contains(target.Name);
        });
        Assert.True(found, $"Target '{target.Name}' not found on any page");

        var row = _pw.Page.Locator("#target-list tr").Filter(new LocatorFilterOptions { HasText = target.Name });
        await row.GetByText("Edit").ClickAsync();
        await _pw.Page.WaitForSelectorAsync("h3:text('Edit Target')");

        await _pw.Page.FillAsync("#target-modal-name", updatedName);
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        await Assertions.Expect(_pw.Page.Locator("#target-list").GetByText(updatedName)).ToBeVisibleAsync();
    }
}

public class Target_13_Archive_BadgeAppearsWhenShowArchivedOn_ViaUI : ManagementTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    [Fact]
    public async Task Test()
    {
        var target = await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        await _pw.Page.GotoAsync("http://localhost:5020/index.html");
        await _pw.Page.GetByRole(AriaRole.Button, new() { Name = "Targets" }).ClickAsync();
        await _pw.Page.WaitForFunctionAsync("document.querySelector('#target-list').textContent.trim().length > 0");

        var found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-targets", async () =>
            (await _pw.Page.InnerTextAsync("#target-list")).Contains(target.Name));
        Assert.True(found, $"Target '{target.Name}' not found");

        var row = _pw.Page.Locator("#target-list tr").Filter(new LocatorFilterOptions { HasText = target.Name });
        await row.GetByText("Archive").ClickAsync();

        // After archiving, target disappears from the default list
        await _pw.Page.WaitForFunctionAsync(
            $"!document.querySelector('#target-list')?.innerText?.includes('{target.Name}')");

        // With Show Archived on, target reappears with badge
        await _pw.Page.CheckAsync("#targets-show-archived");
        await _pw.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-targets", async () =>
            (await _pw.Page.InnerTextAsync("#target-list")).Contains(target.Name));

        var archivedRow = _pw.Page.Locator("#target-list tr").Filter(new LocatorFilterOptions { HasText = target.Name });
        await Assertions.Expect(archivedRow.Locator("span.text-yellow-700")).ToBeVisibleAsync();

        await archivedRow.GetByText("Unarchive").ClickAsync();
        await Assertions.Expect(archivedRow.Locator("span.text-yellow-700")).Not.ToBeVisibleAsync();
    }
}

public class Target_14_ShowArchived_TogglesArchivedRows_ViaUI : ManagementTestBase, IAsyncLifetime
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
            (await _pw.Page.InnerTextAsync("#target-list")).Contains(targetName));
        Assert.False(found, "Archived target should not appear before Show Archived is checked");

        await _pw.Page.CheckAsync("#targets-show-archived");
        await _pw.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        found = await PagedUiHelper.NavigateToPageWhereAsync(_pw.Page, "pager-targets", async () =>
            (await _pw.Page.InnerTextAsync("#target-list")).Contains(targetName));
        Assert.True(found, "Archived target should appear after Show Archived is checked");
    }
}
