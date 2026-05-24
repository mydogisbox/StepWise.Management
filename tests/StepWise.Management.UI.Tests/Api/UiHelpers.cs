using Microsoft.Playwright;

namespace StepWise.Management.UI.Tests.Api;

internal static class UiHelper
{
    internal static async Task NavigateToListAsync(IPage page, string sectionButton, string listSelector)
    {
        await page.GotoAsync("http://localhost:5020/index.html");
        await page.GetByRole(AriaRole.Button, new() { Name = sectionButton }).ClickAsync();
        await page.WaitForFunctionAsync(
            $"document.querySelector('{listSelector}').textContent.trim().length > 0",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });
    }

    internal static async Task NavigateToSectionAsync(IPage page, string sectionButton, string headingText)
    {
        await page.GotoAsync("http://localhost:5020/index.html");
        await page.GetByRole(AriaRole.Button, new() { Name = sectionButton }).ClickAsync();
        await page.WaitForSelectorAsync($"h2:text('{headingText}')");
    }
}
