using System.Text.Json;
using Walkthrough.Core;
using Walkthrough.Http;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

// ── Workflow commands (build) ──────────────────────────────────────────────────

public abstract record WorkflowCommand() : BuildableRequest;
public abstract record WorkflowCommand<TOutput>() : BuildableRequest<TOutput>
{
    public override Type AccumulationKey => typeof(WorkflowCommand);
}

public record CreateWorkflowOutput(string Id, string Name);
public record RenameWorkflowOutput(string Name);
public record AppendStepOutput(string Id, string CatalogStepId, string CatalogId, object? Defaults = null);
public record InsertStepBeforeOutput(string Id, string BeforeId, string CatalogStepId, string CatalogId, object? Defaults = null);
public record RemoveStepOutput(string Id);
public record SetStepDefaultsOutput(string Id, object? Defaults = null);
public record AddAssertionOutput(object Assertion);
public record ArchiveWorkflowOutput();
public record UnarchiveWorkflowOutput();
public record UpdateDescriptionOutput(string Description);

public record CreateWorkflowCommand() : WorkflowCommand<CreateWorkflowOutput>
{
    public IFieldValue<string> Id   { get; init; } = Generated(() => Guid.NewGuid().ToString());
    public IFieldValue<string> Name { get; init; } = Generators.RandomName();
}

public record RenameWorkflowCommand() : WorkflowCommand<RenameWorkflowOutput>
{
    public IFieldValue<string> Name { get; init; } = Generators.RandomName();
}

public record AppendStepCommand() : WorkflowCommand<AppendStepOutput>
{
    public IFieldValue<string>  Id            { get; init; } = Generated(() => Guid.NewGuid().ToString());
    public IFieldValue<string>  CatalogStepId { get; init; } = From(ctx => ctx.Get<UpsertStepOutput>("UpsertStepCommand").Id);
    public IFieldValue<string>  CatalogId     { get; init; } = From(ctx => ctx.Get<UpsertStepOutput>("UpsertStepCommand").CatalogId);
    public IFieldValue<object?> Defaults      { get; init; } = Static<object?>(null);
}

public record InsertStepBeforeCommand() : WorkflowCommand<InsertStepBeforeOutput>
{
    public IFieldValue<string>  Id            { get; init; } = Generated(() => Guid.NewGuid().ToString());
    public IFieldValue<string>  BeforeId      { get; init; } = From(ctx => ctx.Get<AppendStepOutput>("AppendStepCommand").Id);
    public IFieldValue<string>  CatalogStepId { get; init; } = From(ctx => ctx.Get<UpsertStepOutput>("UpsertStepCommand").Id);
    public IFieldValue<string>  CatalogId     { get; init; } = From(ctx => ctx.Get<UpsertStepOutput>("UpsertStepCommand").CatalogId);
    public IFieldValue<object?> Defaults      { get; init; } = Static<object?>(null);
}

public record RemoveStepCommand() : WorkflowCommand<RemoveStepOutput>
{
    public IFieldValue<string> Id { get; init; } = From(ctx => ctx.Get<AppendStepOutput>("AppendStepCommand").Id);
}

public record SetStepDefaultsCommand() : WorkflowCommand<SetStepDefaultsOutput>
{
    public IFieldValue<string>  Id       { get; init; } = From(ctx => ctx.Get<AppendStepOutput>("AppendStepCommand").Id);
    public IFieldValue<object?> Defaults { get; init; } = Static<object?>(null);
}

public record AddAssertionCommand() : WorkflowCommand<AddAssertionOutput>
{
    public IFieldValue<object> Assertion { get; init; } = Static<object>(new { });
}

public record ArchiveWorkflowCommand()   : WorkflowCommand<ArchiveWorkflowOutput>;
public record UnarchiveWorkflowCommand() : WorkflowCommand<UnarchiveWorkflowOutput>;

public record UpdateDescriptionCommand() : WorkflowCommand<UpdateDescriptionOutput>
{
    public IFieldValue<string> Description { get; init; } = Static("");
}

// ── POST /workflows/commands ───────────────────────────────────────────────────

public record WorkflowCommandSuccess(int Index, string AggregateId, string[] Events);

public record PostWorkflowCommandsRequest() : HttpWorkflowRequest<WorkflowCommandSuccess[]>("postWorkflowCommands")
{
    public IFieldValue<string> AggregateId { get; init; } = From(ctx =>
        ctx.HasCapture("CreateWorkflowCommand") ? ctx.Get<CreateWorkflowOutput>("CreateWorkflowCommand").Id :
                                                  ctx.Get<WorkflowCommandSuccess[]>("postWorkflowCommands")[0].AggregateId);
    public IFieldValue<List<object>> Commands { get; init; } = From(ctx => ctx.GetAccumulated<WorkflowCommand>());
}

