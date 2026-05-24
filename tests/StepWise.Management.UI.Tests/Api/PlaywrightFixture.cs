using Microsoft.Playwright;
using Walkthrough.Core;

namespace StepWise.Management.UI.Tests.Api;

// One browser process shared across all tests. Pages are isolated, so parallel tests are safe.
// The browser is never explicitly closed — the process exits after the test run anyway.
internal static class SharedBrowser
{
    private static readonly Lazy<Task<IBrowser>> _browser = new(async () =>
    {
        var playwright = await Playwright.CreateAsync();
        return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    });

    public static Task<IBrowser> GetAsync() => _browser.Value;
}

public interface IUsePlaywright : IAsyncLifetime
{
    IPage Page { get; set; }

    async Task IAsyncLifetime.InitializeAsync()
    {
        var browser = await SharedBrowser.GetAsync();
        Page = await browser.NewPageAsync();
    }

    async Task IAsyncLifetime.DisposeAsync() => await Page.CloseAsync();
}

public interface IUsePlaywrightWithTarget : IUsePlaywright
{
    PlaywrightTarget PwTarget { get; set; }

    async Task IAsyncLifetime.InitializeAsync()
    {
        var browser = await SharedBrowser.GetAsync();
        Page = await browser.NewPageAsync();
        PwTarget = new PlaywrightTarget(Page)
            .Register<PlaywrightListWorkflowsStep>()
            .Register<PlaywrightRunWorkflowStep>()
            .Register<PlaywrightGetRunStep>()
            .Register<PlaywrightListRunsStep>();
    }
}
