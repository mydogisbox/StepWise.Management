using System.Text.Json;
using Walkthrough.Http;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public class Catalog_AddStep_AllFieldsCorrect : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        var target = await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());
        var catalog = await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());

        await BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("getStatus"),
            Method   = Static("GET"),
            Path     = Static("/api/status"),
            Defaults = Static<object?>(new Dictionary<string, object?> { ["param"] = "value1" })
        });
        await ExecuteAsync(new PostCatalogStepCommandsRequest());
        var step = await ExecuteAsync(new GetCatalogStepRequest());

        Assert.Equal("getStatus",   step.StepName);
        Assert.Equal(target.Id,      step.TargetId);
        Assert.Equal(catalog.Id,     step.CatalogId);
        Assert.Equal("GET",         step.Method);
        Assert.Equal("/api/status", step.Path);
        Assert.Equal("value1",      step.Defaults?.GetProperty("param").GetString());
    }
}

public class Catalog_UpsertStep_UpdatesFields : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        var target = await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());
        await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());

        await BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("getStatus"),
            Method   = Static("GET"),
            Path     = Static("/api/catalogs"),
            Defaults = Static<object?>(new Dictionary<string, object?> { ["param"] = "value1" })
        });
        await ExecuteAsync(new PostCatalogStepCommandsRequest());
        await ExecuteAsync(new GetCatalogStepRequest());

        await BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("getStatus"),
            Method   = Static("POST"),
            Path     = Static("/api/catalogs/v2"),
            Defaults = Static<object?>(new Dictionary<string, object?> { ["param"] = "value2" })
        });
        await ExecuteAsync(new PostCatalogStepCommandsRequest());
        var step = await ExecuteAsync(new GetCatalogStepRequest());

        Assert.Equal("getStatus",        step.StepName);
        Assert.Equal(target.Id,          step.TargetId);
        Assert.Equal("POST",             step.Method);
        Assert.Equal("/api/catalogs/v2", step.Path);
        Assert.Equal("value2",           step.Defaults?.GetProperty("param").GetString());
    }
}

public class Catalog_ArchiveStep_IsArchivedTrue : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.SetupCatalogAsync(Runner);

        await BuildAsync(new UpsertStepCommand());
        await BuildAsync(new ArchiveStepCommand());
        await ExecuteAsync(new PostCatalogStepCommandsRequest());
        var step = await ExecuteAsync(new GetCatalogStepRequest());

        Assert.True(step.IsArchived);
    }
}

public class Catalog_ArchiveStep_ExcludedFromList : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.SetupCatalogAsync(Runner);

        await BuildAsync(new UpsertStepCommand());
        await BuildAsync(new ArchiveStepCommand());
        await ExecuteAsync(new PostCatalogStepCommandsRequest());
        var steps = await ExecuteAsync(new ListCatalogStepsRequest());

        Assert.Empty(steps);
    }
}

public class Catalog_ArchiveStep_IncludedWhenShowArchived : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.SetupCatalogAsync(Runner);

        await BuildAsync(new UpsertStepCommand() with { StepName = Static("archivedStep") });
        await BuildAsync(new ArchiveStepCommand());
        await ExecuteAsync(new PostCatalogStepCommandsRequest());
        var steps = await ExecuteAsync(new ListCatalogStepsRequest() with { ShowArchived = Static("true") });

        Assert.Single(steps);
        Assert.Equal("archivedStep", steps[0].StepName);
    }
}

public class Catalog_ErrorCapturesStatus : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new UpsertStepCommand() with
        {
            CatalogId = Static(""),
            TargetId  = Static("")
        });
        var raw       = (HttpRawResult)await ExecuteRawAsync(new PostCatalogStepCommandsRequest());
        var errorBody = JsonSerializer.Deserialize<JsonElement>((string)raw.Body!);

        Assert.Equal(422, raw.StatusCode);
        Assert.NotEmpty(errorBody.GetProperty("error").GetString()!);
    }
}

public class Catalog_SuccessCapturesStatus : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.SetupCatalogAsync(Runner);

        await BuildAsync(new UpsertStepCommand());
        var raw  = (HttpRawResult)await ExecuteRawAsync(new PostCatalogStepCommandsRequest());
        var body = (CatalogStepCommandSuccess[])raw.Body!;

        Assert.Equal(200, raw.StatusCode);
        Assert.NotEmpty(body);
    }
}

public class Catalog_StepShapes_StoredAndReturned : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.SetupCatalogAsync(Runner);

        await BuildAsync(new UpsertStepCommand() with
        {
            RequestShape  = Static<object?>(new Dictionary<string, object?> { ["kind"] = "request" }),
            ResponseShape = Static<object?>(new Dictionary<string, object?> { ["kind"] = "response" })
        });
        await ExecuteAsync(new PostCatalogStepCommandsRequest());
        var step = await ExecuteAsync(new GetCatalogStepRequest());

        Assert.Equal("request",  step.RequestShape?.GetProperty("kind").GetString());
        Assert.Equal("response", step.ResponseShape?.GetProperty("kind").GetString());
    }
}

public class Catalog_StepPolling_StoredAndReturned : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.SetupCatalogAsync(Runner);

        await BuildAsync(new UpsertStepCommand() with
        {
            IsPolling       = Static<bool?>(true),
            RetryCount      = Static<int?>(3),
            RetryDurationMs = Static<int?>(500)
        });
        await ExecuteAsync(new PostCatalogStepCommandsRequest());
        var step = await ExecuteAsync(new GetCatalogStepRequest());

        Assert.True(step.IsPolling);
        Assert.Equal(3,   step.RetryCount);
        Assert.Equal(500, step.RetryDurationMs);
    }
}

public class Catalog_UnarchiveStep_IsArchivedFalse : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await Setups.SetupCatalogAsync(Runner);

        await BuildAsync(new UpsertStepCommand());
        await BuildAsync(new ArchiveStepCommand());
        await BuildAsync(new UnarchiveStepCommand());
        await ExecuteAsync(new PostCatalogStepCommandsRequest());
        var step = await ExecuteAsync(new GetCatalogStepRequest());

        Assert.False(step.IsArchived);
    }
}
