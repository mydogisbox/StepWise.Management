using Walkthrough.Core;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public static class Setups
{
    public static async Task ExampleCatalogAsync(WorkflowRunner runner)
    {
        await runner.BuildAsync(new CreateTargetCommand() with { BaseUrl = Static("http://localhost:5010") });
        await runner.ExecuteAsync(new PostTargetCommandsRequest());

        await runner.BuildAsync(new CreateCatalogCommand());
        await runner.ExecuteAsync(new PostCatalogCommandsRequest());
    }

    public static async Task<UpsertStepOutput> BuildAdminCreateProductStepAsync(WorkflowRunner runner)
    {
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
        await runner.ExecuteAsync(new PostCatalogStepCommandsRequest());
        return adminStep;
    }

    private static async Task BuildListProductsStepAsync(WorkflowRunner runner)
    {
        var listStep = await runner.BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("list-products"),
            Method   = Static("GET"),
            Path     = Static("/products")
        });
        await runner.ExecuteAsync(new PostCatalogStepCommandsRequest());
    }

    public static async Task<string> TwoStepWorkflowAsync(WorkflowRunner runner)
    {
        await ExampleCatalogAsync(runner);

        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());
        
        await BuildAdminCreateProductStepAsync(runner);
        await runner.BuildAsync(new AppendStepCommand());

        await BuildListProductsStepAsync(runner);
        await runner.BuildAsync(new AppendStepCommand());

        await runner.ExecuteAsync(new PostWorkflowCommandsRequest());

        return workflow.Name;
    }

    public static async Task<string> CrossReferenceWorkflowAsync(WorkflowRunner runner)
    {
        await ExampleCatalogAsync(runner);

        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());

        await BuildAdminCreateProductStepAsync(runner);
        await runner.BuildAsync(new AppendStepCommand());

        await BuildListProductsStepAsync(runner);
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
        await ExampleCatalogAsync(runner);

        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());

        await BuildAdminCreateProductStepAsync(runner);
        await runner.BuildAsync(new AppendStepCommand());

        await BuildListProductsStepAsync(runner);
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
        await ExampleCatalogAsync(runner);

        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());

        await BuildAdminCreateProductStepAsync(runner);
        await runner.BuildAsync(new AppendStepCommand());

        var listElectronicsStep = await runner.BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("list-electronics"),
            Method   = Static("GET"),
            Path     = Static("/products?category=electronics")
        });
        await runner.ExecuteAsync(new PostCatalogStepCommandsRequest());
        await runner.BuildAsync(new AppendStepCommand());

        await runner.ExecuteAsync(new PostWorkflowCommandsRequest());

        return workflow.Name;
    }

    public static async Task<string> InStockFilterWorkflowAsync(WorkflowRunner runner)
    {
        await ExampleCatalogAsync(runner);

        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());

        await runner.BuildAsync(new UpsertStepCommand() with
        {
            StepName = Static("list-in-stock"),
            Method   = Static("GET"),
            Path     = Static("/products?inStock=true")
        });
        await runner.ExecuteAsync(new PostCatalogStepCommandsRequest());
        await runner.BuildAsync(new AppendStepCommand());

        await runner.ExecuteAsync(new PostWorkflowCommandsRequest());

        return workflow.Name;
    }

    public static async Task<string> ReusedExampleWorkflowAssertionAsync(WorkflowRunner runner)
    {
        await ExampleCatalogAsync(runner);

        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());

        await BuildAdminCreateProductStepAsync(runner);
        await runner.BuildAsync(new AppendStepCommand());

        await BuildListProductsStepAsync(runner);
        await runner.BuildAsync(new AppendStepCommand());

        await runner.BuildAsync(new AddAssertionCommand() with
        {
            Assertion = Static<object>(new { notEmpty = "$list-products" })
        });
        await runner.ExecuteAsync(new PostWorkflowCommandsRequest());

        return workflow.Name;
    }

    public static async Task<string> RunFailedWorkflowAsync(WorkflowRunner runner)
    {
        await runner.BuildAsync(new CreateTargetCommand() with { BaseUrl = Static("http://localhost:9999") });
        await runner.ExecuteAsync(new PostTargetCommandsRequest());

        await runner.BuildAsync(new CreateCatalogCommand());
        await runner.ExecuteAsync(new PostCatalogCommandsRequest());
        await runner.BuildAsync(new UpsertStepCommand());
        await runner.ExecuteAsync(new PostCatalogStepCommandsRequest());

        var workflow = await runner.BuildAsync(new CreateWorkflowCommand());
        await runner.BuildAsync(new AppendStepCommand());
        await runner.ExecuteAsync(new PostWorkflowCommandsRequest());

        return workflow.Name;
    }

    public static async Task SetupCatalogAsync(WorkflowRunner runner)
    {
        await runner.BuildAsync(new CreateTargetCommand());
        await runner.ExecuteAsync(new PostTargetCommandsRequest());
        await runner.BuildAsync(new CreateCatalogCommand());
        await runner.ExecuteAsync(new PostCatalogCommandsRequest());
    }

    public static async Task SetupCatalogWithStepAsync(WorkflowRunner runner)
    {
        await runner.BuildAsync(new CreateTargetCommand());
        await runner.ExecuteAsync(new PostTargetCommandsRequest());
        await runner.BuildAsync(new CreateCatalogCommand());
        await runner.ExecuteAsync(new PostCatalogCommandsRequest());
        await runner.BuildAsync(new UpsertStepCommand());
        await runner.ExecuteAsync(new PostCatalogStepCommandsRequest());
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
        await runner.ExecuteAsync(new PostCatalogStepCommandsRequest());

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
