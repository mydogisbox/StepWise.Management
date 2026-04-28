# StepWise.Management — Implementation Plan

## StepWise Library Changes Required

### Polling support

Polling must work in both the JSON declarative path and the C# fluent path.

#### JSON path — new `poll` step type in `JsonWorkflowRunner`

Add `poll` as a new step variant alongside `step` and `build` in `StepInvocation`. The runner re-executes the referenced step definition on each attempt and evaluates the `until` condition using the existing assertion evaluation logic.

**Workflow JSON syntax:**
```json
{ "poll": "stepName", "until": { "equal": ["stepName.status", "Completed"] }, "intervalMs": 500, "timeoutMs": 10000 }
```

- `poll` — name of a step defined in the requests file to re-execute on each attempt
- `until` — a single assertion (same syntax as top-level assertions) evaluated against the step's captures
- `intervalMs` — delay between attempts (default: 500)
- `timeoutMs` — max total wait before the poll step fails (default: 10000)

The final successful response is captured under the step name for use in subsequent assertions.

#### C# fluent path — `PollAsync<TResponse>` on `WorkflowContext`

Add a `PollAsync` method to `WorkflowContext` that retries `ExecuteAsync` until a predicate returns true or the timeout expires.

```csharp
var run = await context.PollAsync(
    new GetRunRequest(runId),
    until: r => r.Status == RunStatus.Completed,
    intervalMs: 500,
    timeoutMs: 10000);
```

Both paths keep their existing captures semantics — the final successful response overwrites any prior capture under the same step name.

---

## Decisions Made

### Entity IDs
- Caller-generated (client supplies UUID on create)

### Catalog Step Archiving
- Steps are archived, not deleted, since workflows may reference them
- Archived steps can still be executed by workflows
- `CatalogState.Steps` changes from `Dictionary<string, StepDefinition>` to `Dictionary<string, CatalogStep>`
- `CatalogStep` is a management-specific wrapper: `record CatalogStep(StepDefinition Definition, bool IsArchived = false)`
- New commands: `ArchiveStep(string StepName)`, `UnarchiveStep(string StepName)`

### Catalog Step Target Validation
- `UpsertStep` validates at command time that `stepDef.Target` exists in the catalog's current targets
- A step cannot be added if it references a target not defined in the same catalog

### Workflow Steps
- No `CatalogIds` list on workflow — each step carries its own catalog reference
- Management-specific step type: `record WorkflowStep(Guid Id, string StepName, string CatalogId)`
- UUID per step enables stable identity for insertion and removal

#### Step Management Commands (replaces index-based operations)
| Command | Works on empty workflow? |
|---|---|
| `AppendStep(WorkflowStep)` | ✓ |
| `InsertStepBefore(Guid beforeId, WorkflowStep)` | ✗ |
| `RemoveStep(Guid id)` | ✗ |
| `UpdateStep(Guid id, WorkflowStep)` | ✗ |

### Assertion Validation
- `AddAssertion` validates at command time that all step names referenced in the assertion path exist in the workflow's current steps
- e.g. `{ "notEmpty": "nonExistentStep.field" }` is rejected if `nonExistentStep` is not in the workflow

### Workflow Execution API
- `POST /api/workflows/{id}/run` body: `{ runId }` (client-supplied) → `201 Created` with no body — always records the run regardless of pass/fail
- `GET /runs/{runId}` → returns `TestRunState` including `passed`
- Pass/fail is determined by reading the run result, not by HTTP status of the run endpoint

### Open Question
- `TestRunState.ResultJson` — currently stored and returned as an escaped JSON string. Should it be a nested object in the GET response? Shape TBD.

---

## Domain Model

### Settled: 5-aggregate design

Targets and catalog steps are separate aggregates — they are **not** embedded inside `CatalogAggregate`.

| Aggregate | Stream prefix | Commands endpoint |
|-----------|--------------|------------------|
| Target | `targets/` | `POST /targets/commands` |
| Catalog | `catalogs/` | `POST /catalogs/commands` |
| CatalogStep | `catalog-steps/` | `POST /catalog-steps/commands` |
| Workflow | `workflows/` | `POST /workflows/commands` |
| TestRun | `runs/` | `POST /runs/commands` |

