using CommandFramework.Core;
using static StepWise.Management.Domain.Targets.TargetEvent;
using static StepWise.Management.Domain.Targets.TargetCommands;

namespace StepWise.Management.Domain.Targets;

// State
public record TargetState(string Id, string Name, string BaseUrl, bool IsArchived);

// Events
public abstract record TargetEvent
{
    public record TargetCreated(string Id, string Name, string BaseUrl) : TargetEvent;
    public record TargetUpdated(string Id, string Name, string BaseUrl) : TargetEvent;
    public record TargetArchived(string Id) : TargetEvent;
    public record TargetUnarchived(string Id) : TargetEvent;
}

// Commands
public abstract record TargetCommands
{
    public record CreateTarget(string Id, string Name, string BaseUrl) : TargetCommands;
    public record UpdateTarget(string Name, string BaseUrl) : TargetCommands;
    public record ArchiveTarget() : TargetCommands;
    public record UnarchiveTarget() : TargetCommands;
}

public static class TargetAggregate
{
    public static Result<IEnumerable<TargetEvent>> Handle(TargetState? state, CreateTarget cmd)
    {
        if (state != null) return "Target already exists.";
        if (string.IsNullOrWhiteSpace(cmd.Name)) return "Name is required.";
        if (string.IsNullOrWhiteSpace(cmd.BaseUrl)) return "BaseUrl is required.";
        return new TargetEvent[] { new TargetCreated(cmd.Id, cmd.Name, cmd.BaseUrl) };
    }

    public static Result<IEnumerable<TargetEvent>> Handle(TargetState state, UpdateTarget cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name)) return "Name is required.";
        if (string.IsNullOrWhiteSpace(cmd.BaseUrl)) return "BaseUrl is required.";
        return new TargetEvent[] { new TargetUpdated(state.Id, cmd.Name, cmd.BaseUrl) };
    }

    public static Result<IEnumerable<TargetEvent>> Handle(TargetState state, ArchiveTarget _)
    {
        if (state.IsArchived) return "Target is already archived.";
        return new TargetEvent[] { new TargetArchived(state.Id) };
    }

    public static Result<IEnumerable<TargetEvent>> Handle(TargetState state, UnarchiveTarget _)
    {
        if (!state.IsArchived) return "Target is not archived.";
        return new TargetEvent[] { new TargetUnarchived(state.Id) };
    }

    public static TargetState Apply(TargetState? state, TargetEvent e)
        => e switch
        {
            TargetCreated evt => new TargetState(evt.Id, evt.Name, evt.BaseUrl, false),
            TargetUpdated evt => state! with { Name = evt.Name, BaseUrl = evt.BaseUrl },
            TargetArchived => state! with { IsArchived = true },
            TargetUnarchived => state! with { IsArchived = false },
            _ => throw new InvalidOperationException($"Unknown event: {e.GetType().Name}")
        };

    public static Result<IEnumerable<TargetEvent>> Dispatch(TargetState? state, object command)
        => command switch
        {
            CreateTarget cmd => Handle(state, cmd),
            UpdateTarget cmd when state != null => Handle(state, cmd),
            ArchiveTarget cmd when state != null => Handle(state, cmd),
            UnarchiveTarget cmd when state != null => Handle(state, cmd),
            _ when state == null => "Target does not exist.",
            _ => $"Unknown command '{command.GetType().Name}'."
        };

    public static readonly AggregateDefinition<TargetState, TargetEvent> Definition = new(
        Dispatch: Dispatch,
        Apply: Apply);
}
