using System.Text.Json;
using System.Text.Json.Serialization;
using CommandFramework.Core;
using StepWise.Management;

namespace StepWise.Management.Domain.CatalogSteps;

// State
public record CatalogStepState(
    string Id,
    string CatalogId,
    string TargetId,
    string StepName,
    string Method,
    string Path,
    JsonElement? Defaults,
    bool IsArchived);

// Events
public abstract record CatalogStepEvent;
public record CatalogStepUpserted(
    string Id,
    string CatalogId,
    string TargetId,
    string StepName,
    string Method,
    string Path,
    JsonElement? Defaults) : CatalogStepEvent;
public record CatalogStepArchived(string Id) : CatalogStepEvent;

// Commands
public record UpsertStep(
    string Id,
    string CatalogId,
    string StepName,
    string TargetId,
    string Method,
    string Path,
    JsonElement? Defaults = null);
public record ArchiveStep();

public static class CatalogStepAggregate
{
    public static Result<IEnumerable<CatalogStepEvent>> Handle(CatalogStepState? state, UpsertStep cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.CatalogId)) return "CatalogId is required.";
        if (string.IsNullOrWhiteSpace(cmd.StepName)) return "StepName is required.";
        return new CatalogStepEvent[]
        {
            new CatalogStepUpserted(cmd.Id, cmd.CatalogId, cmd.TargetId, cmd.StepName, cmd.Method, cmd.Path, cmd.Defaults)
        };
    }

    public static Result<IEnumerable<CatalogStepEvent>> Handle(CatalogStepState? state, ArchiveStep _)
    {
        if (state == null) return "CatalogStep does not exist.";
        if (state.IsArchived) return "CatalogStep is already archived.";
        return new CatalogStepEvent[] { new CatalogStepArchived(state.Id) };
    }

    public static CatalogStepState Apply(CatalogStepState? state, CatalogStepEvent e)
        => e switch
        {
            CatalogStepUpserted evt => new CatalogStepState(
                evt.Id, evt.CatalogId, evt.TargetId, evt.StepName, evt.Method, evt.Path, evt.Defaults, false),
            CatalogStepArchived evt => state! with { IsArchived = true },
            _ => throw new InvalidOperationException($"Unknown event: {e.GetType().Name}")
        };

    public static Result<IEnumerable<CatalogStepEvent>> Dispatch(CatalogStepState? state, object command)
        => command switch
        {
            UpsertStep cmd => Handle(state, cmd),
            ArchiveStep cmd => Handle(state, cmd),
            _ => $"Unknown command '{command.GetType().Name}'."
        };

    public static object DeserializeCommand(string type, JsonElement payload)
        => type switch
        {
            nameof(UpsertStep) => payload.Deserialize<UpsertStep>(JsonConfig.Options)!,
            nameof(ArchiveStep) => payload.Deserialize<ArchiveStep>(JsonConfig.Options)!,
            _ => throw new InvalidOperationException($"Unknown command type '{type}'.")
        };

    public static CatalogStepEvent DeserializeEvent(string type, string payload)
        => type switch
        {
            nameof(CatalogStepUpserted) => JsonSerializer.Deserialize<CatalogStepUpserted>(payload, JsonConfig.Options)!,
            nameof(CatalogStepArchived) => JsonSerializer.Deserialize<CatalogStepArchived>(payload, JsonConfig.Options)!,
            _ => throw new InvalidOperationException($"Unknown event type '{type}'.")
        };

    public static readonly AggregateDefinition<CatalogStepState, CatalogStepEvent> Definition = new(
        Dispatch: Dispatch,
        Apply: Apply);
}
