using System.Text.Json;
using CommandFramework.Core;
using StepWise.Json;
using StepWise.Management;

namespace StepWise.Management.Domain.Catalogs;

// State
public record CatalogState(string Id, string Name, bool IsArchived);

// Events
public abstract record CatalogEvent;
public record CatalogCreated(string Id, string Name) : CatalogEvent;
public record CatalogArchived(string Id) : CatalogEvent;

// Commands
public record CreateCatalog(string Id, string Name);
public record ArchiveCatalog();

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

    public static Result<IEnumerable<CatalogEvent>> Handle(CatalogState state, ArchiveCatalog _)
    {
        if (state.IsArchived) return "Catalog is already archived.";
        return new CatalogEvent[] { new CatalogArchived(state.Id) };
    }

    public static CatalogState Apply(CatalogState? state, CatalogEvent e)
        => e switch
        {
            CatalogCreated evt => new CatalogState(evt.Id, evt.Name, false),
            CatalogArchived evt => state! with { IsArchived = true },
            _ => throw new InvalidOperationException($"Unknown event: {e.GetType().Name}")
        };

    public static Result<IEnumerable<CatalogEvent>> Dispatch(CatalogState? state, object command)
        => command switch
        {
            CreateCatalog cmd => Handle(state, cmd),
            ArchiveCatalog cmd when state != null => Handle(state, cmd),
            ArchiveCatalog => "Catalog does not exist.",
            _ => $"Unknown command '{command.GetType().Name}'."
        };

    public static object DeserializeCommand(string type, JsonElement payload)
        => type switch
        {
            nameof(CreateCatalog) => payload.Deserialize<CreateCatalog>(JsonConfig.Options)!,
            nameof(ArchiveCatalog) => payload.Deserialize<ArchiveCatalog>(JsonConfig.Options)!,
            _ => throw new InvalidOperationException($"Unknown command type '{type}'.")
        };

    public static CatalogEvent DeserializeEvent(string type, string payload)
        => type switch
        {
            nameof(CatalogCreated) => JsonSerializer.Deserialize<CatalogCreated>(payload, JsonConfig.Options)!,
            nameof(CatalogArchived) => JsonSerializer.Deserialize<CatalogArchived>(payload, JsonConfig.Options)!,
            _ => throw new InvalidOperationException($"Unknown event type '{type}'.")
        };

    public static readonly AggregateDefinition<CatalogState, CatalogEvent> Definition = new(
        Dispatch: Dispatch,
        Apply: Apply);
}
