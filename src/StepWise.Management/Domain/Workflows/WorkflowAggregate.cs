using System.Text.Json;
using CommandFramework.Core;
using Walkthrough.Json;
using StepWise.Management;

namespace StepWise.Management.Domain.Workflows;

// Step model stored in the workflow aggregate
public record WorkflowStep
{
    public string Id { get; init; } = "";
    public string CatalogStepId { get; init; } = "";
    public string CatalogId { get; init; } = "";
    public JsonElement? Defaults { get; init; }
}

// State
public record WorkflowState(
    string Id,
    string Name,
    List<WorkflowStep> Steps,
    List<AssertionDefinition> Assertions,
    bool IsArchived,
    string Description = "");

// Events
public abstract record WorkflowEvent;
public record WorkflowCreated(string Id, string Name) : WorkflowEvent;
public record WorkflowRenamed(string Id, string Name) : WorkflowEvent;
public record WorkflowDescriptionUpdated(string Id, string Description) : WorkflowEvent;
public record WorkflowStepAppended(WorkflowStep Step) : WorkflowEvent;
public record WorkflowStepInsertedBefore(string BeforeId, WorkflowStep Step) : WorkflowEvent;
public record WorkflowStepRemoved(string StepId) : WorkflowEvent;
public record WorkflowStepDefaultsSet(string StepId, JsonElement? Defaults) : WorkflowEvent;
public record AssertionAdded(AssertionDefinition AssertionDefinition) : WorkflowEvent;
public record WorkflowArchived(string Id) : WorkflowEvent;
public record WorkflowUnarchived(string Id) : WorkflowEvent;

// Commands
public record CreateWorkflow(string Id, string Name);
public record RenameWorkflow(string Name);
public record UpdateDescription(string Description);
public record AppendStep(string Id, string CatalogStepId, string CatalogId, JsonElement? Defaults = null);
public record InsertStepBefore(string BeforeId, string Id, string CatalogStepId, string CatalogId, JsonElement? Defaults = null);
public record RemoveStep(string Id);
public record SetStepDefaults(string Id, JsonElement? Defaults);
public record AddAssertion(AssertionDefinition Assertion);
public record ArchiveWorkflow();
public record UnarchiveWorkflow();

public static class WorkflowAggregate
{
    public static Result<IEnumerable<WorkflowEvent>> Handle(WorkflowState? state, CreateWorkflow cmd)
    {
        if (state != null) return "Workflow already exists.";
        if (string.IsNullOrWhiteSpace(cmd.Name)) return "Workflow Name is required.";
        return new WorkflowEvent[] { new WorkflowCreated(cmd.Id, cmd.Name) };
    }

