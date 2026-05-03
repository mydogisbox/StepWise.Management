using System.Text.Json;

namespace StepWise.Management.UI.Tests.Api;

public class ExecutionApiTests : ManagementApiTestBase
{
    [Fact]
    public async Task Execution_16_Execute_Passes()
    {
        var (_, catalogId, adminStepId, listStepId) = await SetupExampleCatalogAsync();

        var workflowId = NewId();
        var step1Id = NewId();
        var step2Id = NewId();
        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "AppendStep", payload = new { id = step1Id, catalogStepId = adminStepId, catalogId } },
            new { type = "AppendStep", payload = new { id = step2Id, catalogStepId = listStepId, catalogId } }
        ]);

        var run = await RunAndPollAsync(workflowId);
        var result = run.GetProperty("result");

        Assert.True(result.GetProperty("passed").GetBoolean());
        Assert.Equal(2, result.GetProperty("steps").GetArrayLength());
        Assert.Equal(0, result.GetProperty("assertionErrors").GetArrayLength());

        var steps = result.GetProperty("steps").EnumerateArray().ToList();
        var step1 = steps.First(s => s.GetProperty("stepName").GetString() == step1Id);
        Assert.False(string.IsNullOrEmpty(step1.GetProperty("response").GetProperty("id").GetString()));

        var step2 = steps.First(s => s.GetProperty("stepName").GetString() == step2Id);
        Assert.NotEqual(JsonValueKind.Null, step2.GetProperty("response").ValueKind);
    }

    [Fact]
    public async Task Execution_17_ExecuteAssertionFails_ReportedCorrectly()
    {
        var (_, catalogId, adminStepId, listStepId) = await SetupExampleCatalogAsync();

        var workflowId = NewId();
        var step1Id = NewId();
        var step2Id = NewId();
        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "AppendStep", payload = new { id = step1Id, catalogStepId = adminStepId, catalogId } },
            new { type = "AppendStep", payload = new { id = step2Id, catalogStepId = listStepId, catalogId } },
            new { type = "AddAssertion", payload = new { assertion = new { equal = new[] { $"${step2Id}.totalCount", "999" } } } }
        ]);

        var run = await RunAndPollAsync(workflowId);
        var result = run.GetProperty("result");

        Assert.False(result.GetProperty("passed").GetBoolean());
        Assert.Equal(2, result.GetProperty("steps").GetArrayLength());
        Assert.Equal(1, result.GetProperty("assertionErrors").GetArrayLength());

        var steps = result.GetProperty("steps").EnumerateArray().ToList();
        var step1 = steps.First(s => s.GetProperty("stepName").GetString() == step1Id);
        Assert.False(string.IsNullOrEmpty(step1.GetProperty("response").GetProperty("id").GetString()));

        var step2 = steps.First(s => s.GetProperty("stepName").GetString() == step2Id);
        Assert.NotEqual(JsonValueKind.Null, step2.GetProperty("response").ValueKind);
    }

    [Fact]
    public async Task Execution_18_ExecuteWithStepDefaults_DefaultsInOutput()
    {
        var (_, catalogId, adminStepId, listStepId) = await SetupExampleCatalogAsync();

        var workflowId = NewId();
        var step1Id = NewId();
        var step2Id = NewId();
        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "AppendStep", payload = new { id = step1Id, catalogStepId = adminStepId, catalogId } },
            new { type = "AppendStep", payload = new { id = step2Id, catalogStepId = listStepId, catalogId } },
            new { type = "AddAssertion", payload = new { assertion = new { notEmpty = step2Id } } }
        ]);

        var run = await RunAndPollAsync(workflowId);
        var result = run.GetProperty("result");

        Assert.True(result.GetProperty("passed").GetBoolean());
        Assert.Equal(2, result.GetProperty("steps").GetArrayLength());
        Assert.Equal(0, result.GetProperty("assertionErrors").GetArrayLength());

        var steps = result.GetProperty("steps").EnumerateArray().ToList();
        var step1 = steps.First(s => s.GetProperty("stepName").GetString() == step1Id);
        Assert.False(string.IsNullOrEmpty(step1.GetProperty("response").GetProperty("id").GetString()));

        var step2 = steps.First(s => s.GetProperty("stepName").GetString() == step2Id);
        Assert.NotEqual(JsonValueKind.Null, step2.GetProperty("response").ValueKind);
    }

    [Fact]
    public async Task Execution_19_RunResultStoredAsObject()
    {
        var (_, catalogId, adminStepId, listStepId) = await SetupExampleCatalogAsync();

        var workflowId = NewId();
        var step1Id = NewId();
        var step2Id = NewId();
        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "AppendStep", payload = new { id = step1Id, catalogStepId = adminStepId, catalogId } },
            new { type = "AppendStep", payload = new { id = step2Id, catalogStepId = listStepId, catalogId } }
        ]);

        var run = await RunAndPollAsync(workflowId);
        var result = run.GetProperty("result");

        Assert.True(run.GetProperty("passed").GetBoolean());
        Assert.True(result.GetProperty("passed").GetBoolean());
        Assert.Equal(2, result.GetProperty("steps").GetArrayLength());
        Assert.Equal(0, result.GetProperty("assertionErrors").GetArrayLength());

        var steps = result.GetProperty("steps").EnumerateArray().ToList();
        var step1 = steps.First(s => s.GetProperty("stepName").GetString() == step1Id);
        Assert.False(string.IsNullOrEmpty(step1.GetProperty("response").GetProperty("id").GetString()));

        var step2 = steps.First(s => s.GetProperty("stepName").GetString() == step2Id);
        Assert.NotEqual(JsonValueKind.Null, step2.GetProperty("response").ValueKind);
    }

    [Fact]
    public async Task Execution_20_ProductCategoryFilter()
    {
        var (targetId, catalogId, adminStepId, _) = await SetupExampleCatalogAsync();

        var electronicsStepId = NewId();
        await PostCommandsAsync("catalog-steps", electronicsStepId,
        [
            new { type = "UpsertStep", payload = new {
                id = electronicsStepId, catalogId, targetId,
                stepName = "list-electronics",
                method = "GET", path = "/products?category=electronics"
            }}
        ]);

        var workflowId = NewId();
        var step1Id = NewId();
        var step2Id = NewId();
        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "AppendStep", payload = new { id = step1Id, catalogStepId = adminStepId, catalogId } },
            new { type = "AppendStep", payload = new { id = step2Id, catalogStepId = electronicsStepId, catalogId } }
        ]);

        var run = await RunAndPollAsync(workflowId);
        var result = run.GetProperty("result");

        Assert.True(result.GetProperty("passed").GetBoolean());
        Assert.Equal(2, result.GetProperty("steps").GetArrayLength());
        Assert.Equal(0, result.GetProperty("assertionErrors").GetArrayLength());

        var steps = result.GetProperty("steps").EnumerateArray().ToList();
        var step1 = steps.First(s => s.GetProperty("stepName").GetString() == step1Id);
        Assert.False(string.IsNullOrEmpty(step1.GetProperty("response").GetProperty("id").GetString()));

        var step2 = steps.First(s => s.GetProperty("stepName").GetString() == step2Id);
        Assert.NotEqual(JsonValueKind.Null, step2.GetProperty("response").ValueKind);
    }

    [Fact]
    public async Task Execution_21_InStockFilter()
    {
        var (targetId, catalogId, _, _) = await SetupExampleCatalogAsync();

        var inStockStepId = NewId();
        await PostCommandsAsync("catalog-steps", inStockStepId,
        [
            new { type = "UpsertStep", payload = new {
                id = inStockStepId, catalogId, targetId,
                stepName = "list-in-stock",
                method = "GET", path = "/products?inStock=true"
            }}
        ]);

        var workflowId = NewId();
        var step1Id = NewId();
        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "AppendStep", payload = new { id = step1Id, catalogStepId = inStockStepId, catalogId } }
        ]);

        var run = await RunAndPollAsync(workflowId);
        var result = run.GetProperty("result");

        Assert.True(result.GetProperty("passed").GetBoolean());
        Assert.Equal(1, result.GetProperty("steps").GetArrayLength());
        Assert.Equal(0, result.GetProperty("assertionErrors").GetArrayLength());

        var steps = result.GetProperty("steps").EnumerateArray().ToList();
        var step1 = steps.First(s => s.GetProperty("stepName").GetString() == step1Id);
        Assert.NotEqual(JsonValueKind.Null, step1.GetProperty("response").ValueKind);
    }

    [Fact]
    public async Task Execution_22_VoucherValidationWithAssertions()
    {
        var (targetId, catalogId, _, _) = await SetupExampleCatalogAsync();

        var validateStepId = NewId();
        var validateDefaults = JsonDocument.Parse("""{"code":{"static":"SAVE10"}}""").RootElement;
        await PostCommandsAsync("catalog-steps", validateStepId,
        [
            new { type = "UpsertStep", payload = new {
                id = validateStepId, catalogId, targetId,
                stepName = "validate-save10",
                method = "POST", path = "/vouchers/validate",
                defaults = validateDefaults
            }}
        ]);

        var workflowId = NewId();
        var step1Id = NewId();
        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "AppendStep", payload = new { id = step1Id, catalogStepId = validateStepId, catalogId } },
            new { type = "AddAssertion", payload = new { assertion = new { equal = new[] { $"${step1Id}.valid", "true" } } } },
            new { type = "AddAssertion", payload = new { assertion = new { equal = new[] { $"${step1Id}.discountPct", "10" } } } }
        ]);

        var run = await RunAndPollAsync(workflowId);
        var result = run.GetProperty("result");

        Assert.True(result.GetProperty("passed").GetBoolean());
        Assert.Equal(1, result.GetProperty("steps").GetArrayLength());
        Assert.Equal(0, result.GetProperty("assertionErrors").GetArrayLength());

        var steps = result.GetProperty("steps").EnumerateArray().ToList();
        var step1 = steps.First(s => s.GetProperty("stepName").GetString() == step1Id);
        Assert.NotEqual(JsonValueKind.Null, step1.GetProperty("response").GetProperty("valid").ValueKind);
    }
}
