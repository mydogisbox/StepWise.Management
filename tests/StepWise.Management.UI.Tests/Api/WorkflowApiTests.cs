using System.Text.Json;

namespace StepWise.Management.UI.Tests.Api;

public class WorkflowApiTests : ManagementApiTestBase
{
    [Fact]
    public async Task Workflow_06_Create_NameCorrectAndStepsEmpty()
    {
        var workflowId = NewId();
        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = "Test Workflow 6" } }
        ]);

        var workflow = await GetJsonAsync($"/workflows/{workflowId}");

        Assert.Equal("Test Workflow 6", workflow.GetProperty("name").GetString());
        Assert.Equal(0, workflow.GetProperty("steps").GetArrayLength());
    }

    [Fact]
    public async Task Workflow_07_Rename_UpdatesName()
    {
        var workflowId = NewId();
        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "RenameWorkflow", payload = new { name = "Renamed Workflow 7" } }
        ]);

        var workflow = await GetJsonAsync($"/workflows/{workflowId}");

        Assert.Equal("Renamed Workflow 7", workflow.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Workflow_08_AppendStep_OrderAndDefaultsAsserted()
    {
        var (_, catalogId, catalogStepId) = await SetupCatalogWithStepAsync();

        var workflowId = NewId();
        var stepAId = NewId();
        var stepBId = NewId();
        var defaultsA = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["param"] = "value1" });
        var defaultsB = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["param"] = "value2" });

        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "AppendStep", payload = new { id = stepAId, catalogStepId, catalogId, defaults = defaultsA } },
            new { type = "AppendStep", payload = new { id = stepBId, catalogStepId, catalogId, defaults = defaultsB } }
        ]);

        var workflow = await GetJsonAsync($"/workflows/{workflowId}");
        var steps = workflow.GetProperty("steps");

        Assert.Equal(stepAId, steps[0].GetProperty("id").GetString());
        Assert.Equal("value1", steps[0].GetProperty("defaults").GetProperty("param").GetString());
        Assert.Equal(stepBId, steps[1].GetProperty("id").GetString());
        Assert.Equal("value2", steps[1].GetProperty("defaults").GetProperty("param").GetString());
    }

    [Fact]
    public async Task Workflow_09_InsertStepBefore_OrderAsserted()
    {
        var (_, catalogId, catalogStepId) = await SetupCatalogWithStepAsync();

        var workflowId = NewId();
        var stepAId = NewId();
        var stepBId = NewId();
        var insertedId = NewId();
        var defaultsA = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["param"] = "value1" });
        var defaultsB = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["param"] = "value2" });
        var defaultsInserted = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["param"] = "value3" });

        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "AppendStep", payload = new { id = stepAId, catalogStepId, catalogId, defaults = defaultsA } },
            new { type = "AppendStep", payload = new { id = stepBId, catalogStepId, catalogId, defaults = defaultsB } },
            new { type = "InsertStepBefore", payload = new { beforeId = stepBId, id = insertedId, catalogStepId, catalogId, defaults = defaultsInserted } }
        ]);

        var workflow = await GetJsonAsync($"/workflows/{workflowId}");
        var steps = workflow.GetProperty("steps");

        Assert.Equal(stepAId, steps[0].GetProperty("id").GetString());
        Assert.Equal("value1", steps[0].GetProperty("defaults").GetProperty("param").GetString());
        Assert.Equal(insertedId, steps[1].GetProperty("id").GetString());
        Assert.Equal("value3", steps[1].GetProperty("defaults").GetProperty("param").GetString());
        Assert.Equal(stepBId, steps[2].GetProperty("id").GetString());
        Assert.Equal("value2", steps[2].GetProperty("defaults").GetProperty("param").GetString());
    }

    [Fact]
    public async Task Workflow_10_RemoveStep_OneStepRemains()
    {
        var (_, catalogId, catalogStepId) = await SetupCatalogWithStepAsync();

        var workflowId = NewId();
        var stepAId = NewId();
        var stepBId = NewId();

        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "AppendStep", payload = new { id = stepAId, catalogStepId, catalogId } },
            new { type = "AppendStep", payload = new { id = stepBId, catalogStepId, catalogId } },
            new { type = "RemoveStep", payload = new { id = stepAId } }
        ]);

        var workflow = await GetJsonAsync($"/workflows/{workflowId}");
        var steps = workflow.GetProperty("steps");

        Assert.Equal(1, steps.GetArrayLength());
        Assert.Equal(stepBId, steps[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task Workflow_11_SetStepDefaults_DefaultsAsserted()
    {
        var (_, catalogId, catalogStepId) = await SetupCatalogWithStepAsync();

        var workflowId = NewId();
        var stepId = NewId();
        var defaults = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["param"] = "value1" });

        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "AppendStep", payload = new { id = stepId, catalogStepId, catalogId } },
            new { type = "SetStepDefaults", payload = new { id = stepId, defaults } }
        ]);

        var workflow = await GetJsonAsync($"/workflows/{workflowId}");
        var steps = workflow.GetProperty("steps");

        Assert.Equal(1, steps.GetArrayLength());
        Assert.Equal(stepId, steps[0].GetProperty("id").GetString());
        Assert.Equal("value1", steps[0].GetProperty("defaults").GetProperty("param").GetString());
    }

    [Fact]
    public async Task Workflow_12_BadAssertion_StoredSuccessfully()
    {
        var (_, catalogId, catalogStepId) = await SetupCatalogWithStepAsync();

        var workflowId = NewId();
        var stepId = NewId();

        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "AppendStep", payload = new { id = stepId, catalogStepId, catalogId } },
            new { type = "AddAssertion", payload = new { assertion = new { equal = new[] { "nonExistentStep.id", "appendStep.payload.id" } } } }
        ]);

        var workflow = await GetJsonAsync($"/workflows/{workflowId}");

        Assert.Equal(1, workflow.GetProperty("assertions").GetArrayLength());
    }

    [Fact]
    public async Task Workflow_13_AddAssertion_StoredAndAsserted()
    {
        var (_, catalogId, catalogStepId) = await SetupCatalogWithStepAsync();

        var workflowId = NewId();
        var stepId = NewId();

        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "AppendStep", payload = new { id = stepId, catalogStepId, catalogId } },
            new { type = "AddAssertion", payload = new { assertion = new { notEmpty = stepId } } }
        ]);

        var workflow = await GetJsonAsync($"/workflows/{workflowId}");

        Assert.Equal(1, workflow.GetProperty("assertions").GetArrayLength());
    }

    [Fact]
    public async Task Workflow_14_ArchiveWorkflow_IsArchivedTrue()
    {
        var workflowId = NewId();
        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "ArchiveWorkflow", payload = new { } }
        ]);

        var workflow = await GetJsonAsync($"/workflows/{workflowId}");

        Assert.True(workflow.GetProperty("isArchived").GetBoolean());
    }

    [Fact]
    public async Task Workflow_15_UnarchiveWorkflow_IsArchivedFalse()
    {
        var workflowId = NewId();
        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "ArchiveWorkflow", payload = new { } },
            new { type = "UnarchiveWorkflow", payload = new { } }
        ]);

        var workflow = await GetJsonAsync($"/workflows/{workflowId}");

        Assert.False(workflow.GetProperty("isArchived").GetBoolean());
    }

    [Fact]
    public async Task Workflow_16_UpdateDescription_DescriptionAsserted()
    {
        var workflowId = NewId();
        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "UpdateDescription", payload = new { description = "A workflow description" } }
        ]);

        var workflow = await GetJsonAsync($"/workflows/{workflowId}");

        Assert.Equal("A workflow description", workflow.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Workflow_17_ListExcludesArchivedByDefault()
    {
        var workflowId = NewId();
        var workflowName = NewId();
        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = workflowName } },
            new { type = "ArchiveWorkflow", payload = new { } }
        ]);

        var list = await GetJsonAsync("/workflows");

        Assert.DoesNotContain(list.EnumerateArray(), w => w.GetProperty("name").GetString() == workflowName);
    }

    [Fact]
    public async Task Workflow_18_ListIncludesArchivedWhenFlagSet()
    {
        var workflowId = NewId();
        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = "archived-workflow" } },
            new { type = "ArchiveWorkflow", payload = new { } }
        ]);

        var list = await GetJsonAsync("/workflows?showArchived=true");

        var match = list.EnumerateArray().First(w => w.GetProperty("id").GetString() == workflowId);
        Assert.True(match.GetProperty("isArchived").GetBoolean());
    }

    [Fact]
    public async Task Runs_01_List_ShowsCompletedRun()
    {
        var (_, catalogId, catalogStepId) = await SetupCatalogWithStepAsync();

        var workflowId = NewId();
        var stepId = NewId();
        await PostCommandsAsync("workflows", workflowId,
        [
            new { type = "CreateWorkflow", payload = new { id = workflowId, name = NewId() } },
            new { type = "AppendStep", payload = new { id = stepId, catalogStepId, catalogId } }
        ]);

        await RunAndPollAsync(workflowId);

        var list = await GetJsonAsync("/runs");

        Assert.NotEmpty(list.EnumerateArray());
        var match = list.EnumerateArray().First(r => r.GetProperty("workflowId").GetString() == workflowId);
        Assert.True(match.GetProperty("passed").GetBoolean());
    }
}
