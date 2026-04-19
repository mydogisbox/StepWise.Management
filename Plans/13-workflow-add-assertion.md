# Plan: workflow-13-add-assertion.workflow.json

## Goal

Set up a catalog, create a workflow with a step, add a valid assertion referencing that step,
GET the workflow, assert the assertion is stored.

---

## Target workflow JSON

```json
{
  "name": "Workflow_13_AddAssertion",
  "steps": [
    { "workflow": "SetupCatalogWithStep" },
    { "build": "createWorkflow" },
    { "build": "appendStep" },
    { "build": "addAssertion", "with": {
        "assertion": { "static": { "equal": ["appendStep.step.id", "appendStep.step.id"] } }
    }},
    { "step": "postWorkflowCommands" },
    { "step": "getWorkflow" }
  ],
  "assertions": [
    { "count": ["getWorkflow.assertions", "1"] },
    { "equal": ["getWorkflow.assertions[0].equal[0]", "appendStep.step.id"] },
    { "equal": ["getWorkflow.assertions[0].equal[1]", "appendStep.step.id"] }
  ]
}
```

---

## Notes

- Catalog setup is embedded via `{ "workflow": "SetupCatalogWithStep" }`.
- `appendStep` runs once — no `captureAs` needed.
- The stored assertion references `appendStep.step.id` on both sides; the point is that the
  assertion is stored and retrievable, not that it is logically meaningful.
- `addAssertion` step def introduced in workflow-12.
