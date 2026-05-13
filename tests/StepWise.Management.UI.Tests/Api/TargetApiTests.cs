using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public class Target_01_Archive_IsArchivedTrue : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        await BuildAsync(new ArchiveTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        var target = await ExecuteAsync(new GetTargetRequest());
        Assert.True(target.IsArchived);
    }
}

public class Target_02_Unarchive_IsArchivedFalse : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateTargetCommand());
        await BuildAsync(new ArchiveTargetCommand());
        await BuildAsync(new UnarchiveTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        var target = await ExecuteAsync(new GetTargetRequest());
        Assert.False(target.IsArchived);
    }
}

public class Target_03_Update_NameAndBaseUrlUpdated : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateTargetCommand() with
        {
            Name    = Static("original-name"),
            BaseUrl = Static("http://original.com")
        });
        await BuildAsync(new UpdateTargetCommand() with
        {
            Name    = Static("updated-name"),
            BaseUrl = Static("http://updated.com")
        });
        await ExecuteAsync(new PostTargetCommandsRequest());

        var target = await ExecuteAsync(new GetTargetRequest());
        Assert.Equal("updated-name",        target.Name);
        Assert.Equal("http://updated.com",  target.BaseUrl);
    }
}

public class Target_04_Archive_ExcludedFromList : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateTargetCommand());
        await BuildAsync(new ArchiveTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        var targets = await ExecuteAsync(new ListTargetsRequest());
        Assert.DoesNotContain(targets, t => t.Name == create.Name);
    }
}

public class Target_05_Archive_IncludedInListWhenShowArchived : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateTargetCommand());
        await BuildAsync(new ArchiveTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        var targets = await ExecuteAsync(new ListTargetsRequest() with { ShowArchived = Static("true") });
        var target  = targets.Single(t => t.Name == create.Name);
        Assert.True(target.IsArchived);
    }
}

public class Target_06_Create_HasCreatedAt : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        var targets = await ExecuteAsync(new ListTargetsRequest());
        Assert.NotNull(targets.Single(t => t.Name == create.Name).CreatedAt);
    }
}
