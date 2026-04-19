# Plan authoring rules

## Aggregates and references

- Targets, catalogs, catalog steps, workflows, and executions are separate aggregates with
  separate command and GET endpoints.
- When **building a command** that references a dependency, use the list endpoint of that
  aggregate. List step defaults filter by parent aggregate ID where one exists:
  - `listTargets` (GET /targets) — no parent, no filter defaults
  - `listCatalogs` (GET /catalogs) — no parent, no filter defaults
  - `listCatalogSteps` (GET /catalog-steps) — parent: catalog; defaults `catalogId`
  - `listWorkflows` (GET /workflows) — no parent, no filter defaults
- When only one item exists in a list, `[0]` is fine. When multiple items of the same type
  exist in a test, filter by the known post aggregate ID to avoid ordering assumptions
  (e.g. `listTargets[?id=postedTarget.aggregateId].id`).
- Use single-resource GET responses to assert on an aggregate's **own properties**
  (e.g. `getTarget.baseUrl`, `getCatalog.name`).
- To assert that a **foreign key** was stored correctly on a dependent aggregate
  (e.g. `getCatalogStep.targetId`), compare against the list result (`listTargets[0].id`).
  A single GET of the referenced aggregate is not needed.
- Within-batch references (commands accumulated and posted together) may use build captures
  directly — a GET is not possible before the batch is posted.
- Place the list step immediately after the post step for that aggregate, before any
  dependent aggregate is built. Only include single GET steps when assertions require them.

## Step definitions

- Build step defs use `defaults` for overridable field values, not `payload`:
  ```json
  { "accumulateAs": "...", "type": { "static": "..." }, "defaults": { ... } }
  ```
- HTTP step defs use `defaults` for overridable field values:
  ```json
  { "target": "...", "method": "...", "path": "...", "defaults": { ... } }
  ```
- Never use `payload` as a wrapper inside `defaults` — the fields sit directly under `defaults`.
- Every build step def must have a `defaults` block, even if most tests override it, so the
  step is usable without always specifying `with`.
- Never use a static string for a name or id in a default value. Use `{ "generated": "guid" }`
  to prevent tests accidentally asserting against hardcoded defaults.

## Command endpoints

- `postXCommands` returns `{ status, errors }`. The `aggregateId` is part of the request
  body (generated client-side) and is accessible as a capture on the post step, but it is
  not part of the response.

## Workflow JSON

- `with` overrides use field names directly — no `payload` wrapper:
  ```json
  { "build": "upsertStep", "with": { "stepName": { "static": "getStatus" } } }
  ```
- Use `captureAs` on a step only when the same step runs more than once AND both results are
  needed, or when an alias improves clarity.
- Without `captureAs`, repeated builds of the same step overwrite the individual capture;
  only the last is accessible by step name.

## Assertions

- Never assert on a value that comes from a step def default — override it explicitly with
  `with` and assert on the overridden value.
- Both sides of an assertion are resolved as paths if they contain `.` or `[`; bare strings
  without either are treated as literals.
- Use `count` to assert the exact number of items in a collection when that matters to the test.

## Step definition fields

- `stepDefinition` (inside `upsertStep`) includes `targetId`, `method`, `path`, and `defaults`.
  All four must be present in the step def's `defaults` block.
- `upsertStep` does not carry an `id` field — aggregate identity is the `aggregateId`
  generated in `postCatalogStepCommands`.
- Workflow steps (`appendStep`, `insertStepBefore`) include `id`, `catalogStepId`, `catalogId`,
  and `defaults`. `catalogStepId` defaults to `listCatalogSteps[0].id`;
  `catalogId` defaults to `listCatalogs[0].id`.
- `archiveStep` carries no payload — it operates on the catalog step aggregate itself.
  Both `upsertStep` and `archiveStep` accumulate into `catalogStepItems` and are dispatched
  in the same `postCatalogStepCommands` batch.
- `removeStep` and `setStepDefaults` reference the workflow step by `id`
  (client-generated guid from `appendStep.step.id`).

## Execution

- Executions are a separate aggregate from workflows with their own commands endpoint
  (`/executions/commands`) and GET endpoint (`/executions/{aggregateId}`).
- `startExecution` references the workflow via `listWorkflows[0].id`.
- Execution status values: `completed`, `failed`, `in-progress`.
- Execution is asynchronous — always poll `getExecution` until `status == "completed"`.
- A completed execution with a failed assertion still reaches `"completed"` status;
  `passed` and `errors` carry the result.
- `stepOutputs` items have shape: `{ id, status, errors, request, response }`.
  Step-level defaults are merged into `request`. `status` values: `success`, `failed`, `in-progress`.
- Stored assertions are evaluated against the execution output context — they may reference
  `stepOutputs` fields, not workflow authoring captures.

## Shared workflows

- `SetupCatalogWithStep` produces post and list captures available to parent workflows.
  No GET steps are included — those only appear when assertions require them.
- Shared workflow assertions are skipped when embedded; do not add assertions to shared workflows.