### CatalogAggregate (current)

```csharp
public record CatalogState(string Id, string Name, bool IsArchived, string Description = "");
```

Commands: `CreateCatalog`, `UpdateCatalog`, `ArchiveCatalog`, `UnarchiveCatalog`

### CatalogStepAggregate (current)

```csharp
public record CatalogStepState(
    string Id, string CatalogId, string TargetId, string StepName,
    string Method, string Path, JsonElement? Defaults, bool IsArchived,
    JsonElement? RequestShape, JsonElement? ResponseShape,
    bool IsPolling, int? RetryCount, int? RetryDurationMs);
```

Commands: `UpsertStep`, `ArchiveStep`, `UnarchiveStep`

### WorkflowAggregate (current)

```csharp
public record WorkflowState(
    string Id,
    string Name,
    List<WorkflowStep> Steps,
    List<AssertionDefinition> Assertions,
    bool IsArchived,
    string Description = "");

public record WorkflowStep
{
    public string Id { get; init; }           // client-generated UUID
    public string CatalogStepId { get; init; } // UUID of the CatalogStep aggregate
    public string CatalogId { get; init; }
    public JsonElement? Defaults { get; init; }
}
```

Commands: `CreateWorkflow`, `RenameWorkflow`, `UpdateDescription`, `AppendStep`, `InsertStepBefore`, `RemoveStep`, `SetStepDefaults`, `AddAssertion`, `ArchiveWorkflow`, `UnarchiveWorkflow`

**TODO:** `AddAssertion` should validate that step names referenced in the assertion exist in `state.Steps`.

---

### TestRunAggregate (current)

```csharp
public record WorkflowRunState(
    string Id,
    string WorkflowId,
    string Status,        // "pending" | "completed" | "failed"
    bool? Passed,
    JsonElement? Result,
    string? Error,
    DateTimeOffset TriggeredAt,
    long? DurationMs);
```

**Commands / Events:**
| Command | Event | Notes |
|---|---|---|
| `TriggerRun(Id, WorkflowId)` | `RunTriggered(Id, WorkflowId, TriggeredAt)` | Creates outbox entry; inserts `test_run_summaries` row |
| `RecordResult(Passed, Result, DurationMs)` | `RunCompleted(Id, Passed, Result, DurationMs)` | Full `WorkflowResult` JSON stored as blob |
| `RecordFailure(Error, DurationMs)` | `RunFailed(Id, Error, DurationMs)` | For unexpected exceptions |

**Execution flow:**
1. `POST /api/workflows/{id}/run` dispatches `TriggerRun`, creating an outbox row
2. `WorkflowExecutionService` (background worker) claims the row, loads current workflow + catalog step state, calls `JsonWorkflowRunner.RunAsync`
3. Posts `RecordResult` or `RecordFailure` to finalize; marks outbox row processed
4. Up to `WorkflowExecution:MaxAttempts` retries on exception before `RecordFailure`

**TODO (future):** migrate to `StartRun` → `RecordStepResult` (per step) → `CompleteRun` with a `Running` intermediate status, snapshotting the full `WorkflowDefinition` in the event so in-progress runs are isolated from subsequent edits.

---

## REST API

### Command endpoints (via `MapAggregate`)

All mutations are sent as a `CommandBatch` to the aggregate's command endpoint. The batch always targets a single aggregate instance. Responses are never captured in integration tests — only status codes matter.

**Request body:**
```json
{
  "aggregateId": "some-uuid",
  "commands": [
    { "type": "CommandName", "payload": { ... } }
  ]
}
```

**Responses:**
- `200 OK` → `[ { "index": 0, "aggregateId": "...", "events": ["EventName"] } ]`
- `422 Unprocessable Entity` → `{ "error": "..." }`

