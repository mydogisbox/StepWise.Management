using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public class Target_Archive_IsArchivedTrue : ManagementTestBase
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

public class Target_Unarchive_IsArchivedFalse : ManagementTestBase
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

public class Target_Update_NameAndBaseUrlUpdated : ManagementTestBase
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

public class Target_Archive_ExcludedFromList : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateTargetCommand());
        await BuildAsync(new ArchiveTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        var targets = await ExecuteAsync(new ListTargetsRequest());
        Assert.DoesNotContain(targets.Items, t => t.Name == create.Name);
    }
}

public class Target_Archive_IncludedInListWhenShowArchived : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateTargetCommand());
        await BuildAsync(new ArchiveTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        var targets = await ExecuteAsync(new ListTargetsRequest() with { ShowArchived = Static("true") });
        var target  = targets.Items.Single(t => t.Name == create.Name);
        Assert.True(target.IsArchived);
    }
}

public class Target_Create_HasCreatedAt : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateTargetCommand());
        await ExecuteAsync(new PostTargetCommandsRequest());

        var targets = await ExecuteAsync(new ListTargetsRequest());
        Assert.NotNull(targets.Items.Single(t => t.Name == create.Name).CreatedAt);
    }
}

public class Target_Paging_PageSizeIsRespected : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        for (var i = 0; i < 3; i++)
        {
            await BuildAsync(new CreateTargetCommand());
            await ExecuteAsync(new PostTargetCommandsRequest());
        }

        var page1 = await ExecuteAsync(new ListTargetsRequest() with { PageSize = Static(2) });
        Assert.Equal(2, page1.Items.Length);
        Assert.True(page1.TotalPages >= 2);
        Assert.Equal(2, page1.PageSize);

        var page2 = await ExecuteAsync(new ListTargetsRequest() with { Page = Static(2), PageSize = Static(2) });
        Assert.True(page2.Items.Length >= 1);
    }
}
