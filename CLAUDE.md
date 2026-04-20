# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Off-limits dependencies

Do **not** edit files in `../StepWise` or `../CommandFramework`. These are sibling repos treated as read-only dependencies here. Propose changes as suggestions only and wait for explicit instruction before touching those paths.

---

## Build and run

```bash
# Run the API (requires Postgres — see README for setup)
dotnet run --project src/StepWise.Management

# Build only
dotnet build

# Run all tests (requires API on localhost:5000 and a clean DB)
dotnet test tests/StepWise.Management.Tests

# Run a single test
dotnet test tests/StepWise.Management.Tests --filter "FullyQualifiedName~Catalog_01"
```

Tests are integration tests that hit the live API. Start the server before running them.

---

## Architecture

### Sibling dependencies

This project references two sibling repos by path (not NuGet):

- `../CommandFramework` — event-sourced aggregate framework
- `../StepWise` — JSON workflow test runner

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

`GET /targets`, `GET /catalogs`, `GET /catalog-steps`, `GET /workflows` — these query projection tables maintained by `EventReaction` handlers. They are NOT `MapAggregate` endpoints.

| Endpoint | Projection table |
|----------|-----------------|
| `GET /targets` | `target_summaries` |
| `GET /catalogs` | `catalog_summaries` |
| `GET /catalog-steps` | `catalog_step_summaries` |
| `GET /workflows` | `workflow_summaries` |

`GET /catalog-steps` accepts optional query params:
- `catalogId` — filter by catalog
- `showArchived` (default `false`) — when false, excludes rows where `is_archived = true`

List endpoints use `NpgsqlCommand` with positional `$1`, `$2` parameters and `DbDataReader` loops — not Dapper. JSONB columns (`defaults`) require `::text` cast when reading: `defaults::text`.

### Workflow execution

`POST /api/workflows/{id}/run` — synchronous: loads the workflow, resolves catalog steps and targets, runs `JsonWorkflowRunner.RunAsync`, records the result as a `TestRun` aggregate, always returns `{ runId, result }` with HTTP 200. Check `result.passed` to determine pass/fail.

---

## Integration tests

Tests live in `tests/StepWise.Management.Tests/WorkflowTests/`. They use `StepWise.Json.JsonWorkflowTestBase` with a declarative JSON format.

### File layout

```
WorkflowTests/
  targets.json                  ← { "management": "http://localhost:5000" }
  setup-catalog-with-step.workflow.json   ← shared setup, embedded by reference
  *.workflow.json               ← one file per test scenario
  Requests/
    management.requests.json    ← all step definitions
```

### Step definition format

**Build steps** (accumulate command items). `type` and `payload` live inside `defaults`. Creation commands always include `"id": { "generated": "guid" }` in their payload so downstream steps can reference the ID without `captureRequestAs`:
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

**HTTP steps** — use `pathParams` for path placeholders, `query` for query-string fields, and `defaults` for the JSON body. `postXCommands.aggregateId` derives from `createX.payload.id`; GET steps reference the same source directly:
```json
"postTargetCommands": {
  "target": "management",
  "method": "POST",
  "path": "/targets/commands",
  "defaults": {
    "aggregateId": { "from": "createTarget.payload.id" },
    "commands": { "from": "targetItems" }
  }
},
"getTarget": {
  "target": "management",
  "method": "GET",
  "path": "/targets/{aggregateId}",
  "pathParams": {
    "aggregateId": { "from": "createTarget.payload.id" }
  }
},
"listCatalogSteps": {
  "target": "management",
  "method": "GET",
  "path": "/catalog-steps",
  "query": {
    "catalogId": { "from": "createCatalog.payload.id" }
  }
}
```

### Workflow file format

Build step `with` overrides use the payload wrapper to deep-merge inside the `payload` object:
```json
{
  "name": "Catalog_01_CreateTarget",
  "steps": [
    {
      "build": "createTarget",
      "with": {
        "payload": { "static": {
          "name": { "static": "my-target" },
          "baseUrl": { "static": "http://localhost:5000" }
        }}
      }
    },
    { "step": "postTargetCommands" },
    { "step": "getTarget" }
  ],
  "assertions": [
    { "equal": ["getTarget.name", "my-target"] },
    { "equal": ["getTarget.baseUrl", "http://localhost:5000"] }
  ]
}
```

