using Microsoft.Playwright;
using Walkthrough.Core;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public class PlaywrightContext : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private IBrowser    _browser    = null!;
    public  IPage       Page        { get; private set; } = null!;
    public  PlaywrightTarget Target { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser    = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        Page        = await _browser.NewPageAsync();
        Target      = new PlaywrightTarget(Page)
            .Register<PlaywrightListWorkflowsStep>()
            .Register<PlaywrightRunWorkflowStep>()
            .Register<PlaywrightGetRunStep>()
            .Register<PlaywrightListRunsStep>();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}

public class Execution_25_VoucherValidation_ViaUI : ExecutionTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), _pw.Target, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.VoucherValidationAsync(Runner);

        var listed = await ExecuteAsync(new ListWorkflowsRequest() with { Name = Static(workflowName) });
        Assert.Single(listed.Items);
        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed", timeoutMs: 15000);
        Assert.True(run.Passed);
    }
}

public class Execution_33_RunFailed_ViaUI : ExecutionTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), _pw.Target, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.RunFailedWorkflowAsync(Runner);

        var listed = await ExecuteAsync(new ListWorkflowsRequest() with { Name = Static(workflowName) });
        Assert.Single(listed.Items);
        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed", timeoutMs: 15000);
        Assert.False(run.Passed);
    }
}

public class Execution_32_ReusedExampleWorkflowAssertion_ViaUI : ExecutionTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), _pw.Target, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.ReusedExampleWorkflowAssertionAsync(Runner);

        var listed = await ExecuteAsync(new ListWorkflowsRequest() with { Name = Static(workflowName) });
        Assert.Single(listed.Items);
        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed", timeoutMs: 15000);
        Assert.True(run.Passed);
    }
}

public class Execution_31_InStockFilter_ViaUI : ExecutionTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), _pw.Target, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.InStockFilterWorkflowAsync(Runner);

        var listed = await ExecuteAsync(new ListWorkflowsRequest() with { Name = Static(workflowName) });
        Assert.Single(listed.Items);
        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed", timeoutMs: 15000);
        Assert.True(run.Passed);
    }
}

public class Execution_30_ProductCategoryFilter_ViaUI : ExecutionTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), _pw.Target, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.ProductCategoryFilterWorkflowAsync(Runner);

        var listed = await ExecuteAsync(new ListWorkflowsRequest() with { Name = Static(workflowName) });
        Assert.Single(listed.Items);
        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed", timeoutMs: 15000);
        Assert.True(run.Passed);
    }
}

public class Execution_29_RunResultStoredAsObject_ViaUI : ExecutionTestBase, IAsyncLifetime
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
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed", timeoutMs: 15000);
        Assert.True(run.Passed);
    }
}

public class Execution_28_StoredAssertion_ViaUI : ExecutionTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), _pw.Target, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.StoredAssertionWorkflowAsync(Runner);

        var listed = await ExecuteAsync(new ListWorkflowsRequest() with { Name = Static(workflowName) });
        Assert.Single(listed.Items);
        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed", timeoutMs: 15000);
        Assert.True(run.Passed);
    }
}

public class Execution_27_CrossReference_ViaUI : ExecutionTestBase, IAsyncLifetime
{
    private readonly PlaywrightContext _pw = new();

    public Task InitializeAsync() => _pw.InitializeAsync();
    public Task DisposeAsync()    => _pw.DisposeAsync();

    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), _pw.Target, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.CrossReferenceWorkflowAsync(Runner);

        var listed = await ExecuteAsync(new ListWorkflowsRequest() with { Name = Static(workflowName) });
        Assert.Single(listed.Items);
        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed", timeoutMs: 15000);
        Assert.False(run.Passed);
    }
}

public class Execution_26_RunWorkflow_ViaUI : ExecutionTestBase, IAsyncLifetime
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
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed", timeoutMs: 15000);
        Assert.True(run.Passed);
    }
}
