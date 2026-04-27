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
    bool IsArchived,
    JsonElement? RequestShape = null,
    JsonElement? ResponseShape = null,
    bool IsPolling = false,
    int? RetryCount = null,
    int? RetryDurationMs = null);

// Events
public abstract record CatalogStepEvent;
public record CatalogStepUpserted(
    string Id,
    string CatalogId,
    string TargetId,
    string StepName,
    string Method,
    string Path,
    JsonElement? Defaults,
    JsonElement? RequestShape = null,
    JsonElement? ResponseShape = null,
    bool IsPolling = false,
    int? RetryCount = null,
    int? RetryDurationMs = null) : CatalogStepEvent;
public record CatalogStepArchived(string Id) : CatalogStepEvent;
public record CatalogStepUnarchived(string Id) : CatalogStepEvent;

// Commands
public record UpsertStep(
    string Id,
    string CatalogId,
    string StepName,
    string TargetId,
    string Method,
    string Path,
    JsonElement? Defaults = null,
    JsonElement? RequestShape = null,
    JsonElement? ResponseShape = null,
    bool IsPolling = false,
    int? RetryCount = null,
    int? RetryDurationMs = null);
public record ArchiveStep();
public record UnarchiveStep();

public static class CatalogStepAggregate
{
    public static Result<IEnumerable<CatalogStepEvent>> Handle(CatalogStepState? state, UpsertStep cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.CatalogId)) return "CatalogId is required.";
        if (string.IsNullOrWhiteSpace(cmd.StepName)) return "StepName is required.";
        if (string.IsNullOrWhiteSpace(cmd.TargetId)) return "TargetId is required.";
        if (string.IsNullOrWhiteSpace(cmd.Method)) return "Method is required.";
        if (string.IsNullOrWhiteSpace(cmd.Path)) return "Path is required.";
        return new CatalogStepEvent[]
        {
            new CatalogStepUpserted(
                cmd.Id, cmd.CatalogId, cmd.TargetId, cmd.StepName, cmd.Method, cmd.Path,
                cmd.Defaults, cmd.RequestShape, cmd.ResponseShape,
                cmd.IsPolling, cmd.RetryCount, cmd.RetryDurationMs)
        };
    }

    public static Result<IEnumerable<CatalogStepEvent>> Handle(CatalogStepState state, ArchiveStep _)
    {
        if (state.IsArchived) return "CatalogStep is already archived.";
        return new CatalogStepEvent[] { new CatalogStepArchived(state.Id) };
    }

    public static Result<IEnumerable<CatalogStepEvent>> Handle(CatalogStepState state, UnarchiveStep _)
    {
        if (!state.IsArchived) return "CatalogStep is not archived.";
        return new CatalogStepEvent[] { new CatalogStepUnarchived(state.Id) };
    }

    public static CatalogStepState Apply(CatalogStepState? state, CatalogStepEvent e)
        => e switch
        {
            CatalogStepUpserted evt => new CatalogStepState(
                evt.Id, evt.CatalogId, evt.TargetId, evt.StepName, evt.Method, evt.Path, evt.Defaults, false,
                evt.RequestShape, evt.ResponseShape, evt.IsPolling, evt.RetryCount, evt.RetryDurationMs),
            CatalogStepArchived => state! with { IsArchived = true },
            CatalogStepUnarchived => state! with { IsArchived = false },
            _ => throw new InvalidOperationException($"Unknown event: {e.GetType().Name}")
        };

    public static Result<IEnumerable<CatalogStepEvent>> Dispatch(CatalogStepState? state, object command)
        => command switch
        {
            UpsertStep cmd => Handle(state, cmd),
            ArchiveStep cmd when state != null => Handle(state, cmd),
            UnarchiveStep cmd when state != null => Handle(state, cmd),
            _ when state == null => "CatalogStep does not exist.",
            _ => $"Unknown command '{command.GetType().Name}'."
        };

    public static object DeserializeCommand(string type, JsonElement payload)
        => type switch
        {
            nameof(UpsertStep) => payload.Deserialize<UpsertStep>(JsonConfig.Options)!,
            nameof(ArchiveStep) => payload.Deserialize<ArchiveStep>(JsonConfig.Options)!,
            nameof(UnarchiveStep) => payload.Deserialize<UnarchiveStep>(JsonConfig.Options)!,
            _ => throw new InvalidOperationException($"Unknown command type '{type}'.")
        };

    public static CatalogStepEvent DeserializeEvent(string type, string payload)
        => type switch
        {
            nameof(CatalogStepUpserted) => JsonSerializer.Deserialize<CatalogStepUpserted>(payload, JsonConfig.Options)!,
            nameof(CatalogStepArchived) => JsonSerializer.Deserialize<CatalogStepArchived>(payload, JsonConfig.Options)!,
            nameof(CatalogStepUnarchived) => JsonSerializer.Deserialize<CatalogStepUnarchived>(payload, JsonConfig.Options)!,
            _ => throw new InvalidOperationException($"Unknown event type '{type}'.")
        };

    public static readonly AggregateDefinition<CatalogStepState, CatalogStepEvent> Definition = new(
        Dispatch: Dispatch,
        Apply: Apply);
}
