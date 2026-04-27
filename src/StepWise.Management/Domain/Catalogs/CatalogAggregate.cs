using System.Text.Json;
using CommandFramework.Core;
using Walkthrough.Json;
using StepWise.Management;

namespace StepWise.Management.Domain.Catalogs;

// State
public record CatalogState(string Id, string Name, bool IsArchived, string Description = "");

// Events
public abstract record CatalogEvent;
public record CatalogCreated(string Id, string Name) : CatalogEvent;
public record CatalogUpdated(string Id, string Name, string Description) : CatalogEvent;
public record CatalogArchived(string Id) : CatalogEvent;
public record CatalogUnarchived(string Id) : CatalogEvent;

// Commands
public record CreateCatalog(string Id, string Name);
public record UpdateCatalog(string Name, string Description = "");
public record ArchiveCatalog();
public record UnarchiveCatalog();

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

    public static object DeserializeCommand(string type, JsonElement payload)
        => type switch
        {
            nameof(CreateCatalog) => payload.Deserialize<CreateCatalog>(JsonConfig.Options)!,
            nameof(UpdateCatalog) => payload.Deserialize<UpdateCatalog>(JsonConfig.Options)!,
            nameof(ArchiveCatalog) => payload.Deserialize<ArchiveCatalog>(JsonConfig.Options)!,
            nameof(UnarchiveCatalog) => payload.Deserialize<UnarchiveCatalog>(JsonConfig.Options)!,
            _ => throw new InvalidOperationException($"Unknown command type '{type}'.")
        };

    public static CatalogEvent DeserializeEvent(string type, string payload)
        => type switch
        {
            nameof(CatalogCreated) => JsonSerializer.Deserialize<CatalogCreated>(payload, JsonConfig.Options)!,
            nameof(CatalogUpdated) => JsonSerializer.Deserialize<CatalogUpdated>(payload, JsonConfig.Options)!,
            nameof(CatalogArchived) => JsonSerializer.Deserialize<CatalogArchived>(payload, JsonConfig.Options)!,
            nameof(CatalogUnarchived) => JsonSerializer.Deserialize<CatalogUnarchived>(payload, JsonConfig.Options)!,
            _ => throw new InvalidOperationException($"Unknown event type '{type}'.")
        };

    public static readonly AggregateDefinition<CatalogState, CatalogEvent> Definition = new(
        Dispatch: Dispatch,
        Apply: Apply);
}
