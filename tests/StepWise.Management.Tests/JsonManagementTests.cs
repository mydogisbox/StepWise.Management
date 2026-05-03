using Walkthrough.Json;
using Xunit;

namespace StepWise.Management.Tests;

/// <summary>
/// Integration tests for the StepWise.Management HTTP API.
/// Requires the management server running on http://localhost:5020
/// and a clean database (see README for setup).
/// </summary>
public class JsonManagementTests : JsonWorkflowTestBase
{
    protected override IReadOnlyList<string> ContractPaths =>
    [
        "WorkflowTests/Requests/management.contracts.json"
    ];

    protected override IReadOnlyList<string> TargetPaths =>
    [
        "WorkflowTests/management.target.json"
    ];

    protected override IReadOnlyList<string> SharedWorkflowPaths =>
    [
        "WorkflowTests/setup-catalog-with-step.workflow.json",
        "WorkflowTests/setup-admin-create-product-step.workflow.json",
        "WorkflowTests/setup-example-catalog.workflow.json"
    ];

    // ── Catalog ───────────────────────────────────────────────────────────────

    [Fact] public Task Catalog_01_CreateTarget_BaseUrlAsserted() =>
        RunWorkflowAsync("WorkflowTests/catalog-01-create-target.workflow.json");

    [Fact] public Task Catalog_02_Create_NameAsserted() =>
        RunWorkflowAsync("WorkflowTests/catalog-02-create-catalog.workflow.json");

    [Fact] public Task Catalog_03_AddStep_AllFieldsAsserted() =>
        RunWorkflowAsync("WorkflowTests/catalog-03-add-step.workflow.json");

    [Fact] public Task Catalog_04_UpsertStep_AllFieldsUpdated() =>
        RunWorkflowAsync("WorkflowTests/catalog-04-upsert-step.workflow.json");

    [Fact] public Task Catalog_05_ArchiveStep_IsArchivedTrue() =>
        RunWorkflowAsync("WorkflowTests/catalog-05-archive-step.workflow.json");

    [Fact] public Task Catalog_06_ListExcludesArchivedByDefault() =>
        RunWorkflowAsync("WorkflowTests/catalog-06-list-excludes-archived.workflow.json");

    [Fact] public Task Catalog_07_ListIncludesArchivedWhenFlagSet() =>
        RunWorkflowAsync("WorkflowTests/catalog-07-list-includes-archived.workflow.json");

    [Fact] public Task Catalog_08_ErrorCapturesStatus() =>
        RunWorkflowAsync("WorkflowTests/catalog-08-error-captures-status.workflow.json");

    [Fact] public Task Catalog_09_SuccessCapturesStatus() =>
        RunWorkflowAsync("WorkflowTests/catalog-09-success-captures-status.workflow.json");

    [Fact] public Task Catalog_10_UpdateCatalog_NameAndDescriptionAsserted() =>
        RunWorkflowAsync("WorkflowTests/catalog-10-update-catalog.workflow.json");

    [Fact] public Task Catalog_11_ArchiveCatalog_IsArchivedTrue() =>
        RunWorkflowAsync("WorkflowTests/catalog-11-archive-catalog.workflow.json");

    [Fact] public Task Catalog_12_UnarchiveCatalog_IsArchivedFalse() =>
        RunWorkflowAsync("WorkflowTests/catalog-12-unarchive-catalog.workflow.json");

    [Fact] public Task Catalog_13_UnarchiveStep_IsArchivedFalse() =>
        RunWorkflowAsync("WorkflowTests/catalog-13-unarchive-step.workflow.json");

    [Fact] public Task Catalog_14_StepShapes_RoundTrip() =>
        RunWorkflowAsync("WorkflowTests/catalog-14-step-shapes.workflow.json");

    [Fact] public Task Catalog_15_StepPolling_FlagAndRetryAsserted() =>
        RunWorkflowAsync("WorkflowTests/catalog-15-step-polling.workflow.json");

    [Fact] public Task Catalog_16_ListExcludesArchivedCatalog() =>
        RunWorkflowAsync("WorkflowTests/catalog-16-list-excludes-archived-catalog.workflow.json");

    [Fact] public Task Catalog_17_ListIncludesArchivedCatalogWhenFlagSet() =>
        RunWorkflowAsync("WorkflowTests/catalog-17-list-includes-archived-catalog.workflow.json");

    // ── Target ────────────────────────────────────────────────────────────────

    [Fact] public Task Target_01_Archive_IsArchivedTrue() =>
        RunWorkflowAsync("WorkflowTests/target-01-archive.workflow.json");

    [Fact] public Task Target_02_Unarchive_IsArchivedFalse() =>
        RunWorkflowAsync("WorkflowTests/target-02-unarchive.workflow.json");