`StepInvocation` also supports `pathParams` and `query` for per-invocation overrides of path/query params.

Supported assertion types:
- `{ "equal": ["step.field", "literal"] }` — strict equality
- `{ "count": ["stepName", "N"] }` — array length equals N
- `{ "empty": ["stepName"] }` — array is empty
- `{ "notEmpty": ["stepName"] }` — array is non-empty

### Key test rules (from `Plans/rules.md` and `Plans/philosophy.md`)

- **Never hardcode IDs or names in defaults.** Use `{ "generated": "guid" }` so tests are isolated by construction.
- **Assert only against hard-coded literal values in `with` blocks.** Never assert against defaults, generated values, or captured values — these aren't known at the time the assertion is written. If a test needs to assert a field value, specify it explicitly in a `with` block and assert that literal.
- **Assert on GET responses, not command responses.** Command steps (`POST /*/commands`) are fire-and-forget. Assertions reference `getTarget.baseUrl`, not request captures.
- **Foreign key assertions use list responses.** To assert `getCatalogStep.targetId` is correct, compare against `listTargets[?name=createTarget.payload.name].id`, not `getTarget.id`.
- **List step immediately after post.** Place `listTargets` right after `postTargetCommands`, before any dependent builds.
- **Shared workflows carry no assertions.** `SetupCatalogWithStep` only establishes state; the calling workflow owns all assertions.
- **Override only what the test cares about.** Defaults encode correct usage; tests specify only the values that distinguish the scenario.

### ID access and `captureRequestAs`

Creation build steps generate their own `id` in the payload (`"id": { "generated": "guid" }`). `postXCommands.aggregateId` is set to `{ "from": "createX.payload.id" }`, and GET step `pathParams` reference the same source. This means `captureRequestAs` is **not needed** for normal create-then-get flows.

Use `captureRequestAs` only when you need to re-identify an aggregate that was created in a prior step — for example, a second `upsertStep` that must target the same `CatalogStep` aggregate as the first:

```json
{ "step": "postCatalogStepCommands", "captureRequestAs": "catalogStepReq" }
```

Then `catalogStepReq.aggregateId` lets the next build step pass `"id": { "from": "catalogStepReq.aggregateId" }` to update the same aggregate. If you don't need to re-identify an existing aggregate, omit `captureRequestAs`.

### Domain event `Id` convention

Every event type carries a `string Id` field so `EventReaction` handlers can maintain projection tables without access to the stream ID. The pattern:

- **Creation commands** accept `Id` as a parameter and pass it to the created event: `new TargetCreated(cmd.Id, cmd.Name, cmd.BaseUrl)`
- **Update/archive commands** do not accept `Id` — the handler reads it from aggregate state: `new TargetArchived(state.Id)`
- **Reactions** guard against missing IDs for backwards compatibility: `if (string.IsNullOrEmpty(e.Id)) return;`

When adding a new aggregate, follow this pattern or projection maintenance will silently break.

### JSONB SQL conventions

Projection tables store JSON blobs (e.g. `defaults`) as `JSONB` columns. Two casts are required:

- **Writing**: pass the serialized string and cast in SQL — `@defaults::jsonb`
- **Reading**: cast back to text before handing to the reader — `defaults::text`, then `JsonSerializer.Deserialize<JsonElement>(str)`

Omitting either cast causes a Npgsql type mismatch at runtime.

---

## Planned vs implemented

The `PLAN.md` and `Plans/` directory describe the intended final architecture. The current implementation diverges in a few areas:

- **`AddAssertion` validation**: plan requires validating that all step names referenced in an assertion exist in the workflow's current steps. Currently the domain stores any assertion without validation.
- **`TestRunAggregate`**: the plan has an async execution model (`StartRun` → background worker → `RecordStepResult` → `CompleteRun`). The current implementation uses a synchronous `POST /api/workflows/{id}/run` endpoint that always returns HTTP 200.
- **Execution aggregate**: the plan has a separate `executions` aggregate; current code uses `runs`.

When implementing new features, follow `PLAN.md` as the authoritative architecture doc.
