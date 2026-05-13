using Walkthrough.Core;
using Walkthrough.Http;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

// ── Target commands (build) ────────────────────────────────────────────────────

public abstract record TargetCommand() : BuildableRequest;
public abstract record TargetCommand<TOutput>() : BuildableRequest<TOutput>
{
    public override Type AccumulationKey => typeof(TargetCommand);
}

public record CreateTargetOutput(string Id, string Name, string BaseUrl);
public record ArchiveTargetOutput();
public record UnarchiveTargetOutput();
public record UpdateTargetOutput(string Name, string BaseUrl);

public record CreateTargetCommand() : TargetCommand<CreateTargetOutput>
{
    public IFieldValue<string> Id      { get; init; } = Generated(() => Guid.NewGuid().ToString());
    public IFieldValue<string> Name    { get; init; } = Generators.RandomName();
    public IFieldValue<string> BaseUrl { get; init; } = Static("http://localhost:5020");
}

public record ArchiveTargetCommand()   : TargetCommand<ArchiveTargetOutput>;
public record UnarchiveTargetCommand() : TargetCommand<UnarchiveTargetOutput>;

public record UpdateTargetCommand() : TargetCommand<UpdateTargetOutput>
{
    public IFieldValue<string> Name    { get; init; } = Generators.RandomName();
    public IFieldValue<string> BaseUrl { get; init; } = Static("http://updated.com");
}

// ── POST /targets/commands ─────────────────────────────────────────────────────

public record CommandSuccess(int Index, string AggregateId, string[] Events);

public record PostTargetCommandsRequest() : HttpWorkflowRequest<CommandSuccess[]>("postTargetCommands")
{
    public IFieldValue<string> AggregateId { get; init; } = From(ctx =>
        ctx.HasCapture("CreateTargetCommand") ? ctx.Get<CreateTargetOutput>("CreateTargetCommand").Id :
        ctx.HasCapture("getTarget")           ? ctx.Get<TargetResponse>("getTarget").Id :
                                                ctx.Get<CommandSuccess[]>("postTargetCommands")[0].AggregateId);
    public IFieldValue<List<object>> Commands    { get; init; } = From(ctx => ctx.GetAccumulated<TargetCommand>());
}

public class PostTargetCommandsStep : HttpStep<PostTargetCommandsRequest, CommandSuccess[]>
{
    public override HttpMethod Method => HttpMethod.Post;
    public override string     Path   => "/targets/commands";

    private static string CommandType(object cmd) => cmd switch
    {
        CreateTargetOutput    => "CreateTarget",
        ArchiveTargetOutput   => "ArchiveTarget",
        UnarchiveTargetOutput => "UnarchiveTarget",
        UpdateTargetOutput    => "UpdateTarget",
        _                     => throw new InvalidOperationException($"Unknown command type: {cmd.GetType().Name}")
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

// ── GET /targets/{id} ──────────────────────────────────────────────────────────

public record TargetResponse(string Id, string Name, string BaseUrl, bool IsArchived, string? CreatedAt);

public record GetTargetRequest() : HttpWorkflowRequest<TargetResponse>("getTarget")
{
    public IFieldValue<string> Id { get; init; } =
        From(ctx => ctx.Get<CommandSuccess[]>("postTargetCommands")[0].AggregateId);
}

public class GetTargetStep : HttpStep<GetTargetRequest, TargetResponse>
{
    public override HttpMethod Method => HttpMethod.Get;
    public override string     Path   => "/targets/{id}";
}

// ── GET /targets ───────────────────────────────────────────────────────────────

public record ListTargetsRequest() : HttpWorkflowRequest<TargetResponse[]>("listTargets")
{
    public IFieldValue<string> ShowArchived { get; init; } = Static("false");
}

public class ListTargetsStep : HttpStep<ListTargetsRequest, TargetResponse[]>
{
    public override HttpMethod Method => HttpMethod.Get;
    public override string     Path   => "/targets";

    public override Dictionary<string, string> MapQuery(Dictionary<string, object?> resolvedFields) => new()
    {
        ["showArchived"] = resolvedFields["ShowArchived"]?.ToString() ?? "false"
    };
}