    public static Result<IEnumerable<WorkflowEvent>> Handle(WorkflowState state, RenameWorkflow cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name)) return "Name is required.";
        return new WorkflowEvent[] { new WorkflowRenamed(state.Id, cmd.Name) };
    }

    public static Result<IEnumerable<WorkflowEvent>> Handle(WorkflowState state, UpdateDescription cmd)
        => new WorkflowEvent[] { new WorkflowDescriptionUpdated(state.Id, cmd.Description ?? "") };

    public static Result<IEnumerable<WorkflowEvent>> Handle(WorkflowState state, AppendStep cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Id)) return "Step Id is required.";
        if (string.IsNullOrWhiteSpace(cmd.CatalogStepId)) return "CatalogStepId is required.";
        var step = new WorkflowStep { Id = cmd.Id, CatalogStepId = cmd.CatalogStepId, CatalogId = cmd.CatalogId, Defaults = cmd.Defaults };
        return new WorkflowEvent[] { new WorkflowStepAppended(step) };
    }

    public static Result<IEnumerable<WorkflowEvent>> Handle(WorkflowState state, InsertStepBefore cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Id)) return "Step Id is required.";
        if (string.IsNullOrWhiteSpace(cmd.CatalogStepId)) return "CatalogStepId is required.";
        if (!state.Steps.Any(s => s.Id == cmd.BeforeId))
            return $"Step '{cmd.BeforeId}' not found.";
        var step = new WorkflowStep { Id = cmd.Id, CatalogStepId = cmd.CatalogStepId, CatalogId = cmd.CatalogId, Defaults = cmd.Defaults };
        return new WorkflowEvent[] { new WorkflowStepInsertedBefore(cmd.BeforeId, step) };
    }

    public static Result<IEnumerable<WorkflowEvent>> Handle(WorkflowState state, RemoveStep cmd)
    {
        if (!state.Steps.Any(s => s.Id == cmd.Id))
            return $"Step '{cmd.Id}' not found.";
        return new WorkflowEvent[] { new WorkflowStepRemoved(cmd.Id) };
    }

    public static Result<IEnumerable<WorkflowEvent>> Handle(WorkflowState state, SetStepDefaults cmd)
    {
        if (!state.Steps.Any(s => s.Id == cmd.Id))
            return $"Step '{cmd.Id}' not found.";
        return new WorkflowEvent[] { new WorkflowStepDefaultsSet(cmd.Id, cmd.Defaults) };
    }

    public static Result<IEnumerable<WorkflowEvent>> Handle(WorkflowState state, AddAssertion cmd)
        => new WorkflowEvent[] { new AssertionAdded(cmd.Assertion) };

    public static Result<IEnumerable<WorkflowEvent>> Handle(WorkflowState state, ArchiveWorkflow _)
    {
        if (state.IsArchived) return "Workflow is already archived.";
        return new WorkflowEvent[] { new WorkflowArchived(state.Id) };
    }

    public static Result<IEnumerable<WorkflowEvent>> Handle(WorkflowState state, UnarchiveWorkflow _)
    {
        if (!state.IsArchived) return "Workflow is not archived.";
        return new WorkflowEvent[] { new WorkflowUnarchived(state.Id) };
    }

    public static WorkflowState Apply(WorkflowState? state, WorkflowEvent e)
    {
        switch (e)
        {
            case WorkflowCreated evt:
                return new WorkflowState(evt.Id, evt.Name, new List<WorkflowStep>(), new List<AssertionDefinition>(), false);

            case WorkflowDescriptionUpdated evt:
                return state! with { Description = evt.Description };

            case WorkflowRenamed evt:
                return state! with { Name = evt.Name };

            case WorkflowStepAppended evt:
                return state! with { Steps = new List<WorkflowStep>(state.Steps) { evt.Step } };

            case WorkflowStepInsertedBefore evt:
            {
                var steps = new List<WorkflowStep>(state!.Steps);
                var idx = steps.FindIndex(s => s.Id == evt.BeforeId);
                steps.Insert(idx, evt.Step);
                return state with { Steps = steps };
            }

            case WorkflowStepRemoved evt:
                return state! with
                {
                    Steps = state.Steps.Where(s => s.Id != evt.StepId).ToList()
                };

            case WorkflowStepDefaultsSet evt:
            {
                var steps = state!.Steps.Select(s =>
                    s.Id == evt.StepId ? s with { Defaults = evt.Defaults } : s).ToList();
                return state with { Steps = steps };
            }

            case AssertionAdded evt:
                return state! with
                {
                    Assertions = new List<AssertionDefinition>(state.Assertions) { evt.AssertionDefinition }
                };

            case WorkflowArchived:
                return state! with { IsArchived = true };

            case WorkflowUnarchived:
                return state! with { IsArchived = false };

            default:
                throw new InvalidOperationException($"Unknown event: {e.GetType().Name}");
        }
    }

    public static Result<IEnumerable<WorkflowEvent>> Dispatch(WorkflowState? state, object command)
        => command switch
        {
            CreateWorkflow cmd => Handle(state, cmd),
            RenameWorkflow cmd when state != null => Handle(state, cmd),
            UpdateDescription cmd when state != null => Handle(state, cmd),
            AppendStep cmd when state != null => Handle(state, cmd),
            InsertStepBefore cmd when state != null => Handle(state, cmd),
            RemoveStep cmd when state != null => Handle(state, cmd),
            SetStepDefaults cmd when state != null => Handle(state, cmd),
            AddAssertion cmd when state != null => Handle(state, cmd),
            ArchiveWorkflow cmd when state != null => Handle(state, cmd),
            UnarchiveWorkflow cmd when state != null => Handle(state, cmd),
            _ when state == null => "Workflow does not exist.",
            _ => $"Unknown command '{command.GetType().Name}'."
        };

    public static object DeserializeCommand(string type, JsonElement payload)
        => type switch
        {
            nameof(CreateWorkflow) => payload.Deserialize<CreateWorkflow>(JsonConfig.Options)!,
            nameof(RenameWorkflow) => payload.Deserialize<RenameWorkflow>(JsonConfig.Options)!,
            nameof(UpdateDescription) => payload.Deserialize<UpdateDescription>(JsonConfig.Options)!,
            nameof(AppendStep) => payload.Deserialize<AppendStep>(JsonConfig.Options)!,
            nameof(InsertStepBefore) => payload.Deserialize<InsertStepBefore>(JsonConfig.Options)!,
            nameof(RemoveStep) => payload.Deserialize<RemoveStep>(JsonConfig.Options)!,
            nameof(SetStepDefaults) => payload.Deserialize<SetStepDefaults>(JsonConfig.Options)!,
            nameof(AddAssertion) => payload.Deserialize<AddAssertion>(JsonConfig.Options)!,
            nameof(ArchiveWorkflow) => payload.Deserialize<ArchiveWorkflow>(JsonConfig.Options)!,
            nameof(UnarchiveWorkflow) => payload.Deserialize<UnarchiveWorkflow>(JsonConfig.Options)!,
            _ => throw new InvalidOperationException($"Unknown command type '{type}'.")
        };

    public static WorkflowEvent DeserializeEvent(string type, string payload)
        => type switch
        {
            nameof(WorkflowCreated) => JsonSerializer.Deserialize<WorkflowCreated>(payload, JsonConfig.Options)!,
            nameof(WorkflowRenamed) => JsonSerializer.Deserialize<WorkflowRenamed>(payload, JsonConfig.Options)!,
            nameof(WorkflowDescriptionUpdated) => JsonSerializer.Deserialize<WorkflowDescriptionUpdated>(payload, JsonConfig.Options)!,
            nameof(WorkflowStepAppended) => JsonSerializer.Deserialize<WorkflowStepAppended>(payload, JsonConfig.Options)!,
            nameof(WorkflowStepInsertedBefore) => JsonSerializer.Deserialize<WorkflowStepInsertedBefore>(payload, JsonConfig.Options)!,
            nameof(WorkflowStepRemoved) => JsonSerializer.Deserialize<WorkflowStepRemoved>(payload, JsonConfig.Options)!,
            nameof(WorkflowStepDefaultsSet) => JsonSerializer.Deserialize<WorkflowStepDefaultsSet>(payload, JsonConfig.Options)!,
            nameof(AssertionAdded) => JsonSerializer.Deserialize<AssertionAdded>(payload, JsonConfig.Options)!,
            nameof(WorkflowArchived) => JsonSerializer.Deserialize<WorkflowArchived>(payload, JsonConfig.Options)!,
            nameof(WorkflowUnarchived) => JsonSerializer.Deserialize<WorkflowUnarchived>(payload, JsonConfig.Options)!,
            _ => throw new InvalidOperationException($"Unknown event type '{type}'.")
        };

    public static readonly AggregateDefinition<WorkflowState, WorkflowEvent> Definition = new(
        Dispatch: Dispatch,
        Apply: Apply);
}
