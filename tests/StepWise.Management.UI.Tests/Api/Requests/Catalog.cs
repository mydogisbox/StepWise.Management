using Walkthrough.Core;
using Walkthrough.Http;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

// ── Catalog commands (build) ───────────────────────────────────────────────────

public abstract record CatalogCommand() : BuildableRequest;
public abstract record CatalogCommand<TOutput>() : BuildableRequest<TOutput>
{
    public override Type AccumulationKey => typeof(CatalogCommand);
}

public record CreateCatalogOutput(string Id, string Name);
public record UpdateCatalogOutput(string Name, string Description);
public record ArchiveCatalogOutput();
public record UnarchiveCatalogOutput();

public record CreateCatalogCommand() : CatalogCommand<CreateCatalogOutput>
{
    public IFieldValue<string> Id   { get; init; } = Generated(() => Guid.NewGuid().ToString());
    public IFieldValue<string> Name { get; init; } = Generators.RandomName();
}

public record UpdateCatalogCommand() : CatalogCommand<UpdateCatalogOutput>
{
    public IFieldValue<string> Name        { get; init; } = Generators.RandomName();
    public IFieldValue<string> Description { get; init; } = Static("");
}

public record ArchiveCatalogCommand()   : CatalogCommand<ArchiveCatalogOutput>;
public record UnarchiveCatalogCommand() : CatalogCommand<UnarchiveCatalogOutput>;

// ── POST /catalogs/commands ────────────────────────────────────────────────────

public record CatalogCommandSuccess(int Index, string AggregateId, string[] Events);

public record PostCatalogCommandsRequest() : WorkflowRequest<CatalogCommandSuccess[], PostCatalogCommandsRequest>, IWorkflowRequest
{
    public static string StepName => "postCatalogCommands";
    public IFieldValue<string> AggregateId { get; init; } = From(ctx =>
        ctx.HasCapture("CreateCatalogCommand") ? ctx.Get<CreateCatalogOutput>("CreateCatalogCommand").Id :
                                                 ctx.Get<CatalogCommandSuccess[]>("postCatalogCommands")[0].AggregateId);
    public IFieldValue<List<object>> Commands { get; init; } = From(ctx => ctx.GetAccumulated<CatalogCommand>());
}

public class PostCatalogCommandsStep : HttpStep<PostCatalogCommandsRequest, CatalogCommandSuccess[], PostCatalogCommandsStep>, IHttpStep
{
    public static HttpMethod Method => HttpMethod.Post;
    public static string     Path   => "/catalogs/commands";

    private static string CommandType(object cmd) => cmd switch
    {
        CreateCatalogOutput    => "CreateCatalog",
        UpdateCatalogOutput    => "UpdateCatalog",
        ArchiveCatalogOutput   => "ArchiveCatalog",
        UnarchiveCatalogOutput => "UnarchiveCatalog",
        _                      => throw new InvalidOperationException($"Unknown command type: {cmd.GetType().Name}")
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

// ── GET /catalogs/{id} ────────────────────────────────────────────────────────

public record CatalogResponse(string Id, string Name, string? Description, bool IsArchived, string? CreatedAt);

public record GetCatalogRequest() : WorkflowRequest<CatalogResponse, GetCatalogRequest>, IWorkflowRequest
{
    public static string StepName => "getCatalog";
    public IFieldValue<string> Id { get; init; } =
        From(ctx => ctx.Get<CatalogCommandSuccess[]>("postCatalogCommands")[0].AggregateId);
}

public class GetCatalogStep : HttpStep<GetCatalogRequest, CatalogResponse, GetCatalogStep>, IHttpStep
{
    public static HttpMethod Method => HttpMethod.Get;
    public static string     Path   => "/catalogs/{id}";
}

// ── GET /catalogs ─────────────────────────────────────────────────────────────

public record ListCatalogsRequest() : WorkflowRequest<CatalogResponse[], ListCatalogsRequest>, IWorkflowRequest
{
    public static string StepName => "listCatalogs";
    public IFieldValue<string> ShowArchived { get; init; } = Static("false");
}

public class ListCatalogsStep : HttpStep<ListCatalogsRequest, CatalogResponse[], ListCatalogsStep>, IHttpStep
{
    public static HttpMethod Method => HttpMethod.Get;
    public static string     Path   => "/catalogs";

    public override Dictionary<string, string> MapQuery(Dictionary<string, object?> resolvedFields) => new()
    {
        ["showArchived"] = resolvedFields["ShowArchived"]?.ToString() ?? "false"
    };
}
