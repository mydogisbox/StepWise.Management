using Walkthrough.Http;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public class Catalog_18_CreateCatalog_EmptyName_Returns422 : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateCatalogCommand() with { Name = Static("") });
        var raw = (HttpRawResult)await ExecuteRawAsync(new PostCatalogCommandsRequest());

        Assert.Equal(422, raw.StatusCode);
    }
}

public class Catalog_19_CreateCatalog_DuplicateCreate_Returns422 : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());

        await BuildAsync(new CreateCatalogCommand() with { Id = Static(create.Id) });
        var raw = (HttpRawResult)await ExecuteRawAsync(new PostCatalogCommandsRequest());

        Assert.Equal(422, raw.StatusCode);
    }
}

public class Catalog_20_ArchiveCatalog_AlreadyArchived_Returns422 : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateCatalogCommand());
        await BuildAsync(new ArchiveCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());

        await BuildAsync(new ArchiveCatalogCommand());
        var raw = (HttpRawResult)await ExecuteRawAsync(new PostCatalogCommandsRequest());

        Assert.Equal(422, raw.StatusCode);
    }
}

public class Catalog_21_ArchiveCatalog_DoesNotExist_Returns422 : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new ArchiveCatalogCommand());
        var raw = (HttpRawResult)await ExecuteRawAsync(new PostCatalogCommandsRequest() with
        {
            AggregateId = Static(Guid.NewGuid().ToString())
        });

        Assert.Equal(422, raw.StatusCode);
    }
}

public class Catalog_02_Create_NameAsserted : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateCatalogCommand() with { Name = Static("My Catalog") });
        await ExecuteAsync(new PostCatalogCommandsRequest());
        var catalog = await ExecuteAsync(new GetCatalogRequest());

        Assert.Equal("My Catalog", catalog.Name);
    }
}

public class Catalog_10_UpdateCatalog_NameAndDescriptionUpdated : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateCatalogCommand() with { Name = Static("original-catalog") });
        await BuildAsync(new UpdateCatalogCommand() with
        {
            Name        = Static("updated-catalog"),
            Description = Static("A useful catalog")
        });
        await ExecuteAsync(new PostCatalogCommandsRequest());
        var catalog = await ExecuteAsync(new GetCatalogRequest());

        Assert.Equal("updated-catalog",    catalog.Name);
        Assert.Equal("A useful catalog",   catalog.Description);
    }
}

public class Catalog_11_ArchiveCatalog_IsArchivedTrue : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateCatalogCommand());
        await BuildAsync(new ArchiveCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());
        var catalog = await ExecuteAsync(new GetCatalogRequest());

        Assert.True(catalog.IsArchived);
    }
}

public class Catalog_12_UnarchiveCatalog_IsArchivedFalse : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        await BuildAsync(new CreateCatalogCommand());
        await BuildAsync(new ArchiveCatalogCommand());
        await BuildAsync(new UnarchiveCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());
        var catalog = await ExecuteAsync(new GetCatalogRequest());

        Assert.False(catalog.IsArchived);
    }
}

public class Catalog_16_Archive_ExcludedFromList : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateCatalogCommand());
        await BuildAsync(new ArchiveCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());
        var catalogs = await ExecuteAsync(new ListCatalogsRequest());

        Assert.DoesNotContain(catalogs.Items, c => c.Name == create.Name);
    }
}

public class Catalog_17_Archive_IncludedInListWhenShowArchived : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        var create = await BuildAsync(new CreateCatalogCommand());
        await BuildAsync(new ArchiveCatalogCommand());
        await ExecuteAsync(new PostCatalogCommandsRequest());
        var catalogs = await ExecuteAsync(new ListCatalogsRequest() with { ShowArchived = Static("true") });

        var catalog = catalogs.Items.Single(c => c.Id == create.Id);
        Assert.True(catalog.IsArchived);
    }
}

public class Catalog_22_Paging_PageSizeIsRespected : ManagementTestBase
{
    [Fact]
    public async Task Test()
    {
        for (var i = 0; i < 3; i++)
        {
            await BuildAsync(new CreateCatalogCommand());
            await ExecuteAsync(new PostCatalogCommandsRequest());
        }

        var page1 = await ExecuteAsync(new ListCatalogsRequest() with { PageSize = Static(2) });
        Assert.Equal(2, page1.Items.Length);
        Assert.True(page1.TotalPages >= 2);
        Assert.Equal(2, page1.PageSize);

        var page2 = await ExecuteAsync(new ListCatalogsRequest() with { Page = Static(2), PageSize = Static(2) });
        Assert.True(page2.Items.Length >= 1);
    }
}
