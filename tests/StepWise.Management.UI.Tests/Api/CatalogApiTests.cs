using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

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

        Assert.DoesNotContain(catalogs, c => c.Name == create.Name);
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

        var catalog = catalogs.Single(c => c.Id == create.Id);
        Assert.True(catalog.IsArchived);
    }
}
