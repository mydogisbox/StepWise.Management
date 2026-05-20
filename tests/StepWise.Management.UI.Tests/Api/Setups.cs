using Walkthrough.Core;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public static class Setups
{
    public static async Task<(UpsertStepOutput AdminStep, UpsertStepOutput ListProductsStep)> ExampleCatalogAsync(WorkflowRunner runner)
    {
        var target = await runner.BuildAsync(new CreateTargetCommand() with { BaseUrl = Static("http://localhost:5010") });
        await runner.ExecuteAsync(new PostTargetCommandsRequest());
        await runner.ExecuteAsync(new ListTargetsRequest() with { Name = Static(target.Name) });

        await runner.BuildAsync(new CreateCatalogCommand());
        await runner.ExecuteAsync(new PostCatalogCommandsRequest());

        var adminStep = await runner.BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("admin-create-product"),
            Method   = Static("POST"),
            Path     = Static("/admin/products"),
            Headers  = Static<object?>(new Dictionary<string, object?>
            {
                ["X-Admin-Key"] = new Dictionary<string, object?> { ["static"] = "admin-secret" }
            }),
            Defaults = Static<object?>(new Dictionary<string, object?>
            {
                ["name"]     = new Dictionary<string, object?> { ["generated"] = "guid" },
                ["category"] = new Dictionary<string, object?> { ["static"] = "electronics" },
                ["price"]    = new Dictionary<string, object?> { ["static"] = 9.99 },
                ["stock"]    = new Dictionary<string, object?> { ["static"] = 10 }
            })
        });
        await runner.ExecuteAsync(new PostCatalogStepCommandsRequest() with
        {
            Commands = Static(new List<object> { adminStep })
        });

        var listStep = await runner.BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("list-products"),
            Method   = Static("GET"),
            Path     = Static("/products")
        });
        await runner.ExecuteAsync(new PostCatalogStepCommandsRequest() with
        {
            Commands = Static(new List<object> { listStep })
        });

        return (adminStep, listStep);
    }

    public static async Task<string> TwoStepWorkflowAsync(WorkflowRunner runner)
    {
        var (adminStep, _) = await ExampleCatalogAsync(runner);

        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());
        await runner.BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(adminStep.Id) });
        await runner.BuildAsync(new AppendStepCommand());
        await runner.ExecuteAsync(new PostWorkflowCommandsRequest());

        return workflow.Name;
    }

    public static async Task<string> CrossReferenceWorkflowAsync(WorkflowRunner runner)
    {
        var (adminStep, _) = await ExampleCatalogAsync(runner);

        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());
        await runner.BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(adminStep.Id) });
        await runner.BuildAsync(new AppendStepCommand());
        await runner.BuildAsync(new AddAssertionCommand() with
        {
            Assertion = Static<object>(new { equal = new object[] { "$list-products.totalCount", "999" } })
        });
        await runner.ExecuteAsync(new PostWorkflowCommandsRequest());

        return workflow.Name;
    }

    public static async Task<string> StoredAssertionWorkflowAsync(WorkflowRunner runner)
    {
        var (adminStep, _) = await ExampleCatalogAsync(runner);

        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());
        await runner.BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(adminStep.Id) });
        await runner.BuildAsync(new AppendStepCommand());
        await runner.BuildAsync(new AddAssertionCommand() with
        {
            Assertion = Static<object>(new { notEmpty = "list-products" })
        });
        await runner.ExecuteAsync(new PostWorkflowCommandsRequest());

        return workflow.Name;
    }

    public static async Task<string> ProductCategoryFilterWorkflowAsync(WorkflowRunner runner)
    {
        var (adminStep, _) = await ExampleCatalogAsync(runner);

        var listElectronicsStep = await runner.BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("list-electronics"),
            Method   = Static("GET"),
            Path     = Static("/products?category=electronics")
        });
        await runner.ExecuteAsync(new PostCatalogStepCommandsRequest() with
        {
            AggregateId = Static(listElectronicsStep.Id),
            Commands    = Static(new List<object> { listElectronicsStep })
        });

        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());
        await runner.BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(adminStep.Id) });
        await runner.BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(listElectronicsStep.Id) });
        await runner.ExecuteAsync(new PostWorkflowCommandsRequest());

        return workflow.Name;
    }

    public static async Task<string> InStockFilterWorkflowAsync(WorkflowRunner runner)
    {
        await ExampleCatalogAsync(runner);

        var listInStockStep = await runner.BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("list-in-stock"),
            Method   = Static("GET"),
            Path     = Static("/products?inStock=true")
        });
        await runner.ExecuteAsync(new PostCatalogStepCommandsRequest() with
        {
            AggregateId = Static(listInStockStep.Id),
            Commands    = Static(new List<object> { listInStockStep })
        });

        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());
        await runner.BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(listInStockStep.Id) });
        await runner.ExecuteAsync(new PostWorkflowCommandsRequest());

        return workflow.Name;
    }

    public static async Task<string> ReusedExampleWorkflowAssertionAsync(WorkflowRunner runner)
    {
        var (adminStep, listStep) = await ExampleCatalogAsync(runner);

        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());
        await runner.BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(adminStep.Id) });
        await runner.BuildAsync(new AppendStepCommand() with { CatalogStepId = Static(listStep.Id) });
        await runner.BuildAsync(new AddAssertionCommand() with
        {
            Assertion = Static<object>(new { notEmpty = "$list-products" })
        });
        await runner.ExecuteAsync(new PostWorkflowCommandsRequest());

        return workflow.Name;
    }

    public static async Task<string> RunFailedWorkflowAsync(WorkflowRunner runner)
    {
        var target = await runner.BuildAsync(new CreateTargetCommand() with { BaseUrl = Static("http://localhost:9999") });
        await runner.ExecuteAsync(new PostTargetCommandsRequest());
        await runner.ExecuteAsync(new ListTargetsRequest() with { Name = Static(target.Name) });

        await runner.BuildAsync(new CreateCatalogCommand());
        await runner.ExecuteAsync(new PostCatalogCommandsRequest());
        var step = await runner.BuildAsync(new UpsertStepCommand());
        await runner.ExecuteAsync(new PostCatalogStepCommandsRequest() with
        {
            AggregateId = Static(step.Id),
            Commands    = Static(new List<object> { step })
        });

        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());
        await runner.BuildAsync(new AppendStepCommand());
        await runner.ExecuteAsync(new PostWorkflowCommandsRequest());

        return workflow.Name;
    }

    public static async Task<string> ArchivedWorkflowAsync(WorkflowRunner runner)
    {
        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());
        await runner.BuildAsync(new ArchiveWorkflowCommand());
        await runner.ExecuteAsync(new PostWorkflowCommandsRequest());
        return workflow.Name;
    }

    public static async Task<string> ArchivedTargetAsync(WorkflowRunner runner)
    {
        var target = await runner.BuildAsync(new CreateTargetCommand());
        await runner.BuildAsync(new ArchiveTargetCommand());
        await runner.ExecuteAsync(new PostTargetCommandsRequest());
        return target.Name;
    }

    public static async Task<string> CreatedTargetAsync(WorkflowRunner runner)
    {
        var target = await runner.BuildAsync(new CreateTargetCommand());
        await runner.ExecuteAsync(new PostTargetCommandsRequest());
        return target.Name;
    }

    public static async Task<string> VoucherValidationAsync(WorkflowRunner runner)
    {
        await ExampleCatalogAsync(runner);

        var validateSave10Step = await runner.BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("validate-save10"),
            Method   = Static("POST"),
            Path     = Static("/vouchers/validate"),
            Defaults = Static<object?>(new Dictionary<string, object?> { ["code"] = "SAVE10" })
        });
        await runner.ExecuteAsync(new PostCatalogStepCommandsRequest() with
        {
            Commands = Static(new List<object> { validateSave10Step })
        });

        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());
        await runner.BuildAsync(new AppendStepCommand());
        await runner.BuildAsync(new AddAssertionCommand() with
        {
            Assertion = Static<object>(new { equal = new object[] { "$validate-save10.valid", "true" } })
        });
        await runner.BuildAsync(new AddAssertionCommand() with
        {
            Assertion = Static<object>(new { equal = new object[] { "$validate-save10.discountPct", "10" } })
        });
        await runner.ExecuteAsync(new PostWorkflowCommandsRequest());

        return workflow.Name;
    }
}
