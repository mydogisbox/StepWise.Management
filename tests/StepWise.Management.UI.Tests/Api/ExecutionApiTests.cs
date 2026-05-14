using System.Text.Json;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public abstract class ExecutionTestBase : WorkflowTestBase
{
    protected async Task<(UpsertStepOutput AdminStep, UpsertStepOutput ListProductsStep)> SetupExampleCatalogAsync()
    {
        await BuildAsync(new CreateTargetCommand() with { BaseUrl = Static("http://localhost:5010") });
        await ExecuteAsync(new PostTargetCommandsRequest());
        await ExecuteAsync(new ListTargetsRequest());

        await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());

        var adminStep = await BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("admin-create-product"),
            Method   = Static("POST"),
            Path     = Static("/admin/products"),
            Headers  = Static<object?>(new Dictionary<string, object?>
            {
                ["X-Admin-Key"] = new Dictionary<string, object?> { ["static"] = "admin-secret" }
            }),
            Defaults = Static<object?>(new Dictionary<string, object?>
            {
                ["name"]     = new Dictionary<string, object?> { ["generated"] = "guid" },
                ["category"] = new Dictionary<string, object?> { ["static"] = "electronics" },
                ["price"]    = new Dictionary<string, object?> { ["static"] = 9.99 },
                ["stock"]    = new Dictionary<string, object?> { ["static"] = 10 }
            })
        });
        await ExecuteAsync(new PostCatalogStepCommandsRequest() with
        {
            AggregateId = Static(adminStep.Id),
            Commands    = Static(new List<object> { adminStep })
        });

        var listStep = await BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("list-products"),
            Method   = Static("GET"),
            Path     = Static("/products")
        });
        await ExecuteAsync(new PostCatalogStepCommandsRequest() with
        {
            AggregateId = Static(listStep.Id),
            Commands    = Static(new List<object> { listStep })
        });

        return (adminStep, listStep);
    }

    protected static RunStepResult GetStep(RunResult result, string stepName)
        => result.Steps.Single(s => s.StepName == stepName);
}

public class Execution_16_RunWorkflow : ExecutionTestBase
{
    [Fact]
    public async Task Test()
    {
        var (adminStep, _) = await SetupExampleCatalogAsync();

        await BuildAsync(new CreateWorkflowCommand());
        var step1 = await BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(adminStep.Id) });
        var step2 = await BuildAsync(new AppendStepCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed");

        var result = run.Result!;
        Assert.True(result.Passed);
        Assert.Equal(2, result.Steps.Length);
        Assert.Empty(result.AssertionErrors);

        var s1 = GetStep(result, step1.Id);
        Assert.False(string.IsNullOrEmpty(s1.Response.GetProperty("id").GetString()));
        var s2 = GetStep(result, step2.Id);
        Assert.NotEqual(JsonValueKind.Null, s2.Response.ValueKind);
    }
}

public class Execution_17_CrossReference : ExecutionTestBase
{
    [Fact]
    public async Task Test()
    {
        var (adminStep, _) = await SetupExampleCatalogAsync();

        await BuildAsync(new CreateWorkflowCommand());
        var step1 = await BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(adminStep.Id) });
        var step2 = await BuildAsync(new AppendStepCommand());
        await BuildAsync(new AddAssertionCommand() with
        {
            Assertion = Static<object>(new { equal = new object[] { "$" + step2.Id + ".totalCount", "999" } })
        });
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed");

        var result = run.Result!;
        Assert.False(result.Passed);
        Assert.Equal(2, result.Steps.Length);
        Assert.Single(result.AssertionErrors);

        var s1 = GetStep(result, step1.Id);
        Assert.False(string.IsNullOrEmpty(s1.Response.GetProperty("id").GetString()));
        var s2 = GetStep(result, step2.Id);
        Assert.NotEqual(JsonValueKind.Null, s2.Response.ValueKind);
    }
}

public class Execution_18_StoredAssertion : ExecutionTestBase
{
    [Fact]
    public async Task Test()
    {
        var (adminStep, _) = await SetupExampleCatalogAsync();

        await BuildAsync(new CreateWorkflowCommand());
        var step1 = await BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(adminStep.Id) });
        var step2 = await BuildAsync(new AppendStepCommand());
        await BuildAsync(new AddAssertionCommand() with
        {
            Assertion = Static<object>(new { notEmpty = step2.Id })
        });
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed");

        var result = run.Result!;
        Assert.True(result.Passed);
        Assert.Equal(2, result.Steps.Length);
        Assert.Empty(result.AssertionErrors);

        var s1 = GetStep(result, step1.Id);
        Assert.False(string.IsNullOrEmpty(s1.Response.GetProperty("id").GetString()));
        var s2 = GetStep(result, step2.Id);
        Assert.NotEqual(JsonValueKind.Null, s2.Response.ValueKind);
    }
}