public class PostWorkflowCommandsStep : HttpStep<PostWorkflowCommandsRequest, WorkflowCommandSuccess[]>
{
    public override HttpMethod Method => HttpMethod.Post;
    public override string     Path   => "/workflows/commands";

    private static string CommandType(object cmd) => cmd switch
    {
        CreateWorkflowOutput    => "CreateWorkflow",
        RenameWorkflowOutput    => "RenameWorkflow",
        AppendStepOutput        => "AppendStep",
        InsertStepBeforeOutput  => "InsertStepBefore",
        RemoveStepOutput        => "RemoveStep",
        SetStepDefaultsOutput   => "SetStepDefaults",
        AddAssertionOutput      => "AddAssertion",
        ArchiveWorkflowOutput   => "ArchiveWorkflow",
        UnarchiveWorkflowOutput => "UnarchiveWorkflow",
        UpdateDescriptionOutput => "UpdateDescription",
        _                       => throw new InvalidOperationException($"Unknown command type: {cmd.GetType().Name}")
    };

    public override Dictionary<string, object?> MapBody(Dictionary<string, object?> fields) => new()
    {
        ["AggregateId"] = fields["AggregateId"],
        ["Commands"] = ((List<object?>)fields["Commands"]!)
            .Select(cmd => (object)new Dictionary<string, object?>
            {
                ["Type"]    = CommandType(cmd!),
                ["Payload"] = cmd
            })
            .ToList()
    };
}

// ── GET /workflows/{id} ───────────────────────────────────────────────────────

public record WorkflowStepResponse(string Id, string CatalogStepId, string CatalogId, JsonElement? Defaults);
public record WorkflowResponse(string Id, string Name, string? Description, bool IsArchived, WorkflowStepResponse[] Steps, JsonElement[] Assertions);

public record GetWorkflowRequest() : HttpWorkflowRequest<WorkflowResponse>("getWorkflow")
{
    public IFieldValue<string> Id { get; init; } =
        From(ctx => ctx.Get<WorkflowCommandSuccess[]>("postWorkflowCommands")[0].AggregateId);
}

public class GetWorkflowStep : HttpStep<GetWorkflowRequest, WorkflowResponse>
{
    public override HttpMethod Method => HttpMethod.Get;
    public override string     Path   => "/workflows/{id}";
}

// ── GET /workflows ────────────────────────────────────────────────────────────

public record WorkflowSummaryResponse(string Id, string Name, bool IsArchived);

public record ListWorkflowsRequest() : HttpWorkflowRequest<WorkflowSummaryResponse[]>("listWorkflows")
{
    public IFieldValue<string> ShowArchived { get; init; } = Static("false");
}

public class ListWorkflowsStep : HttpStep<ListWorkflowsRequest, WorkflowSummaryResponse[]>
{
    public override HttpMethod Method => HttpMethod.Get;
    public override string     Path   => "/workflows";

    public override Dictionary<string, string> MapQuery(Dictionary<string, object?> resolvedFields) => new()
    {
        ["showArchived"] = resolvedFields["ShowArchived"]?.ToString() ?? "false"
    };
}

// ── POST /api/workflows/{workflowId}/run ──────────────────────────────────────

public record RunWorkflowResponse(string RunId);

public record RunWorkflowRequest() : HttpWorkflowRequest<RunWorkflowResponse>("runWorkflow")
{
    public IFieldValue<string> WorkflowId { get; init; } = From(ctx => ctx.Get<CreateWorkflowOutput>("CreateWorkflowCommand").Id);
    public IFieldValue<string> RunId      { get; init; } = Generated(() => Guid.NewGuid().ToString());
}

public class RunWorkflowStep : HttpStep<RunWorkflowRequest, RunWorkflowResponse>
{
    public override HttpMethod Method => HttpMethod.Post;
    public override string     Path   => "/api/workflows/{workflowId}/run";
}

// ── GET /runs/{runId} ─────────────────────────────────────────────────────────

public record RunResponse(string Id, string WorkflowId, string Status, bool? Passed, JsonElement? Result);

public record GetRunRequest() : HttpWorkflowRequest<RunResponse>("getRun")
{
    public IFieldValue<string> RunId { get; init; } = From(ctx => ctx.Get<RunWorkflowResponse>("runWorkflow").RunId);
}

public class GetRunStep : HttpStep<GetRunRequest, RunResponse>
{
    public override HttpMethod Method => HttpMethod.Get;
    public override string     Path   => "/runs/{runId}";
}

// ── GET /runs ─────────────────────────────────────────────────────────────────

public record RunSummaryResponse(string Id, string WorkflowId, string? WorkflowName, bool? Passed, int? DurationMs);

public record ListRunsRequest() : HttpWorkflowRequest<RunSummaryResponse[]>("listRuns");

public class ListRunsStep : HttpStep<ListRunsRequest, RunSummaryResponse[]>
{
    public override HttpMethod Method => HttpMethod.Get;
    public override string     Path   => "/runs";
}
