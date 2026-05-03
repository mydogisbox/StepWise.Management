using System.Text.Json;

namespace StepWise.Management.UI.Tests.Api;

public class CatalogApiTests : ManagementApiTestBase
{
    [Fact]
    public async Task Catalog_02_Create_NameAsserted()
    {
        var catalogId = NewId();
        await PostCommandsAsync("catalogs", catalogId,
        [
            new { type = "CreateCatalog", payload = new { id = catalogId, name = "My Catalog" } }
        ]);

        var catalog = await GetJsonAsync($"/catalogs/{catalogId}");

        Assert.Equal("My Catalog", catalog.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Catalog_03_AddStep_AllFieldsAsserted()
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
        var defaults = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["param"] = "value1" });
        await PostCommandsAsync("catalog-steps", stepId,
        [
            new { type = "UpsertStep", payload = new {
                id = stepId, catalogId, targetId,
                stepName = "getStatus", method = "GET", path = "/api/status",
                defaults
            }}
        ]);

        var step = await GetJsonAsync($"/catalog-steps/{stepId}");

        Assert.Equal("getStatus", step.GetProperty("stepName").GetString());
        Assert.Equal(targetId, step.GetProperty("targetId").GetString());
        Assert.Equal(catalogId, step.GetProperty("catalogId").GetString());
        Assert.Equal("GET", step.GetProperty("method").GetString());
        Assert.Equal("/api/status", step.GetProperty("path").GetString());
        Assert.Equal("value1", step.GetProperty("defaults").GetProperty("param").GetString());
    }

    [Fact]
    public async Task Catalog_04_UpsertStep_AllFieldsUpdated()
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
        var defaults1 = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["param"] = "value1" });
        await PostCommandsAsync("catalog-steps", stepId,
        [
            new { type = "UpsertStep", payload = new {
                id = stepId, catalogId, targetId,
                stepName = "getStatus", method = "GET", path = "/api/catalogs",
                defaults = defaults1
            }}
        ]);

        var defaults2 = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["param"] = "value2" });
        await PostCommandsAsync("catalog-steps", stepId,
        [
            new { type = "UpsertStep", payload = new {
                id = stepId, catalogId, targetId,
                stepName = "getStatus", method = "POST", path = "/api/catalogs/v2",
                defaults = defaults2
            }}
        ]);

        var step = await GetJsonAsync($"/catalog-steps/{stepId}");

        Assert.Equal("getStatus", step.GetProperty("stepName").GetString());
        Assert.Equal(targetId, step.GetProperty("targetId").GetString());
        Assert.Equal("POST", step.GetProperty("method").GetString());
        Assert.Equal("/api/catalogs/v2", step.GetProperty("path").GetString());
        Assert.Equal("value2", step.GetProperty("defaults").GetProperty("param").GetString());
    }

    [Fact]
    public async Task Catalog_05_ArchiveStep_IsArchivedTrue()
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
            new { type = "UpsertStep", payload = new { id = stepId, catalogId, targetId, stepName = NewId(), method = "GET", path = "/api/ping" } },
            new { type = "ArchiveStep", payload = new { } }
        ]);

        var step = await GetJsonAsync($"/catalog-steps/{stepId}");

        Assert.True(step.GetProperty("isArchived").GetBoolean());
    }

    [Fact]
    public async Task Catalog_06_ListExcludesArchivedByDefault()
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
            new { type = "UpsertStep", payload = new { id = stepId, catalogId, targetId, stepName = NewId(), method = "GET", path = "/api/ping" } },
            new { type = "ArchiveStep", payload = new { } }
        ]);

        var list = await GetJsonAsync($"/catalog-steps?catalogId={catalogId}");

        Assert.Equal(0, list.GetArrayLength());
    }

    [Fact]
    public async Task Catalog_07_ListIncludesArchivedWhenFlagSet()
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
            new { type = "UpsertStep", payload = new { id = stepId, catalogId, targetId, stepName = "archivedStep", method = "GET", path = "/api/ping" } },
            new { type = "ArchiveStep", payload = new { } }
        ]);

        var list = await GetJsonAsync($"/catalog-steps?catalogId={catalogId}&showArchived=true");

        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("archivedStep", list[0].GetProperty("stepName").GetString());
    }

    [Fact]
    public async Task Catalog_08_ErrorCapturesStatus()
    {
        var stepId = NewId();
        var (body, status) = await PostCommandsRawAsync("catalog-steps", stepId,
        [
            new { type = "UpsertStep", payload = new { id = stepId, catalogId = "", targetId = "any", stepName = "x", method = "GET", path = "/x" } }
        ]);

        Assert.Equal(422, status);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("error").GetString()));
    }

    [Fact]
    public async Task Catalog_09_SuccessCapturesStatus()
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
        var (body, status) = await PostCommandsRawAsync("catalog-steps", stepId,
        [
            new { type = "UpsertStep", payload = new { id = stepId, catalogId, targetId, stepName = NewId(), method = "GET", path = "/api/ping" } }
        ]);

        Assert.Equal(200, status);
        Assert.NotEqual(JsonValueKind.Null, body.ValueKind);
    }

    [Fact]
    public async Task Catalog_10_UpdateCatalog_NameAndDescriptionAsserted()
    {
        var catalogId = NewId();
        await PostCommandsAsync("catalogs", catalogId,
        [
            new { type = "CreateCatalog", payload = new { id = catalogId, name = "original-catalog" } },
            new { type = "UpdateCatalog", payload = new { name = "updated-catalog", description = "A useful catalog" } }
        ]);

        var catalog = await GetJsonAsync($"/catalogs/{catalogId}");

        Assert.Equal("updated-catalog", catalog.GetProperty("name").GetString());
        Assert.Equal("A useful catalog", catalog.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Catalog_11_ArchiveCatalog_IsArchivedTrue()
    {
        var catalogId = NewId();
        await PostCommandsAsync("catalogs", catalogId,
        [
            new { type = "CreateCatalog", payload = new { id = catalogId, name = NewId() } },
            new { type = "ArchiveCatalog", payload = new { } }
        ]);

        var catalog = await GetJsonAsync($"/catalogs/{catalogId}");

        Assert.True(catalog.GetProperty("isArchived").GetBoolean());
    }

    [Fact]
    public async Task Catalog_12_UnarchiveCatalog_IsArchivedFalse()
    {
        var catalogId = NewId();
        await PostCommandsAsync("catalogs", catalogId,
        [
            new { type = "CreateCatalog", payload = new { id = catalogId, name = NewId() } },
            new { type = "ArchiveCatalog", payload = new { } },
            new { type = "UnarchiveCatalog", payload = new { } }
        ]);

        var catalog = await GetJsonAsync($"/catalogs/{catalogId}");

        Assert.False(catalog.GetProperty("isArchived").GetBoolean());
    }

    [Fact]
    public async Task Catalog_13_UnarchiveStep_IsArchivedFalse()
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
            new { type = "UpsertStep", payload = new { id = stepId, catalogId, targetId, stepName = NewId(), method = "GET", path = "/api/ping" } },
            new { type = "ArchiveStep", payload = new { } },
            new { type = "UnarchiveStep", payload = new { } }
        ]);

        var step = await GetJsonAsync($"/catalog-steps/{stepId}");

        Assert.False(step.GetProperty("isArchived").GetBoolean());
    }

    [Fact]
    public async Task Catalog_14_StepShapes_RoundTrip()
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

        var requestShape = JsonSerializer.SerializeToElement(new { kind = "request" });
        var responseShape = JsonSerializer.SerializeToElement(new { kind = "response" });
        var stepId = NewId();
        await PostCommandsAsync("catalog-steps", stepId,
        [
            new { type = "UpsertStep", payload = new {
                id = stepId, catalogId, targetId, stepName = NewId(), method = "GET", path = "/api/ping",
                requestShape, responseShape
            }}
        ]);

        var step = await GetJsonAsync($"/catalog-steps/{stepId}");

        Assert.Equal("request", step.GetProperty("requestShape").GetProperty("kind").GetString());
        Assert.Equal("response", step.GetProperty("responseShape").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Catalog_15_StepPolling_FlagAndRetryAsserted()
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
            new { type = "UpsertStep", payload = new {
                id = stepId, catalogId, targetId, stepName = NewId(), method = "GET", path = "/api/ping",
                isPolling = true, retryCount = 3, retryDurationMs = 500
            }}
        ]);

        var step = await GetJsonAsync($"/catalog-steps/{stepId}");

        Assert.True(step.GetProperty("isPolling").GetBoolean());
        Assert.Equal(3, step.GetProperty("retryCount").GetInt32());
        Assert.Equal(500, step.GetProperty("retryDurationMs").GetInt32());
    }

    [Fact]
    public async Task Catalog_16_ListExcludesArchivedCatalog()
    {
        var catalogId = NewId();
        var catalogName = NewId();
        await PostCommandsAsync("catalogs", catalogId,
        [
            new { type = "CreateCatalog", payload = new { id = catalogId, name = catalogName } },
            new { type = "ArchiveCatalog", payload = new { } }
        ]);

        var list = await GetJsonAsync("/catalogs");

        Assert.DoesNotContain(list.EnumerateArray(), c => c.GetProperty("name").GetString() == catalogName);
    }

    [Fact]
    public async Task Catalog_17_ListIncludesArchivedCatalogWhenFlagSet()
    {
        var catalogId = NewId();
        await PostCommandsAsync("catalogs", catalogId,
        [
            new { type = "CreateCatalog", payload = new { id = catalogId, name = "archived-catalog" } },
            new { type = "ArchiveCatalog", payload = new { } }
        ]);

        var list = await GetJsonAsync("/catalogs?showArchived=true");

        var match = list.EnumerateArray().First(c => c.GetProperty("id").GetString() == catalogId);
        Assert.True(match.GetProperty("isArchived").GetBoolean());
    }
}
