# Plan: workflow-07-rename.workflow.json

## Goal

Create a workflow, rename it, GET it back, assert the new name.

---

## Target workflow JSON

```json
{
  "name": "Workflow_07_Rename",
  "steps": [
    { "build": "createWorkflow" },
    { "build": "renameWorkflow", "with": {
        "name": { "static": "Renamed Workflow 7" }
    }},
    { "step": "postWorkflowCommands" },
    { "step": "getWorkflow" }
  ],
  "assertions": [
    { "equal": ["getWorkflow.name", "Renamed Workflow 7"] }
  ]
}
```

---

## New step definitions (management.requests.json)

```json
"renameWorkflow": {
  "accumulateAs": "workflowItems",
  "type": { "static": "RenameWorkflow" },
  "defaults": {
    "name": { "generated": "guid" }
  }
}
```