| Method | Path | Accepted commands |
|---|---|---|
| `POST` | `/targets/commands` | `CreateTarget`, `UpdateTarget`, `ArchiveTarget`, `UnarchiveTarget` |
| `POST` | `/catalogs/commands` | `CreateCatalog`, `UpdateCatalog`, `ArchiveCatalog`, `UnarchiveCatalog` |
| `POST` | `/catalog-steps/commands` | `UpsertStep`, `ArchiveStep`, `UnarchiveStep` |
| `POST` | `/workflows/commands` | `CreateWorkflow`, `RenameWorkflow`, `UpdateDescription`, `AppendStep`, `InsertStepBefore`, `RemoveStep`, `SetStepDefaults`, `AddAssertion`, `ArchiveWorkflow`, `UnarchiveWorkflow` |
| `POST` | `/runs/commands` | `TriggerRun`, `RecordResult`, `RecordFailure` |

### Aggregate GET endpoints (via `MapAggregate`)

| Method | Path | Returns |
|---|---|---|
| `GET` | `/targets/{id}` | `TargetState` |
| `GET` | `/catalogs/{id}` | `CatalogState` |
| `GET` | `/catalog-steps/{id}` | `CatalogStepState` |
| `GET` | `/workflows/{id}` | `WorkflowState` |
| `GET` | `/runs/{id}` | `WorkflowRunState` |

### Read-model list endpoints

| Method | Path | Returns |
|---|---|---|
| `GET` | `/targets` | `target_summaries` rows |
| `GET` | `/catalogs` | `catalog_summaries` rows |
| `GET` | `/catalog-steps` | `catalog_step_summaries` rows (filterable by `catalogId`) |
| `GET` | `/workflows` | `workflow_summaries` rows |
| `GET` | `/runs` | `test_run_summaries` rows joined with `workflow_summaries` (desc by `started_at`) |

### Execution trigger endpoint

`POST /api/workflows/{id}/run` — body `{ "runId": "client-uuid" }`. Dispatches `TriggerRun`; returns `{ runId }`. Poll `GET /runs/{runId}` for result.

### Integration test conventions
- Command steps (`POST /*/commands`) are fire-and-forget — their responses are never captured
- All field assertions reference GET responses only
- Status code assertions may reference command step responses (e.g. expecting `422` for invalid commands)

---

## Integration Test Scenarios

All tests are fully self-contained — each sets up exactly the dependencies it needs.

### Catalog

| # | Steps | Assertion |
|---|---|---|
| 1 | Create catalog → GET catalog | name matches |
| 2 | Create catalog → add target → add step → GET catalog | step appears |
| 3 | Create catalog → add target → add step → upsert step with new method → GET catalog | method changed |
| 4 | Create catalog → add target → add step → archive step → GET catalog | `steps.X.isArchived = true` |
| 5 | Create catalog → add target → GET catalog | target appears |

### Workflow

| # | Steps | Assertion |
|---|---|---|
| 6 | Create workflow → GET workflow | name correct, steps empty |
| 7 | Create workflow → rename → GET workflow | new name |
| 8 | Create catalog + target + step → create workflow → AppendStep → GET workflow | steps list has entry |
| 9 | Create catalog + target + step → create workflow → AppendStep(A) → AppendStep(B) → InsertStepBefore(B, C) → GET workflow | order is A, C, B |
| 10 | Create catalog + target + step → create workflow → AppendStep → RemoveStep → GET workflow | steps empty |
| 11 | Create catalog + target + step → create workflow → AppendStep → UpdateStep → GET workflow | step changed |
| 12 | Create catalog + target + step → create workflow → AppendStep("ping") → AddAssertion("nonExistentStep.field") | command fails |
| 13 | Create catalog + target + step → create workflow → AppendStep("ping") → AddAssertion("ping.field") → GET workflow | assertion appears |
| 14 | Create workflow → archive → GET workflow | `isArchived = true` |
| 15 | Create workflow → archive → unarchive → GET workflow | `isArchived = false` |

### Execution

Steps include polling `GET /runs/{id}` until `status = "Completed"` before asserting.

| # | Steps | Assertion |
|---|---|---|
| 16 | Create catalog + target + step → create workflow → AppendStep → StartRun → poll GET run until Completed | `passed = true` |
| 17 | Create catalog + target + 2 steps (step 2 references step 1 result) → create workflow → AppendStep × 2 → add assertion verifying cross-reference → StartRun → poll GET run | `passed = true` |
| 18 | Create catalog + target + step → create workflow → AppendStep → add assertion → StartRun → poll GET run | `passed = true` |
