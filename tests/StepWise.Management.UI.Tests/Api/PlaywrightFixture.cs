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

public abstract class PlaywrightTestBase : ManagementTestBase, IAsyncLifetime
{
    protected IPage Page { get; private set; } = null!;

    public virtual async Task InitializeAsync()
    {
        var browser = await SharedBrowser.GetAsync();
        Page = await browser.NewPageAsync();
        await Page.GotoAsync(UiHelper.AppUrl);
    }

    public async Task DisposeAsync() => await Page.CloseAsync();
}

public abstract class PlaywrightWithTargetTestBase : PlaywrightTestBase
{
    protected PlaywrightTarget PwTarget { get; private set; } = null!;

    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), PwTarget, ApiTarget);

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        PwTarget = new PlaywrightTarget(Page)
            .Register<PlaywrightListCatalogsStep>()
            .Register<PlaywrightListWorkflowsStep>()
            .Register<PlaywrightListRunsStep>()
            .Register<PlaywrightOpenCatalogDetailStep>()
            .Register<PlaywrightOpenWorkflowDetailStep>()
            .Register<PlaywrightArchiveCatalogStep>()
            .Register<PlaywrightArchiveWorkflowStep>()
            .Register<PlaywrightArchiveTargetStep>()
            .Register<PlaywrightCreateWorkflowViaFormStep>()
            .Register<PlaywrightCreateCatalogViaFormStep>()
            .Register<PlaywrightNextWorkflowsPageStep>()
            .Register<PlaywrightNextCatalogsPageStep>()
            .Register<PlaywrightOpenRunDetailStep>();
    }
}
