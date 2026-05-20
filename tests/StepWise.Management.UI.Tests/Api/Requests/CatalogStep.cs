using System.Text.Json;
using Walkthrough.Core;
using Walkthrough.Http;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

// ── CatalogStep commands (build) ───────────────────────────────────────────────

public abstract record CatalogStepCommand() : BuildableRequest;
public abstract record CatalogStepCommand<TOutput>() : BuildableRequest<TOutput>
{
    public override Type AccumulationKey => typeof(CatalogStepCommand);
}

public record UpsertStepOutput(string Id, string CatalogId, string StepName, string TargetId, string Method, string Path, object? Defaults = null, object? RequestShape = null, object? ResponseShape = null, bool? IsPolling = null, int? RetryCount = null, int? RetryDurationMs = null, object? Headers = null);
public record ArchiveStepOutput();
public record UnarchiveStepOutput();

public record UpsertStepCommand() : CatalogStepCommand<UpsertStepOutput>
{
    public IFieldValue<string>  Id        { get; init; } = Generated(() => Guid.NewGuid().ToString());
    public IFieldValue<string>  CatalogId { get; init; } = From(ctx => ctx.Get<CreateCatalogOutput>("CreateCatalogCommand").Id);
    public IFieldValue<string>  StepName  { get; init; } = Generators.RandomName();
    public IFieldValue<string>  TargetId  { get; init; } = From(ctx =>
        ctx.Get<PagedResponse<TargetResponse>>("listTargets").Items.Single(t => t.Name == ctx.Get<CreateTargetOutput>("CreateTargetCommand").Name).Id);
    public IFieldValue<string>  Method          { get; init; } = Static("GET");
    public IFieldValue<string>  Path            { get; init; } = Static("/api/ping");
    public IFieldValue<object?> Defaults        { get; init; } = Static<object?>(null);
    public IFieldValue<object?> RequestShape    { get; init; } = Static<object?>(null);
    public IFieldValue<object?> ResponseShape   { get; init; } = Static<object?>(null);
    public IFieldValue<bool?>   IsPolling       { get; init; } = Static<bool?>(null);
    public IFieldValue<int?>    RetryCount      { get; init; } = Static<int?>(null);
    public IFieldValue<int?>    RetryDurationMs { get; init; } = Static<int?>(null);
    public IFieldValue<object?> Headers         { get; init; } = Static<object?>(null);
}

public record ArchiveStepCommand()   : CatalogStepCommand<ArchiveStepOutput>;
public record UnarchiveStepCommand() : CatalogStepCommand<UnarchiveStepOutput>;

// ── POST /catalog-steps/commands ──────────────────────────────────────────────

public record CatalogStepCommandSuccess(int Index, string AggregateId, string[] Events);

public record PostCatalogStepCommandsRequest() : WorkflowRequest<CatalogStepCommandSuccess[], PostCatalogStepCommandsRequest>, IWorkflowRequest
{
    public static string StepName => "postCatalogStepCommands";
    public IFieldValue<string> AggregateId { get; init; } = From(ctx =>
        ctx.HasCapture("UpsertStepCommand") ? ctx.Get<UpsertStepOutput>("UpsertStepCommand").Id :
                                              ctx.Get<CatalogStepCommandSuccess[]>("postCatalogStepCommands")[0].AggregateId);
    public IFieldValue<List<object>> Commands { get; init; } = From(ctx => ctx.GetAccumulated<CatalogStepCommand>());
}

public class PostCatalogStepCommandsStep : HttpStep<PostCatalogStepCommandsRequest, CatalogStepCommandSuccess[], PostCatalogStepCommandsStep>, IHttpStep
{
    public static HttpMethod Method => HttpMethod.Post;
    public static string     Path   => "/catalog-steps/commands";

    private static string CommandType(object cmd) => cmd switch
    {
        UpsertStepOutput    => "UpsertStep",
        ArchiveStepOutput   => "ArchiveStep",
        UnarchiveStepOutput => "UnarchiveStep",
        _                   => throw new InvalidOperationException($"Unknown command type: {cmd.GetType().Name}")
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

// ── GET /catalog-steps/{id} ───────────────────────────────────────────────────

public record CatalogStepResponse(string Id, string CatalogId, string StepName, string TargetId, string Method, string Path, bool IsArchived, JsonElement? Defaults, JsonElement? RequestShape, JsonElement? ResponseShape, bool? IsPolling, int? RetryCount, int? RetryDurationMs);

public record GetCatalogStepRequest() : WorkflowRequest<CatalogStepResponse, GetCatalogStepRequest>, IWorkflowRequest
{
    public static string StepName => "getCatalogStep";
    public IFieldValue<string> Id { get; init; } =
        From(ctx => ctx.Get<CatalogStepCommandSuccess[]>("postCatalogStepCommands")[0].AggregateId);
}

public class GetCatalogStepStep : HttpStep<GetCatalogStepRequest, CatalogStepResponse, GetCatalogStepStep>, IHttpStep
{
    public static HttpMethod Method => HttpMethod.Get;
    public static string     Path   => "/catalog-steps/{id}";
}

// ── GET /catalog-steps ────────────────────────────────────────────────────────

public record ListCatalogStepsRequest() : WorkflowRequest<CatalogStepResponse[], ListCatalogStepsRequest>, IWorkflowRequest
{
    public static string StepName => "listCatalogSteps";
    public IFieldValue<string> CatalogId    { get; init; } = From(ctx => ctx.Get<CreateCatalogOutput>("CreateCatalogCommand").Id);
    public IFieldValue<string> ShowArchived { get; init; } = Static("false");
}

public class ListCatalogStepsStep : HttpStep<ListCatalogStepsRequest, CatalogStepResponse[], ListCatalogStepsStep>, IHttpStep
{
    public static HttpMethod Method => HttpMethod.Get;
    public static string     Path   => "/catalog-steps";

    public override Dictionary<string, string> MapQuery(Dictionary<string, object?> resolvedFields) => new()
    {
        ["catalogId"]    = resolvedFields["CatalogId"]?.ToString() ?? "",
        ["showArchived"] = resolvedFields["ShowArchived"]?.ToString() ?? "false"
    };
}
