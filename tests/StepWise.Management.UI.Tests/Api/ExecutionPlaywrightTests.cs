using Microsoft.Playwright;
using Walkthrough.Core;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public abstract class VoucherValidationTestBase : ExecutionTestBase
{
    [Fact]
    public async Task Test()
    {
        await SetupExampleCatalogAsync();

        var validateSave10Step = await BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("validate-save10"),
            Method   = Static("POST"),
            Path     = Static("/vouchers/validate"),
            Defaults = Static<object?>(new Dictionary<string, object?> { ["code"] = "SAVE10" })
        });
        await ExecuteAsync(new PostCatalogStepCommandsRequest() with
        {
            AggregateId = Static(validateSave10Step.Id),
            Commands    = Static(new List<object> { validateSave10Step })
        });

        var workflow = await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(validateSave10Step.Id) });
        await BuildAsync(new AddAssertionCommand() with
        {
            Assertion = Static<object>(new { equal = new object[] { "$validate-save10.valid", "true" } })
        });
        await BuildAsync(new AddAssertionCommand() with
        {
            Assertion = Static<object>(new { equal = new object[] { "$validate-save10.discountPct", "10" } })
        });
        await ExecuteAsync(new PostWorkflowCommandsRequest());

        var listed = await ExecuteAsync(new ListWorkflowsRequest());
        _ = listed.Single(w => w.Name == workflow.Name);
        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed", timeoutMs: 15000);
        await AssertAsync(run);
    }

    protected virtual Task AssertAsync(RunResponse run) => Task.CompletedTask;
}

public class Execution_25_VoucherValidation_ViaUI : VoucherValidationTestBase, IAsyncLifetime
{
    private IPlaywright      _playwright      = null!;
    private IBrowser         _browser         = null!;
    private PlaywrightTarget _playwrightTarget = null!;

    protected IPage Page { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _playwright       = await Playwright.CreateAsync();
        _browser          = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        Page              = await _browser.NewPageAsync();
        _playwrightTarget = new PlaywrightTarget(Page)
            .Register<PlaywrightListWorkflowsStep>()
            .Register<PlaywrightRunWorkflowStep>()
            .Register<PlaywrightGetRunStep>();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }

    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), _playwrightTarget, ApiTarget);

    protected override Task AssertAsync(RunResponse run)
    {
        Assert.True(run.Passed);
        return Task.CompletedTask;
    }
}
