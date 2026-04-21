using System.Text.Json;
using CommandFramework.Core;
using StepWise.Management;

namespace StepWise.Management.Domain.Targets;

// State
public record TargetState(string Id, string Name, string BaseUrl, bool IsArchived);

// Events
public abstract record TargetEvent;
public record TargetCreated(string Id, string Name, string BaseUrl) : TargetEvent;
public record TargetArchived(string Id) : TargetEvent;

// Commands
public record CreateTarget(string Id, string Name, string BaseUrl);
public record ArchiveTarget();

public static class TargetAggregate
{
    public static Result<IEnumerable<TargetEvent>> Handle(TargetState? state, CreateTarget cmd)
    {
        if (state != null) return "Target already exists.";
        if (string.IsNullOrWhiteSpace(cmd.Name)) return "Name is required.";
        if (string.IsNullOrWhiteSpace(cmd.BaseUrl)) return "BaseUrl is required.";
        return new TargetEvent[] { new TargetCreated(cmd.Id, cmd.Name, cmd.BaseUrl) };
    }

    public static Result<IEnumerable<TargetEvent>> Handle(TargetState state, ArchiveTarget _)
    {
        if (state.IsArchived) return "Target is already archived.";
        return new TargetEvent[] { new TargetArchived(state.Id) };
    }

    public static TargetState Apply(TargetState? state, TargetEvent e)
        => e switch
        {
            TargetCreated evt => new TargetState(evt.Id, evt.Name, evt.BaseUrl, false),
            TargetArchived evt => state! with { IsArchived = true },
            _ => throw new InvalidOperationException($"Unknown event: {e.GetType().Name}")
        };

    public static Result<IEnumerable<TargetEvent>> Dispatch(TargetState? state, object command)
        => command switch
        {
            CreateTarget cmd => Handle(state, cmd),
            ArchiveTarget cmd when state != null => Handle(state, cmd),
            ArchiveTarget => "Target does not exist.",
            _ => $"Unknown command '{command.GetType().Name}'."
        };

    public static object DeserializeCommand(string type, JsonElement payload)
        => type switch
        {
            nameof(CreateTarget) => payload.Deserialize<CreateTarget>(JsonConfig.Options)!,
            nameof(ArchiveTarget) => payload.Deserialize<ArchiveTarget>(JsonConfig.Options)!,
            _ => throw new InvalidOperationException($"Unknown command type '{type}'.")
        };

    public static TargetEvent DeserializeEvent(string type, string payload)
        => type switch
        {
            nameof(TargetCreated) => JsonSerializer.Deserialize<TargetCreated>(payload, JsonConfig.Options)!,
            nameof(TargetArchived) => JsonSerializer.Deserialize<TargetArchived>(payload, JsonConfig.Options)!,
            _ => throw new InvalidOperationException($"Unknown event type '{type}'.")
        };

    public static readonly AggregateDefinition<TargetState, TargetEvent> Definition = new(
        Dispatch: Dispatch,
        Apply: Apply);
}
