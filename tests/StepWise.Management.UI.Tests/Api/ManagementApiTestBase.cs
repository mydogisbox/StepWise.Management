using System.Net.Http.Json;
using System.Text.Json;

namespace StepWise.Management.UI.Tests.Api;

public abstract class ManagementApiTestBase
{
    protected static readonly HttpClient Http = new() { BaseAddress = new Uri("http://localhost:5020") };

    protected static string NewId() => Guid.NewGuid().ToString();

    protected static async Task PostCommandsAsync(string resource, string aggregateId, IEnumerable<object> commands)
    {
        var resp = await Http.PostAsJsonAsync($"/{resource}/commands", new { aggregateId, commands });
        resp.EnsureSuccessStatusCode();
    }

    protected static async Task<(JsonElement Body, int Status)> PostCommandsRawAsync(string resource, string aggregateId, IEnumerable<object> commands)
    {
        var resp = await Http.PostAsJsonAsync($"/{resource}/commands", new { aggregateId, commands });
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (body, (int)resp.StatusCode);
    }

    protected static async Task<JsonElement> GetJsonAsync(string path)
    {
        var resp = await Http.GetAsync(path);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    protected static async Task<JsonElement> RunAndPollAsync(string workflowId)
    {
        var runId = NewId();
        var resp = await Http.PostAsJsonAsync($"/api/workflows/{workflowId}/run", new { runId });
        resp.EnsureSuccessStatusCode();

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var run = await GetJsonAsync($"/runs/{runId}");
            if (run.GetProperty("status").GetString() == "completed")
                return run;
            await Task.Delay(200);
        }
        throw new TimeoutException($"Run {runId} did not complete within 10 s");
    }

    protected static async Task<(string TargetId, string CatalogId, string StepId)> SetupCatalogWithStepAsync()
    {
        var targetId = NewId();
        await PostCommandsAsync("targets", targetId,
        [
            new { type = "CreateTarget", payload = new { id = targetId, name = NewId(), baseUrl = "http://localhost:5020" } }
        ]);

        var catalogId = NewId();
        await PostCommandsAsync("catalogs", catalogId,
        [
            new { type = "CreateCatalog", payload = new { id = catalogId, name = NewId() } }
        ]);

        var stepId = NewId();
        await PostCommandsAsync("catalog-steps", stepId,
        [
            new { type = "UpsertStep", payload = new { id = stepId, catalogId, targetId, stepName = NewId(), method = "GET", path = "/api/ping" } }
        ]);

        return (targetId, catalogId, stepId);
    }

    // Returns (targetId, catalogId, adminCreateProductStepId, listProductsStepId).
    // Defaults and headers for admin-create-product are stored in FieldValueDefinition format
    // so the workflow runner generates a fresh name GUID per execution.
    protected static async Task<(string TargetId, string CatalogId, string AdminStepId, string ListStepId)> SetupExampleCatalogAsync()
    {
        var targetId = NewId();
        await PostCommandsAsync("targets", targetId,
        [
            new { type = "CreateTarget", payload = new { id = targetId, name = NewId(), baseUrl = "http://localhost:5010" } }
        ]);

        var catalogId = NewId();
        await PostCommandsAsync("catalogs", catalogId,
        [
            new { type = "CreateCatalog", payload = new { id = catalogId, name = NewId() } }
        ]);

        var adminStepId = NewId();
        var adminDefaults = JsonDocument.Parse(
            """{"name":{"generated":"guid"},"category":{"static":"electronics"},"price":{"static":9.99},"stock":{"static":10}}""").RootElement;
        var adminHeaders = JsonDocument.Parse(
            """{"X-Admin-Key":{"static":"admin-secret"}}""").RootElement;
        await PostCommandsAsync("catalog-steps", adminStepId,
        [
            new { type = "UpsertStep", payload = new {
                id = adminStepId, catalogId, targetId,
                stepName = "admin-create-product",
                method = "POST", path = "/admin/products",
                headers = adminHeaders,
                defaults = adminDefaults
            }}
        ]);

        var listStepId = NewId();
        await PostCommandsAsync("catalog-steps", listStepId,
        [
            new { type = "UpsertStep", payload = new {
                id = listStepId, catalogId, targetId,
                stepName = "list-products",
                method = "GET", path = "/products"
            }}
        ]);

        return (targetId, catalogId, adminStepId, listStepId);
    }
}
