using System.Text.Json;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public abstract class ExecutionTestBase : ManagementTestBase
{
    protected static RunStepResult GetStep(RunResult result, string stepName)
        => result.Steps.Single(s => s.StepName == stepName);
}

public class Execution_RunWorkflow : ExecutionTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.TwoStepWorkflowAsync(Runner);

        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed");

        var result = run.Result!;
        Assert.True(result.Passed);
        Assert.Equal(2, result.Steps.Length);
        Assert.Empty(result.AssertionErrors);

        var s1 = GetStep(result, "admin-create-product");
        Assert.False(string.IsNullOrEmpty(s1.Response.GetProperty("id").GetString()));
        var s2 = GetStep(result, "list-products");
        Assert.NotEqual(JsonValueKind.Null, s2.Response.ValueKind);
    }
}

public class Execution_CrossReference : ExecutionTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.CrossReferenceWorkflowAsync(Runner);

        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed");

        var result = run.Result!;
        Assert.False(result.Passed);
        Assert.Equal(2, result.Steps.Length);
        Assert.Single(result.AssertionErrors);

        var s1 = GetStep(result, "admin-create-product");
        Assert.False(string.IsNullOrEmpty(s1.Response.GetProperty("id").GetString()));
        var s2 = GetStep(result, "list-products");
        Assert.NotEqual(JsonValueKind.Null, s2.Response.ValueKind);
    }
}

public class Execution_StoredAssertion : ExecutionTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.StoredAssertionWorkflowAsync(Runner);

        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed");

        var result = run.Result!;
        Assert.True(result.Passed);
        Assert.Equal(2, result.Steps.Length);
        Assert.Empty(result.AssertionErrors);

        var s1 = GetStep(result, "admin-create-product");
        Assert.False(string.IsNullOrEmpty(s1.Response.GetProperty("id").GetString()));
        var s2 = GetStep(result, "list-products");
        Assert.NotEqual(JsonValueKind.Null, s2.Response.ValueKind);
    }
}

public class Execution_RunResultStoredAsObject : ExecutionTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.TwoStepWorkflowAsync(Runner);

        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed");

        var result = run.Result!;
        Assert.True(run.Passed);
        Assert.True(result.Passed);
        Assert.Equal(2, result.Steps.Length);
        Assert.Empty(result.AssertionErrors);

        var s1 = GetStep(result, "admin-create-product");
        Assert.False(string.IsNullOrEmpty(s1.Response.GetProperty("id").GetString()));
        var s2 = GetStep(result, "list-products");
        Assert.NotEqual(JsonValueKind.Null, s2.Response.ValueKind);
    }
}

public class Execution_ProductCategoryFilter : ExecutionTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.ProductCategoryFilterWorkflowAsync(Runner);

        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed");

        var result = run.Result!;
        Assert.True(result.Passed);
        Assert.Equal(2, result.Steps.Length);
        Assert.Empty(result.AssertionErrors);

        var s1 = GetStep(result, "admin-create-product");
        Assert.False(string.IsNullOrEmpty(s1.Response.GetProperty("id").GetString()));
        var s2 = GetStep(result, "list-electronics");
        Assert.NotEqual(JsonValueKind.Null, s2.Response.ValueKind);
    }
}

public class Execution_InStockFilter : ExecutionTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.InStockFilterWorkflowAsync(Runner);

        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed");

        var result = run.Result!;
        Assert.True(result.Passed);
        Assert.Single(result.Steps);
        Assert.Empty(result.AssertionErrors);

        var s1 = GetStep(result, "list-in-stock");
        Assert.NotEqual(JsonValueKind.Null, s1.Response.ValueKind);
    }
}

public class Execution_RunFailed_StatusIsFailedWithError : ExecutionTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.RunFailedWorkflowAsync(Runner);

        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status != "pending");

        Assert.Equal("failed", run.Status);
        Assert.False(string.IsNullOrEmpty(run.Error));
    }
}

public class Execution_ReusedExampleWorkflowAssertion : ExecutionTestBase
{
    // Mirrors example-01-list-products.workflow.json: { "notEmpty": "$listProducts" }
    [Fact]
    public async Task Test()
    {
        await Setups.ReusedExampleWorkflowAssertionAsync(Runner);

        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed");

        var result = run.Result!;
        Assert.True(result.Passed);
        Assert.Equal(2, result.Steps.Length);
        Assert.Empty(result.AssertionErrors);
    }
}

public class Execution_VoucherValidationWithAssertions : ExecutionTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.VoucherValidationAsync(Runner);

        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed", timeoutMs: 15000);

        var result = run.Result!;
        Assert.True(result.Passed);
        Assert.Single(result.Steps);
        Assert.Empty(result.AssertionErrors);

        var s1 = GetStep(result, "validate-save10");
        Assert.NotEqual(JsonValueKind.Undefined, s1.Response.GetProperty("valid").ValueKind);
    }
}

public class Runs_List_ShowsCompletedRun : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.SetupCatalogWithStepAsync(Runner);

        var create = await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new AppendStepCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        await ExecuteAsync(new RunWorkflowRequest());
        await PollAsync(new GetRunRequest(), r => r.Status == "completed");
        var runs = await ExecuteAsync(new ListRunsRequest());

        var run = runs.Items.Single(r => r.WorkflowId == create.Id);
        Assert.True(run.Passed);
    }
}

public class Runs_Paging_PageSizeIsRespected : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.SetupCatalogWithStepAsync(Runner);

        var create = await BuildAsync(new CreateWorkflowCommand());
        await BuildAsync(new AppendStepCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        await ExecuteAsync(new RunWorkflowRequest());
        await PollAsync(new GetRunRequest(), r => r.Status == "completed");

        var page1 = await ExecuteAsync(new ListRunsRequest() with { PageSize = Static(1) });
        Assert.Equal(1, page1.PageSize);
        Assert.True(page1.Total >= 1);
        Assert.Single(page1.Items);
        Assert.Equal((int)Math.Ceiling((double)page1.Total / 1), page1.TotalPages);
    }
}
