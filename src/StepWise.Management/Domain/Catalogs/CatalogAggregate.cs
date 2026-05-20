using CommandFramework.Core;
using static StepWise.Management.Domain.Catalogs.CatalogEvent;
using static StepWise.Management.Domain.Catalogs.CatalogCommands;

namespace StepWise.Management.Domain.Catalogs;

// State
public record CatalogState(string Id, string Name, bool IsArchived, string Description = "");

// Events
public abstract record CatalogEvent
{
    public record CatalogCreated(string Id, string Name) : CatalogEvent;
    public record CatalogUpdated(string Id, string Name, string Description) : CatalogEvent;
    public record CatalogArchived(string Id) : CatalogEvent;
    public record CatalogUnarchived(string Id) : CatalogEvent;
}

// Commands
public abstract record CatalogCommands
{
    public record CreateCatalog(string Id, string Name) : CatalogCommands;
    public record UpdateCatalog(string Name, string Description = "") : CatalogCommands;
    public record ArchiveCatalog() : CatalogCommands;
    public record UnarchiveCatalog() : CatalogCommands;
}

public static class CatalogAggregate
{
    public static Result<IEnumerable<CatalogEvent>> Handle(CatalogState? state, CreateCatalog cmd)
    {
        if (state != null)
            return "Catalog already exists.";
        if (string.IsNullOrWhiteSpace(cmd.Name))
            return "Catalog Name is required.";
        return new CatalogEvent[] { new CatalogCreated(cmd.Id, cmd.Name) };
    }

    public static Result<IEnumerable<CatalogEvent>> Handle(CatalogState state, UpdateCatalog cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name))
            return "Catalog Name is required.";
        return new CatalogEvent[] { new CatalogUpdated(state.Id, cmd.Name, cmd.Description) };
    }

    public static Result<IEnumerable<CatalogEvent>> Handle(CatalogState state, ArchiveCatalog _)
    {
        if (state.IsArchived) return "Catalog is already archived.";
        return new CatalogEvent[] { new CatalogArchived(state.Id) };
    }

    public static Result<IEnumerable<CatalogEvent>> Handle(CatalogState state, UnarchiveCatalog _)
    {
        if (!state.IsArchived) return "Catalog is not archived.";
        return new CatalogEvent[] { new CatalogUnarchived(state.Id) };
    }

    public static CatalogState Apply(CatalogState? state, CatalogEvent e)
        => e switch
        {
            CatalogCreated evt => new CatalogState(evt.Id, evt.Name, false),
            CatalogUpdated evt => state! with { Name = evt.Name, Description = evt.Description },
            CatalogArchived => state! with { IsArchived = true },
            CatalogUnarchived => state! with { IsArchived = false },
            _ => throw new InvalidOperationException($"Unknown event: {e.GetType().Name}")
        };

    public static Result<IEnumerable<CatalogEvent>> Dispatch(CatalogState? state, object command)
        => command switch
        {
            CreateCatalog cmd => Handle(state, cmd),
            UpdateCatalog cmd when state != null => Handle(state, cmd),
            ArchiveCatalog cmd when state != null => Handle(state, cmd),
            UnarchiveCatalog cmd when state != null => Handle(state, cmd),
            _ when state == null => "Catalog does not exist.",
            _ => $"Unknown command '{command.GetType().Name}'."
        };

    public static readonly AggregateDefinition<CatalogState, CatalogEvent> Definition = new(
        Dispatch: Dispatch,
        Apply: Apply);
}