    [Fact] public Task Target_03_Update_NameAndUrlAsserted() =>
        RunWorkflowAsync("WorkflowTests/target-03-update.workflow.json");

    [Fact] public Task Target_04_ListExcludesArchivedByDefault() =>
        RunWorkflowAsync("WorkflowTests/target-04-list-excludes-archived.workflow.json");

    [Fact] public Task Target_05_ListIncludesArchivedWhenFlagSet() =>
        RunWorkflowAsync("WorkflowTests/target-05-list-includes-archived.workflow.json");

    [Fact] public Task Target_06_CreatedAt_PresentInList() =>
        RunWorkflowAsync("WorkflowTests/target-06-created-at.workflow.json");

    // ── Workflow ──────────────────────────────────────────────────────────────

    [Fact] public Task Workflow_06_Create_NameCorrectAndStepsEmpty() =>
        RunWorkflowAsync("WorkflowTests/workflow-06-create.workflow.json");

    [Fact] public Task Workflow_07_Rename_UpdatesName() =>
        RunWorkflowAsync("WorkflowTests/workflow-07-rename.workflow.json");

    [Fact] public Task Workflow_08_AppendStep_OrderAndDefaultsAsserted() =>
        RunWorkflowAsync("WorkflowTests/workflow-08-append-step.workflow.json");

    [Fact] public Task Workflow_09_InsertStepBefore_OrderAsserted() =>
        RunWorkflowAsync("WorkflowTests/workflow-09-insert-before.workflow.json");

    [Fact] public Task Workflow_10_RemoveStep_OneStepRemains() =>
        RunWorkflowAsync("WorkflowTests/workflow-10-remove-step.workflow.json");

    [Fact] public Task Workflow_11_SetStepDefaults_DefaultsAsserted() =>
        RunWorkflowAsync("WorkflowTests/workflow-11-set-step-defaults.workflow.json");

    [Fact] public Task Workflow_12_BadAssertion_StoredSuccessfully() =>
        RunWorkflowAsync("WorkflowTests/workflow-12-bad-assertion.workflow.json");

    [Fact] public Task Workflow_13_AddAssertion_StoredAndAsserted() =>
        RunWorkflowAsync("WorkflowTests/workflow-13-add-assertion.workflow.json");

    [Fact] public Task Workflow_14_ArchiveWorkflow_IsArchivedTrue() =>
        RunWorkflowAsync("WorkflowTests/workflow-14-archive-workflow.workflow.json");

    [Fact] public Task Workflow_15_UnarchiveWorkflow_IsArchivedFalse() =>
        RunWorkflowAsync("WorkflowTests/workflow-15-unarchive-workflow.workflow.json");

    [Fact] public Task Workflow_16_UpdateDescription_DescriptionAsserted() =>
        RunWorkflowAsync("WorkflowTests/workflow-16-update-description.workflow.json");

    [Fact] public Task Workflow_17_ListExcludesArchivedByDefault() =>
        RunWorkflowAsync("WorkflowTests/workflow-17-list-excludes-archived.workflow.json");

    [Fact] public Task Workflow_18_ListIncludesArchivedWhenFlagSet() =>
        RunWorkflowAsync("WorkflowTests/workflow-18-list-includes-archived.workflow.json");

    // ── Runs ──────────────────────────────────────────────────────────────────

    [Fact] public Task Runs_01_List_ShowsCompletedRun() =>
        RunWorkflowAsync("WorkflowTests/runs-01-list.workflow.json");

    // ── Execution ─────────────────────────────────────────────────────────────

    [Fact] public Task Execution_16_Execute_Passes() =>
        RunWorkflowAsync("WorkflowTests/execution-16-run.workflow.json");

    [Fact] public Task Execution_17_ExecuteAssertionFails_ReportedCorrectly() =>
        RunWorkflowAsync("WorkflowTests/execution-17-cross-reference.workflow.json");

    [Fact] public Task Execution_18_ExecuteWithStepDefaults_DefaultsInOutput() =>
        RunWorkflowAsync("WorkflowTests/execution-18-assertion.workflow.json");

    [Fact] public Task Execution_19_RunResultStoredAsObject() =>
        RunWorkflowAsync("WorkflowTests/execution-19-run-result-stored-as-object.workflow.json");

    [Fact] public Task Execution_20_ProductCategoryFilter() =>
        RunWorkflowAsync("WorkflowTests/execution-20-category-filter.workflow.json");

    [Fact] public Task Execution_21_InStockFilter() =>
        RunWorkflowAsync("WorkflowTests/execution-21-in-stock-filter.workflow.json");

    [Fact] public Task Execution_22_VoucherValidationWithAssertions() =>
        RunWorkflowAsync("WorkflowTests/execution-22-voucher-validation.workflow.json");
}
