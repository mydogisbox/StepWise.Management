using StepWise.Management.Domain.Catalogs;
using Xunit;

namespace StepWise.Management.Tests.Catalogs;

public class CatalogAggregateTests
{
    private static CatalogState Created(string name = "My Catalog")
        => CatalogAggregate.Apply(null, new CatalogCreated("test-id", name));

    // ── CreateCatalog ─────────────────────────────────────────────────────────

    [Fact]
    public void CreateCatalog_succeeds_on_new_stream()
    {
        var result = CatalogAggregate.Dispatch(null, new CreateCatalog("test-id", "My Catalog"));

        Assert.True(result.IsSuccess);
        var events = result.Value.ToList();
        Assert.Single(events);
        Assert.IsType<CatalogCreated>(events[0]);
    }

    [Fact]
    public void CreateCatalog_fails_when_catalog_already_exists()
    {
        var state = Created();
        var result = CatalogAggregate.Dispatch(state, new CreateCatalog("test-id", "My Catalog"));

        Assert.True(result.IsError);
    }

    [Fact]
    public void CreateCatalog_fails_when_name_is_empty()
    {
        var result = CatalogAggregate.Dispatch(null, new CreateCatalog("test-id", ""));

        Assert.True(result.IsError);
    }

    [Fact]
    public void Apply_CatalogCreated_initializes_state()
    {
        var state = Created("My Catalog");

        Assert.Equal("My Catalog", state.Name);
        Assert.False(state.IsArchived);
    }

    // ── ArchiveCatalog ────────────────────────────────────────────────────────

    [Fact]
    public void ArchiveCatalog_succeeds()
    {
        var state = Created();
        var result = CatalogAggregate.Dispatch(state, new ArchiveCatalog());

        Assert.True(result.IsSuccess);
        var newState = result.Value.Aggregate(state, CatalogAggregate.Apply);
        Assert.True(newState.IsArchived);
    }

    [Fact]
    public void ArchiveCatalog_fails_when_already_archived()
    {
        var state = Created() with { IsArchived = true };
        var result = CatalogAggregate.Dispatch(state, new ArchiveCatalog());

        Assert.True(result.IsError);
    }

    [Fact]
    public void ArchiveCatalog_fails_when_catalog_does_not_exist()
    {
        var result = CatalogAggregate.Dispatch(null, new ArchiveCatalog());

        Assert.True(result.IsError);
    }
}
