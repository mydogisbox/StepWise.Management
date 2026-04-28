using Walkthrough.Json;
using Xunit;

namespace StepWise.Management.Tests;

/// <summary>
/// Integration tests for the Example Order Management API.
/// Requires ExampleApi running on http://localhost:3001 (started via dev.sh).
/// All tests are self-contained: each creates its own products, users, and vouchers
/// via admin endpoints before exercising user-facing behaviour.
/// </summary>
public class JsonExampleApiTests : JsonWorkflowTestBase
{
    protected override IReadOnlyList<string> RequestPaths =>
    [
        "Requests/example.requests.json"
    ];

    protected override string TargetsPath => "WorkflowTests/targets.json";

    protected override IReadOnlyList<string> SharedWorkflowPaths => [];

    [Fact] public Task Example_01_ListProducts_NotEmpty() =>
        RunWorkflowAsync("WorkflowTests/example-01-list-products.workflow.json");

    [Fact] public Task Example_02_GetProduct_FieldsAsserted() =>
        RunWorkflowAsync("WorkflowTests/example-02-get-product.workflow.json");

    [Fact] public Task Example_03_FilterByCategory_ReturnsMatchingProducts() =>
        RunWorkflowAsync("WorkflowTests/example-03-filter-by-category.workflow.json");

    [Fact] public Task Example_04_FilterInStock_ExcludesOutOfStockProducts() =>
        RunWorkflowAsync("WorkflowTests/example-04-filter-in-stock.workflow.json");

    [Fact] public Task Example_05_PurchaseFlow_OrderCreatedPending() =>
        RunWorkflowAsync("WorkflowTests/example-05-purchase-flow.workflow.json");

    [Fact] public Task Example_06_VoucherDiscount_DiscountApplied() =>
        RunWorkflowAsync("WorkflowTests/example-06-voucher-discount.workflow.json");

    [Fact] public Task Example_07_OutOfStock_AddToCartReturns422() =>
        RunWorkflowAsync("WorkflowTests/example-07-out-of-stock.workflow.json");

    [Fact] public Task Example_08_CancelOrder_StatusIsCancelled() =>
        RunWorkflowAsync("WorkflowTests/example-08-cancel-order.workflow.json");

    [Fact] public Task Example_09_ValidateVoucher_ValidIsTrue() =>
        RunWorkflowAsync("WorkflowTests/example-09-validate-voucher.workflow.json");

    [Fact] public Task Example_10_ValidateInvalidVoucher_Returns422() =>
        RunWorkflowAsync("WorkflowTests/example-10-validate-invalid-voucher.workflow.json");
}
