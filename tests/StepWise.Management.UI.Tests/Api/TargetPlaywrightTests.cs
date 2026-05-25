using Microsoft.Playwright;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public class Target_Archive_ExcludedFromList_ViaUI : PlaywrightTestBase
{
    [Fact]
    public async Task Test()
    {
        var targetName = await Setups.ArchivedTargetAsync(Runner);

        await UiHelper.NavigateAndFilterAsync(Page, "Targets", "targets-name-filter", targetName);

        Assert.DoesNotContain(targetName, await Page.InnerTextAsync("#target-list"));
    }
}

public class Target_Create_HasCreatedAt_ViaUI : PlaywrightTestBase
{

    [Fact]
    public async Task Test()
    {
        await Setups.CreatedTargetAsync(Runner);

        await UiHelper.NavigateToListAsync(Page, "Targets", "#target-list");

        var firstRow = Page.Locator("#target-list table tbody tr").First;
        var createdAt = (await firstRow.Locator("td:nth-child(3)").InnerTextAsync()).Trim();
        Assert.NotEqual("—", createdAt);
    }
}

public class Target_Paging_PagerAppearsAfterPageSize_ViaUI : PlaywrightTestBase
{

    [Fact]
    public async Task Test()
    {
        for (var i = 0; i < 11; i++)
        {
            await BuildAsync(new CreateTargetCommand());
            await ExecuteAsync(new PostTargetCommandsRequest());
        }

        await UiHelper.NavigateToListAsync(Page, "Targets", "#target-list");
        await Page.WaitForFunctionAsync("document.querySelector('#pager-targets').textContent.trim().length > 0");

        var pagerText = await Page.InnerTextAsync("#pager-targets");
        Assert.Contains("Page 1 of", pagerText);

        await Page.ClickAsync("#pager-targets button:last-child");
        await Page.WaitForFunctionAsync("document.querySelector('#pager-targets').textContent.includes('Page 2 of')");

        var afterNavText = await Page.InnerTextAsync("#pager-targets");
        Assert.Contains("Page 2 of", afterNavText);
    }
}

public class Target_CreateViaForm_AppearsInList_ViaUI : PlaywrightTestBase
{

    [Fact]
    public async Task Test()
    {
        var name = $"playwright-target-{Guid.NewGuid():N}";

        await UiHelper.NavigateToSectionAsync(Page, "Targets", "Targets");

        await Page.GetByRole(AriaRole.Button, new() { Name = "+ New Target" }).ClickAsync();
        await Page.WaitForSelectorAsync("h3:text('New Target')");

        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Name" }).FillAsync(name);
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Base URL" }).FillAsync("http://localhost:9999");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        await Assertions.Expect(Page.Locator("h3:text('New Target')")).Not.ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 15000 });
        await Assertions.Expect(Page.GetByText(name)).ToBeVisibleAsync();
    }
}

public class Target_Edit_UpdatesInList_ViaUI : PlaywrightTestBase
{

    [Fact]
    public async Task Test()
    {
        var target = await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());
        var updatedName = $"updated-{Guid.NewGuid():N}";

        await UiHelper.NavigateAndFilterAsync(Page, "Targets", "targets-name-filter", target.Name);

        var row = Page.Locator("#target-list tr").Filter(new LocatorFilterOptions { HasText = target.Name });
        await row.GetByText("Edit").ClickAsync();
        await Page.WaitForSelectorAsync("h3:text('Edit Target')");

        await Page.FillAsync("#target-modal-name", updatedName);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        await Assertions.Expect(Page.Locator("h3:text('Edit Target')")).Not.ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 15000 });
        var filterDone = Page.WaitForResponseAsync(r => r.Url.Contains("name=") && r.Request.Method == "GET");
        await Page.FillAsync("#targets-name-filter", updatedName);
        await filterDone;
        await Assertions.Expect(Page.Locator("#target-list").GetByText(updatedName)).ToBeVisibleAsync();
    }
}

public class Target_Archive_BadgeAppearsWhenShowArchivedOn_ViaUI : PlaywrightTestBase
{

    [Fact]
    public async Task Test()
    {
        var target = await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        await UiHelper.NavigateAndFilterAsync(Page, "Targets", "targets-name-filter", target.Name);

        var row = Page.Locator("#target-list tr").Filter(new LocatorFilterOptions { HasText = target.Name });
        await row.GetByText("Archive").ClickAsync();

        await Page.WaitForFunctionAsync(
            $"!document.querySelector('#target-list')?.innerText?.includes('{target.Name}')");

        await Page.CheckAsync("#targets-show-archived");
        await Assertions.Expect(Page.Locator("#target-list").GetByText(target.Name)).ToBeVisibleAsync();

        var archivedRow = Page.Locator("#target-list tr").Filter(new LocatorFilterOptions { HasText = target.Name });
        await Assertions.Expect(archivedRow.Locator("span.text-yellow-700")).ToBeVisibleAsync();

        await archivedRow.GetByText("Unarchive").ClickAsync();
        await Assertions.Expect(archivedRow.Locator("span.text-yellow-700")).Not.ToBeVisibleAsync();
    }
}

public class Target_ShowArchived_TogglesArchivedRows_ViaUI : PlaywrightTestBase
{
    [Fact]
    public async Task Test()
    {
        var targetName = await Setups.ArchivedTargetAsync(Runner);

        await UiHelper.NavigateAndFilterAsync(Page, "Targets", "targets-name-filter", targetName);
        Assert.DoesNotContain(targetName, await Page.InnerTextAsync("#target-list"));

        await Page.CheckAsync("#targets-show-archived");
        await Assertions.Expect(Page.Locator("#target-list").GetByText(targetName)).ToBeVisibleAsync();
    }
}
