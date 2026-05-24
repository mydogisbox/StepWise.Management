using Microsoft.Playwright;
using Walkthrough.Core;

namespace StepWise.Management.UI.Tests.Api;

public class Execution_VoucherValidation_ViaUI : ExecutionTestBase, IUsePlaywrightWithTarget
{
    public IPage Page { get; set; } = null!;
    public PlaywrightTarget PwTarget { get; set; } = null!;


    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), PwTarget, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.VoucherValidationAsync(Runner);
        var run = await Setups.RunViaUiAsync(Runner, workflowName);
        Assert.True(run.Passed);
    }
}

public class Execution_RunWorkflow_ViaUI : ExecutionTestBase, IUsePlaywrightWithTarget
{
    public IPage Page { get; set; } = null!;
    public PlaywrightTarget PwTarget { get; set; } = null!;


    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), PwTarget, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.TwoStepWorkflowAsync(Runner);
        var run = await Setups.RunViaUiAsync(Runner, workflowName);
        Assert.True(run.Passed);
    }
}

public class Execution_CrossReference_ViaUI : ExecutionTestBase, IUsePlaywrightWithTarget
{
    public IPage Page { get; set; } = null!;
    public PlaywrightTarget PwTarget { get; set; } = null!;


    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), PwTarget, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.CrossReferenceWorkflowAsync(Runner);
        var run = await Setups.RunViaUiAsync(Runner, workflowName);
        Assert.False(run.Passed);
    }
}

public class Execution_StoredAssertion_ViaUI : ExecutionTestBase, IUsePlaywrightWithTarget
{
    public IPage Page { get; set; } = null!;
    public PlaywrightTarget PwTarget { get; set; } = null!;


    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), PwTarget, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.StoredAssertionWorkflowAsync(Runner);
        var run = await Setups.RunViaUiAsync(Runner, workflowName);
        Assert.True(run.Passed);
    }
}

public class Execution_ProductCategoryFilter_ViaUI : ExecutionTestBase, IUsePlaywrightWithTarget
{
    public IPage Page { get; set; } = null!;
    public PlaywrightTarget PwTarget { get; set; } = null!;


    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), PwTarget, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.ProductCategoryFilterWorkflowAsync(Runner);
        var run = await Setups.RunViaUiAsync(Runner, workflowName);
        Assert.True(run.Passed);
    }
}

public class Execution_InStockFilter_ViaUI : ExecutionTestBase, IUsePlaywrightWithTarget
{
    public IPage Page { get; set; } = null!;
    public PlaywrightTarget PwTarget { get; set; } = null!;


    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), PwTarget, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.InStockFilterWorkflowAsync(Runner);
        var run = await Setups.RunViaUiAsync(Runner, workflowName);
        Assert.True(run.Passed);
    }
}

public class Execution_ReusedExampleWorkflowAssertion_ViaUI : ExecutionTestBase, IUsePlaywrightWithTarget
{
    public IPage Page { get; set; } = null!;
    public PlaywrightTarget PwTarget { get; set; } = null!;


    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), PwTarget, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.ReusedExampleWorkflowAssertionAsync(Runner);
        var run = await Setups.RunViaUiAsync(Runner, workflowName);
        Assert.True(run.Passed);
    }
}

public class Execution_RunFailed_ViaUI : ExecutionTestBase, IUsePlaywrightWithTarget
{
    public IPage Page { get; set; } = null!;
    public PlaywrightTarget PwTarget { get; set; } = null!;


    protected override WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), PwTarget, ApiTarget);

    [Fact]
    public async Task Test()
    {
        var workflowName = await Setups.RunFailedWorkflowAsync(Runner);
        var run = await Setups.RunViaUiAsync(Runner, workflowName);
        Assert.False(run.Passed);
    }
}
