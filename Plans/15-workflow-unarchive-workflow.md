# Plan: workflow-15-unarchive-workflow.workflow.json

## Goal

Create a workflow, archive it, unarchive it, GET it, assert isArchived is false.

---

## Target workflow JSON

```json
{
  "name": "Workflow_15_UnarchiveWorkflow",
  "steps": [
    { "build": "createWorkflow" },
    { "build": "archiveWorkflow" },
    { "build": "unarchiveWorkflow" },
    { "step": "postWorkflowCommands" },
    { "step": "getWorkflow" }
  ],
  "assertions": [
    { "equal": ["getWorkflow.isArchived", "false"] }
  ]
}
```

---

## New step definitions (management.requests.json)

```json
"unarchiveWorkflow": {
  "accumulateAs": "workflowItems",
  "type": { "static": "UnarchiveWorkflow" },
  "defaults": {}
}
```

---

## Notes

- No catalog setup needed — this test is purely about workflow archiving state.
- Both `archiveWorkflow` and `unarchiveWorkflow` have no payload; the aggregate ID in
  `postWorkflowCommands` identifies the workflow.
- The archive step is required to establish the archived state before unarchiving.
