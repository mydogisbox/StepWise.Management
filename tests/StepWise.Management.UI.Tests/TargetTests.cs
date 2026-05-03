using Microsoft.Playwright;

namespace StepWise.Management.UI.Tests;

public class TargetTests : PlaywrightTestBase
{
    [Fact]
    public async Task CreateTarget_AppearsInList()
    {
        var name = $"playwright-target-{Guid.NewGuid():N}";

        await Page.GotoAsync($"{BaseUrl}/index.html");

        // Navigate to Targets
        await Page.GetByRole(AriaRole.Button, new() { Name = "Targets" }).ClickAsync();
        await Page.WaitForSelectorAsync("h2:text('Targets')");

        // Open new target modal
        await Page.GetByRole(AriaRole.Button, new() { Name = "+ New Target" }).ClickAsync();
        await Page.WaitForSelectorAsync("h3:text('New Target')");

        // Fill in the form
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Name" }).FillAsync(name);
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Base URL" }).FillAsync("http://localhost:9999");

        // Save
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        // Modal closes and new target appears in the list
        await Assertions.Expect(Page.Locator("h3:text('New Target')")).Not.ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText(name)).ToBeVisibleAsync();
    }
}
