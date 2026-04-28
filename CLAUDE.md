# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Off-limits dependencies

Do **not** edit files in `../Walkthrough` or `../CommandFramework`. These are sibling repos treated as read-only dependencies here. Propose changes as suggestions only and wait for explicit instruction before touching those paths.

---

## Build and run

```bash
# Run the API (requires Postgres — see README for setup)
dotnet run --project src/StepWise.Management

# Build only
dotnet build

# Run all tests (starts DB, runs migrations, starts API, runs tests, stops API)
bash test.sh

# Run a single test (requires API already running via test.sh or manually)
dotnet test tests/StepWise.Management.Tests --filter "FullyQualifiedName~Catalog_01"
```

Tests are integration tests. Use `test.sh` — it handles the full lifecycle (DB, migrations, API start/stop).

---

## Architecture

### Sibling dependencies

This project references two sibling repos by path (not NuGet):

- `../CommandFramework` — event-sourced aggregate framework
- `../Walkthrough` — JSON workflow test runner

Both must be present alongside this repo. Do not add these as packages.

### Domain aggregates

Five independent event-sourced aggregates, each with their own stream prefix and command/GET endpoints via `MapAggregate`:

| Aggregate | Stream prefix | Commands endpoint |
|-----------|--------------|------------------|
| Target | `targets/` | `POST /targets/commands` |
| Catalog | `catalogs/` | `POST /catalogs/commands` |
| CatalogStep | `catalog-steps/` | `POST /catalog-steps/commands` |
| Workflow | `workflows/` | `POST /workflows/commands` |
| TestRun | `runs/` | `POST /runs/commands` |

`MapAggregate` registers two routes: `POST /{name}/commands` (accepts `CommandBatch`) and `GET /{name}/{aggregateId}` (returns folded state). The POST response is `IReadOnlyList<CommandSuccess>` — a JSON array.

### CommandBatch format

```json
{
  "aggregateId": "client-generated-uuid",
  "commands": [
    { "type": "CommandName", "payload": { "field": "value" } }
  ]
}
```

The `aggregateId` is always client-generated. The server does not assign IDs.

### List endpoints (projection-based)

`GET /targets`, `GET /catalogs`, `GET /catalog-steps`, `GET /workflows`, `GET /runs` — these query projection tables maintained by `EventReaction` handlers. They are NOT `MapAggregate` endpoints.

| Endpoint | Projection table |
|----------|-----------------|
| `GET /targets` | `target_summaries` |
| `GET /catalogs` | `catalog_summaries` |
| `GET /catalog-steps` | `catalog_step_summaries` |
| `GET /workflows` | `workflow_summaries` |
| `GET /runs` | `test_run_summaries` |

All list endpoints accept `showArchived` (default `false`) — when false, excludes rows where `is_archived = true`. `GET /catalog-steps` additionally accepts `catalogId` to filter by catalog.

List endpoints use `NpgsqlCommand` with positional `$1`, `$2` parameters and `DbDataReader` loops — not Dapper. JSONB columns (`defaults`) require `::text` cast when reading: `defaults::text`.

### `test_run_summaries` reactions

Three reactions maintain this table across the run lifecycle:
- `RunTriggered` — INSERTs the row with `workflow_id` and `started_at`; `passed` and `duration_ms` are left NULL until the run finishes.
- `RunCompleted` — UPDATEs `passed` and `duration_ms`
- `RunFailed` — UPDATEs `passed = false` and `duration_ms`

`passed` and `duration_ms` are nullable columns to support this two-phase write. `workflow_name` is not stored — `GET /runs` joins `workflow_summaries` at query time so names stay current.

### Workflow execution

`POST /api/workflows/{id}/run` — synchronous: loads the workflow, resolves catalog steps and targets, runs `JsonWorkflowRunner.RunAsync`, records the result as a `TestRun` aggregate, always returns `{ runId, result }` with HTTP 200. Check `result.passed` to determine pass/fail.

---

## Integration tests

Tests live in `tests/StepWise.Management.Tests/WorkflowTests/`. They use `Walkthrough.Json.JsonWorkflowTestBase`.

**For the full JSON workflow format** — step definitions, field value types (`static`, `from`, `generated`, `template`), assertion types, path syntax, `poll`, `captureAs`, `headers`, per-invocation overrides, etc. — read `../Walkthrough/CLAUDE.md`. Everything below is specific to this project.

### File layout

```
WorkflowTests/
  targets.json                         ← target definitions (management + example APIs)
  setup-catalog-with-step.workflow.json ← shared setup, embedded via `workflow` step
  *.workflow.json                       ← one file per test scenario
  Requests/
    management.requests.json            ← management API step definitions
    example.requests.json               ← Example API step definitions
```

### Management-specific step conventions

**Build steps** accumulate `CommandBatch` items. `type` and `payload` are always nested inside `defaults`. Creation commands include `"id": { "generated": "guid" }` in the payload so downstream steps can reference it:

```json
"createTarget": {
  "accumulateAs": "targetItems",
  "defaults": {
    "type": { "static": "CreateTarget" },
    "payload": { "static": {
      "id": { "generated": "guid" },
      "name": { "generated": "guid" },
      "baseUrl": { "static": "http://localhost:5000" }
    }}
  }
}
```

Build step `with` overrides use the `payload` wrapper to deep-merge inside the payload object:

```json
{
  "build": "createTarget",
  "with": {
    "payload": { "static": {
      "name": { "static": "my-target" },
      "baseUrl": { "static": "http://localhost:5000" }
    }}
  }
}
```

### Key test rules

- **Never hardcode IDs or names in defaults.** Use `{ "generated": "guid" }` so tests are isolated by construction.
- **Assert only against hard-coded literal values in `with` blocks.** Never assert against defaults, generated values, or captured values — these aren't known at assertion-write time.
- **Assert on GET responses, not command responses.** Command steps (`POST /*/commands`) are fire-and-forget. Assertions reference `$getTarget.baseUrl`, not request captures.
- **Foreign key assertions use list responses.** To assert `$getCatalogStep.targetId` is correct, compare against `$listTargets[?name=createTarget.payload.name].id`, not `$getTarget.id`.
- **List step immediately after post.** Place `listTargets` right after `postTargetCommands`, before any dependent builds.
- **Shared workflows carry no assertions.** `SetupCatalogWithStep` only establishes state; the calling workflow owns all assertions.
- **Override only what the test cares about.** Defaults encode correct usage; tests specify only the values that distinguish the scenario.

### `captureRequestAs`

Creation build steps generate their own `id` in the payload, so `captureRequestAs` is not needed for normal create-then-get flows. Use it only when you need to re-identify an existing aggregate across step invocations — for example, a second `upsertStep` targeting the same `CatalogStep` aggregate as the first.

### `captureFullResponseAs` ⚠️ not in Walkthrough CLAUDE.md

By default, any non-2xx response throws. Use `captureFullResponseAs` to capture the full response without throwing. The captured value is `{ "status": int, "body": ... }`.

```json
{ "step": "postCatalogStepCommands", "captureFullResponseAs": "errorResponse" }
```

Assertions reference `$errorResponse.status` and `$errorResponse.body.*`. This feature is used in existing management tests and works, but it does **not appear in `../Walkthrough/CLAUDE.md`** — its support in the library is undocumented. `captureFullResponseAs` and `captureRequestAs` can be combined on the same invocation.

### Domain event `Id` convention

Every event type carries a `string Id` field so `EventReaction` handlers can maintain projection tables without access to the stream ID. The pattern:

- **Creation commands** accept `Id` as a parameter and pass it to the created event: `new TargetCreated(cmd.Id, cmd.Name, cmd.BaseUrl)`
- **Update/archive commands** do not accept `Id` — the handler reads it from aggregate state: `new TargetArchived(state.Id)`

When adding a new aggregate, follow this pattern or projection maintenance will silently break.

### JSONB SQL conventions

Projection tables store JSON blobs (e.g. `defaults`) as `JSONB` columns. Two casts are required:

- **Writing**: pass the serialized string and cast in SQL — `@defaults::jsonb`
- **Reading**: cast back to text before handing to the reader — `defaults::text`, then `JsonSerializer.Deserialize<JsonElement>(str)`

Omitting either cast causes a Npgsql type mismatch at runtime.

---

## Planned vs implemented

The `PLAN.md` and `Plans/` directory describe the intended final architecture. The following gaps remain:

- **`AddAssertion` validation**: plan requires validating that all step names referenced in an assertion exist in the workflow's current steps. Currently the domain stores any assertion without validation.
- **`TestRunAggregate` command model**: the plan calls for `StartRun` → `RecordStepResult` (one per step) → `CompleteRun` with a `Running` intermediate status and `List<StepRunResult>` on state. The current model uses `TriggerRun` → `RecordResult`/`RecordFailure`, recording the full run result as a single JSON blob with no per-step entries.
- **Execution API**: plan says clients call `POST /runs/commands` with `StartRun` directly. Current code still has a `POST /api/workflows/{id}/run` wrapper endpoint.

### Settled design decisions (no longer open)

- **5-aggregate design is intentional**: Target, Catalog, CatalogStep, Workflow, and TestRun are separate aggregates. PLAN.md's earlier description of embedding steps/targets inside `CatalogAggregate` is superseded.
- **Async execution is implemented**: `POST /api/workflows/{id}/run` dispatches `TriggerRun`, an outbox entry is created, and `WorkflowExecutionService` processes it asynchronously. Callers poll `GET /runs/{id}` for the result.
- **`runs` stream prefix**: the plan referenced a separate `executions` aggregate; `runs` is the implemented and correct name.

When implementing new features, follow `PLAN.md` as the authoritative architecture doc.
