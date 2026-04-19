# Plan: workflow-06-create.workflow.json

## Goal

Create a workflow, GET it back, assert the name and that steps is empty.

---

## Target workflow JSON

```json
{
  "name": "Workflow_06_Create",
  "steps": [
    { "build": "createWorkflow", "with": {
        "name": { "static": "Test Workflow 6" }
    }},
    { "step": "postWorkflowCommands" },
    { "step": "getWorkflow" }
  ],
  "assertions": [
    { "equal": ["getWorkflow.name", "Test Workflow 6"] },
    { "empty": "getWorkflow.steps" }
  ]
}
```

---

## New step definitions (management.requests.json)

```json
"createWorkflow": {
  "accumulateAs": "workflowItems",
  "type": { "static": "CreateWorkflow" },
  "defaults": {
    "name": { "generated": "guid" }
  }
},
"postWorkflowCommands": {
  "target": "management",
  "method": "POST",
  "path": "/workflows/commands",
  "defaults": {
    "aggregateId": { "generated": "guid" },
    "commands": { "from": "workflowItems" }
  }
},
"getWorkflow": {
  "target": "management",
  "method": "GET",
  "path": "/workflows/{aggregateId}",
  "defaults": {
    "aggregateId": { "from": "postWorkflowCommands.aggregateId" }
  }
}
```
