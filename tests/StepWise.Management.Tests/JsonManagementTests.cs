using StepWise.Json;
using Xunit;

namespace StepWise.Management.Tests;

/// <summary>
/// Integration tests for the StepWise.Management HTTP API.
/// Requires the management server running on http://localhost:5000
/// and a clean database (see README for setup).
/// </summary>
public class JsonManagementTests : JsonWorkflowTestBase
{
    protected override IReadOnlyList<string> RequestPaths =>
    [
        "Requests/management.requests.json"
    ];

    protected override string TargetsPath => "WorkflowTests/targets.json";

    protected override IReadOnlyList<string> SharedWorkflowPaths =>
    [
        "WorkflowTests/setup-catalog-with-step.workflow.json"
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

    // ── Execution ─────────────────────────────────────────────────────────────

    [Fact] public Task Execution_16_Execute_Passes() =>
        RunWorkflowAsync("WorkflowTests/execution-16-run.workflow.json");

    [Fact] public Task Execution_17_ExecuteAssertionFails_ReportedCorrectly() =>
        RunWorkflowAsync("WorkflowTests/execution-17-cross-reference.workflow.json");

    [Fact] public Task Execution_18_ExecuteWithStepDefaults_DefaultsInOutput() =>
        RunWorkflowAsync("WorkflowTests/execution-18-assertion.workflow.json");
}
