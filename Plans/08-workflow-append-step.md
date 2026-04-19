# Plan: workflow-08-append-step.workflow.json

## Goal

Set up a catalog, create a workflow, append two steps, GET the workflow,
assert both steps appear in the correct order by workflow step id.

---

## Target workflow JSON

```json
{
  "name": "Workflow_08_AppendStep",
  "steps": [
    { "workflow": "SetupCatalogWithStep" },
    { "build": "createWorkflow" },
    { "build": "appendStep", "captureAs": "appendedStepA", "with": {
        "step": { "defaults": { "static": { "param": "value1" } } }
    }},
    { "build": "appendStep", "captureAs": "appendedStepB", "with": {
        "step": { "defaults": { "static": { "param": "value2" } } }
    }},
    { "step": "postWorkflowCommands" },
    { "step": "getWorkflow" }
  ],
  "assertions": [
    { "equal": ["getWorkflow.steps[0].id", "appendedStepA.step.id"] },
    { "equal": ["getWorkflow.steps[0].defaults.param", "value1"] },
    { "equal": ["getWorkflow.steps[1].id", "appendedStepB.step.id"] },
    { "equal": ["getWorkflow.steps[1].defaults.param", "value2"] }
  ]
}
```

---

## Step def updates (management.requests.json)

## New step definitions (management.requests.json)

```json
"appendStep": {
  "accumulateAs": "workflowItems",
  "type": { "static": "AppendStep" },
  "defaults": {
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

- Catalog setup is embedded via `{ "workflow": "SetupCatalogWithStep" }`. `appendStep`
  defaults filter `listCatalogs` and `listCatalogSteps` by their respective post aggregate
  IDs — no ordering assumptions are made.
- `appendStep` appears twice — both use `captureAs` for clarity.
- Both catalog steps reference the same catalog step from setup; order is verified by workflow step id.
