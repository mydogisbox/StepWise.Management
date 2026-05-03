using System.Text.Json;

namespace StepWise.Management.UI.Tests.Api;

public class TargetApiTests : ManagementApiTestBase
{
    [Fact]
    public async Task Catalog_01_CreateTarget_BaseUrlAsserted()
    {
        var targetId = NewId();
        await PostCommandsAsync("targets", targetId,
        [
            new { type = "CreateTarget", payload = new { id = targetId, name = "my-target", baseUrl = "http://localhost:5020" } }
        ]);

        var target = await GetJsonAsync($"/targets/{targetId}");

        Assert.Equal("my-target", target.GetProperty("name").GetString());
        Assert.Equal("http://localhost:5020", target.GetProperty("baseUrl").GetString());
    }

    [Fact]
    public async Task Target_01_Archive_IsArchivedTrue()
    {
        var targetId = NewId();
        await PostCommandsAsync("targets", targetId,
        [
            new { type = "CreateTarget", payload = new { id = targetId, name = NewId(), baseUrl = "http://localhost:5020" } },
            new { type = "ArchiveTarget", payload = new { } }
        ]);

        var target = await GetJsonAsync($"/targets/{targetId}");

        Assert.True(target.GetProperty("isArchived").GetBoolean());
    }

    [Fact]
    public async Task Target_02_Unarchive_IsArchivedFalse()
    {
        var targetId = NewId();
        await PostCommandsAsync("targets", targetId,
        [
            new { type = "CreateTarget", payload = new { id = targetId, name = NewId(), baseUrl = "http://localhost:5020" } },
            new { type = "ArchiveTarget", payload = new { } },
            new { type = "UnarchiveTarget", payload = new { } }
        ]);

        var target = await GetJsonAsync($"/targets/{targetId}");

        Assert.False(target.GetProperty("isArchived").GetBoolean());
    }

    [Fact]
    public async Task Target_03_Update_NameAndUrlAsserted()
    {
        var targetId = NewId();
        await PostCommandsAsync("targets", targetId,
        [
            new { type = "CreateTarget", payload = new { id = targetId, name = "original-name", baseUrl = "http://original.com" } },
            new { type = "UpdateTarget", payload = new { name = "updated-name", baseUrl = "http://updated.com" } }
        ]);

        var target = await GetJsonAsync($"/targets/{targetId}");

        Assert.Equal("updated-name", target.GetProperty("name").GetString());
        Assert.Equal("http://updated.com", target.GetProperty("baseUrl").GetString());
    }

    [Fact]
    public async Task Target_04_ListExcludesArchivedByDefault()
    {
        var targetId = NewId();
        var targetName = NewId();
        await PostCommandsAsync("targets", targetId,
        [
            new { type = "CreateTarget", payload = new { id = targetId, name = targetName, baseUrl = "http://localhost:5020" } },
            new { type = "ArchiveTarget", payload = new { } }
        ]);

        var list = await GetJsonAsync("/targets");

        Assert.DoesNotContain(list.EnumerateArray(), t => t.GetProperty("name").GetString() == targetName);
    }

    [Fact]
    public async Task Target_05_ListIncludesArchivedWhenFlagSet()
    {
        var targetId = NewId();
        var targetName = NewId();
        await PostCommandsAsync("targets", targetId,
        [
            new { type = "CreateTarget", payload = new { id = targetId, name = targetName, baseUrl = "http://localhost:5020" } },
            new { type = "ArchiveTarget", payload = new { } }
        ]);

        var list = await GetJsonAsync("/targets?showArchived=true");

        var match = list.EnumerateArray().FirstOrDefault(t => t.GetProperty("name").GetString() == targetName);
        Assert.NotEqual(JsonValueKind.Undefined, match.ValueKind);
        Assert.True(match.GetProperty("isArchived").GetBoolean());
    }

    [Fact]
    public async Task Target_06_CreatedAt_PresentInList()
    {
        var targetId = NewId();
        await PostCommandsAsync("targets", targetId,
        [
            new { type = "CreateTarget", payload = new { id = targetId, name = NewId(), baseUrl = "http://localhost:5020" } }
        ]);

        var list = await GetJsonAsync("/targets");

        var match = list.EnumerateArray().First(t => t.GetProperty("id").GetString() == targetId);
        Assert.False(string.IsNullOrEmpty(match.GetProperty("createdAt").GetString()));
    }
}
