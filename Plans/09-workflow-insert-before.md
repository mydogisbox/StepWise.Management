# Plan: workflow-09-insert-before.workflow.json

## Goal

Set up a catalog, create a workflow, append two steps, insert a third before the second.
Assert the final order by workflow step id.

---

## Target workflow JSON

```json
{
  "name": "Workflow_09_InsertStepBefore",
  "steps": [
    { "workflow": "SetupCatalogWithStep" },
    { "build": "createWorkflow" },
    { "build": "appendStep", "captureAs": "appendedStepA", "with": {
        "step": { "defaults": { "static": { "param": "value1" } } }
    }},
    { "build": "appendStep", "captureAs": "appendedStepB", "with": {
        "step": { "defaults": { "static": { "param": "value2" } } }
    }},
    { "build": "insertStepBefore", "with": {
        "beforeId": { "from": "appendedStepB.step.id" },
        "step": { "defaults": { "static": { "param": "value3" } } }
    }},
    { "step": "postWorkflowCommands" },
    { "step": "getWorkflow" }
  ],
  "assertions": [
    { "equal": ["getWorkflow.steps[0].id", "appendedStepA.step.id"] },
    { "equal": ["getWorkflow.steps[0].defaults.param", "value1"] },
    { "equal": ["getWorkflow.steps[1].id", "insertStepBefore.step.id"] },
    { "equal": ["getWorkflow.steps[1].defaults.param", "value3"] },
    { "equal": ["getWorkflow.steps[2].id", "appendedStepB.step.id"] },
    { "equal": ["getWorkflow.steps[2].defaults.param", "value2"] }
  ]
}
```

---

## New step definitions (management.requests.json)

```json
"insertStepBefore": {
  "accumulateAs": "workflowItems",
  "type": { "static": "InsertStepBefore" },
  "defaults": {
    "beforeId": { "from": "appendStep.step.id" },
    "step": {
      "id": { "generated": "guid" },
      "catalogStepId": { "from": "listCatalogSteps[0].id" },
      "catalogId": { "from": "listCatalogs[0].id" },
      "defaults": { "static": {} }
    }
  }
}
```

---

## Notes

- Catalog setup is embedded via `{ "workflow": "SetupCatalogWithStep" }`.
- `appendStep` appears twice — both use `captureAs` for clarity.
- `insertStepBefore` appears once — no `captureAs` needed; referenced in assertions as `insertStepBefore`.
- `beforeId` is overridden to target `appendedStepB` specifically; the step def default uses
  `appendStep.step.id` as a fallback for simpler cases.
- All assertions verify order by workflow step id rather than catalog step identity.
