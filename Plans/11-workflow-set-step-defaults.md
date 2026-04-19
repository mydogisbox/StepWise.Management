# Plan: workflow-11-set-step-defaults.workflow.json

## Goal

Set up a catalog, create a workflow, append a step, set its defaults, GET the workflow,
assert the defaults are stored on the step.

---

## Target workflow JSON

```json
{
  "name": "Workflow_11_SetStepDefaults",
  "steps": [
    { "workflow": "SetupCatalogWithStep" },
    { "build": "createWorkflow" },
    { "build": "appendStep" },
    { "build": "setStepDefaults", "with": {
        "defaults": { "static": { "param": "value1" } }
    }},
    { "step": "postWorkflowCommands" },
    { "step": "getWorkflow" }
  ],
  "assertions": [
    { "count": ["getWorkflow.steps", "1"] },
    { "equal": ["getWorkflow.steps[0].id", "appendStep.step.id"] },
    { "equal": ["getWorkflow.steps[0].defaults.param", "value1"] }
  ]
}
```

---

## New step definitions (management.requests.json)

```json
"setStepDefaults": {
  "accumulateAs": "workflowItems",
  "type": { "static": "SetStepDefaults" },
  "defaults": {
    "id": { "from": "appendStep.step.id" },
    "defaults": { "static": {} }
  }
}
```

---

## Notes

- Catalog setup is embedded via `{ "workflow": "SetupCatalogWithStep" }`.
- `appendStep` runs once — no `captureAs` needed; `setStepDefaults` references
  `appendStep.step.id` via its default.
- The `count` assertion confirms the step count is unchanged.
- `setStepDefaults` is the only way to set per-step execution defaults on a workflow step;
  the catalog step reference (`catalogStepId`, `catalogId`) is immutable after append.
