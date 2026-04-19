# Plan: workflow-10-remove-step.workflow.json

## Goal

Set up a catalog, create a workflow, append two steps, remove the first, GET the workflow,
assert only the second step remains.

---

## Target workflow JSON

```json
{
  "name": "Workflow_10_RemoveStep",
  "steps": [
    { "workflow": "SetupCatalogWithStep" },
    { "build": "createWorkflow" },
    { "build": "appendStep", "captureAs": "appendedStepA" },
    { "build": "appendStep", "captureAs": "appendedStepB" },
    { "build": "removeStep", "with": {
        "id": { "from": "appendedStepA.step.id" }
    }},
    { "step": "postWorkflowCommands" },
    { "step": "getWorkflow" }
  ],
  "assertions": [
    { "count": ["getWorkflow.steps", "1"] },
    { "equal": ["getWorkflow.steps[0].id", "appendedStepB.step.id"] }
  ]
}
```

---

## New step definitions (management.requests.json)

```json
"removeStep": {
  "accumulateAs": "workflowItems",
  "type": { "static": "RemoveStep" },
  "defaults": {
    "id": { "from": "appendStep.step.id" }
  }
}
```

---

## Notes

- Catalog setup is embedded via `{ "workflow": "SetupCatalogWithStep" }`.
- `appendStep` appears twice — both use `captureAs` for clarity.
- `removeStep` overrides `id` to target `appendedStepA` specifically; the step def default
  uses `appendStep.payload.step.id` as a fallback for simpler cases.
- The assertion confirms only one step remains and it is `appendedStepB`.
