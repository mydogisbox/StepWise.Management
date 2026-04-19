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

### List endpoints (non-aggregate, event-scanning)

`GET /targets`, `GET /catalogs`, `GET /catalog-steps?catalogId=...`, `GET /workflows` — these scan the event store and return arrays of summary objects. They are NOT `MapAggregate` endpoints.

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

**Build steps** (accumulate command items). `type` and `payload` live inside `defaults` so they are resolvable field values:
```json
"createTarget": {
  "accumulateAs": "targetItems",
  "defaults": {
    "type": { "static": "CreateTarget" },
    "payload": { "static": {
      "baseUrl": { "static": "http://localhost:5000" }
    }}
  }
}
```

**HTTP steps** — use `pathParams` for path placeholders, `query` for query-string fields, and `defaults` for the JSON body:
```json
"postTargetCommands": {
  "target": "management",
  "method": "POST",
  "path": "/targets/commands",
  "defaults": {
    "aggregateId": { "generated": "guid" },
    "commands": { "from": "targetItems" }
  }
},
"getTarget": {
  "target": "management",
  "method": "GET",
  "path": "/targets/{aggregateId}",
  "pathParams": {
    "aggregateId": { "from": "targetReq.aggregateId" }
  }
},
"listCatalogSteps": {
  "target": "management",
  "method": "GET",
  "path": "/catalog-steps",
  "query": {
    "catalogId": { "from": "catalogReq.aggregateId" }
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
          "baseUrl": { "static": "http://localhost:5000" }
        }}
      }
    },
    { "step": "postTargetCommands", "captureRequestAs": "targetReq" },
    { "step": "listTargets" },
    { "step": "getTarget" }
  ],
  "assertions": [
    { "equal": ["getTarget.baseUrl", "http://localhost:5000"] }
  ]
}
```

`StepInvocation` also supports `pathParams` and `query` for per-invocation overrides of path/query params.

### Key test rules (from `Plans/rules.md` and `Plans/philosophy.md`)

- **Never hardcode IDs or names in defaults.** Use `{ "generated": "guid" }` so tests are isolated by construction.
- **Assert on GET responses, not command responses.** Command steps (`POST /*/commands`) are fire-and-forget. Assertions reference `getTarget.baseUrl`, not request captures.
- **Foreign key assertions use list responses.** To assert `getCatalogStep.targetId` is correct, compare against `listTargets[0].id`, not `getTarget.id`.
- **List step immediately after post.** Place `listTargets` right after `postTargetCommands`, before any dependent builds.
- **Shared workflows carry no assertions.** `SetupCatalogWithStep` only establishes state; the calling workflow owns all assertions.
- **Override only what the test cares about.** Defaults encode correct usage; tests specify only the values that distinguish the scenario.

### `postXCommands.aggregateId` access

The `aggregateId` is generated client-side in `postXCommands.defaults`. Since the POST response is a JSON array (no `aggregateId` echoed back), the only way to retrieve it downstream is via `captureRequestAs` on the post step invocation. `captureRequestAs` captures the resolved request body dict before the HTTP call, under the given key:

```json
{ "step": "postTargetCommands", "captureRequestAs": "targetReq" }
```

Then `targetReq.aggregateId` is available to downstream steps and assertions. Step defs for GET-by-ID endpoints default to these captured keys (e.g. `getTarget` uses `targetReq.aggregateId` as its `pathParams.aggregateId`).

---

## Planned vs implemented

The `PLAN.md` and `Plans/` directory describe the intended final architecture. The current implementation diverges in a few areas:

- **`AddAssertion` validation**: plan requires validating that all step names referenced in an assertion exist in the workflow's current steps. Currently the domain stores any assertion without validation.
- **`TestRunAggregate`**: the plan has an async execution model (`StartRun` → background worker → `RecordStepResult` → `CompleteRun`). The current implementation uses a synchronous `POST /api/workflows/{id}/run` endpoint that always returns HTTP 200.
- **Execution aggregate**: the plan has a separate `executions` aggregate; current code uses `runs`.

When implementing new features, follow `PLAN.md` as the authoritative architecture doc.