public class Execution_19_RunResultStoredAsObject : ExecutionTestBase
{
    [Fact]
    public async Task Test()
    {
        var (adminStep, _) = await SetupExampleCatalogAsync();

        await BuildAsync(new CreateWorkflowCommand());
        var step1 = await BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(adminStep.Id) });
        var step2 = await BuildAsync(new AppendStepCommand());
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed");

        var result = run.Result!;
        Assert.True(run.Passed);
        Assert.True(result.Passed);
        Assert.Equal(2, result.Steps.Length);
        Assert.Empty(result.AssertionErrors);

        var s1 = GetStep(result, step1.Id);
        Assert.False(string.IsNullOrEmpty(s1.Response.GetProperty("id").GetString()));
        var s2 = GetStep(result, step2.Id);
        Assert.NotEqual(JsonValueKind.Null, s2.Response.ValueKind);
    }
}

public class Execution_20_ProductCategoryFilter : ExecutionTestBase
{
    [Fact]
    public async Task Test()
    {
        var (adminStep, _) = await SetupExampleCatalogAsync();

        var listElectronicsStep = await BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("list-electronics"),
            Method   = Static("GET"),
            Path     = Static("/products?category=electronics")
        });
        await ExecuteAsync(new PostCatalogStepCommandsRequest() with
        {
            AggregateId = Static(listElectronicsStep.Id),
            Commands    = Static(new List<object> { listElectronicsStep })
        });

        await BuildAsync(new CreateWorkflowCommand());
        var step1 = await BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(adminStep.Id) });
        var step2 = await BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(listElectronicsStep.Id) });
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed");

        var result = run.Result!;
        Assert.True(result.Passed);
        Assert.Equal(2, result.Steps.Length);
        Assert.Empty(result.AssertionErrors);

        var s1 = GetStep(result, step1.Id);
        Assert.False(string.IsNullOrEmpty(s1.Response.GetProperty("id").GetString()));
        var s2 = GetStep(result, step2.Id);
        Assert.NotEqual(JsonValueKind.Null, s2.Response.ValueKind);
    }
}

public class Execution_21_InStockFilter : ExecutionTestBase
{
    [Fact]
    public async Task Test()
    {
        await SetupExampleCatalogAsync();

        var listInStockStep = await BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("list-in-stock"),
            Method   = Static("GET"),
            Path     = Static("/products?inStock=true")
        });
        await ExecuteAsync(new PostCatalogStepCommandsRequest() with
        {
            AggregateId = Static(listInStockStep.Id),
            Commands    = Static(new List<object> { listInStockStep })
        });

        await BuildAsync(new CreateWorkflowCommand());
        var step1 = await BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(listInStockStep.Id) });
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed");

        var result = run.Result!;
        Assert.True(result.Passed);
        Assert.Single(result.Steps);
        Assert.Empty(result.AssertionErrors);

        var s1 = GetStep(result, step1.Id);
        Assert.NotEqual(JsonValueKind.Null, s1.Response.ValueKind);
    }
}

public class Execution_22_VoucherValidationWithAssertions : ExecutionTestBase
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

        await BuildAsync(new CreateWorkflowCommand());
        var step1 = await BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(validateSave10Step.Id) });
        await BuildAsync(new AddAssertionCommand() with
        {
            Assertion = Static<object>(new { equal = new object[] { "$" + step1.Id + ".valid", "true" } })
        });
        await BuildAsync(new AddAssertionCommand() with
        {
            Assertion = Static<object>(new { equal = new object[] { "$" + step1.Id + ".discountPct", "10" } })
        });
        await ExecuteAsync(new PostWorkflowCommandsRequest());
        await ExecuteAsync(new RunWorkflowRequest());
        var run = await PollAsync(new GetRunRequest(), r => r.Status == "completed");

        var result = run.Result!;
        Assert.True(result.Passed);
        Assert.Single(result.Steps);
        Assert.Empty(result.AssertionErrors);

        var s1 = GetStep(result, step1.Id);
        Assert.NotEqual(JsonValueKind.Undefined, s1.Response.GetProperty("valid").ValueKind);
    }
}
