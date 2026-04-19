# Plan: workflow-14-archive-workflow.workflow.json

## Goal

Create a workflow, archive it, GET it, assert isArchived is true.

---

## Target workflow JSON

```json
{
  "name": "Workflow_14_ArchiveWorkflow",
  "steps": [
    { "build": "createWorkflow" },
    { "build": "archiveWorkflow" },
    { "step": "postWorkflowCommands" },
    { "step": "getWorkflow" }
  ],
  "assertions": [
    { "equal": ["getWorkflow.isArchived", "true"] }
  ]
}
```

---

## New step definitions (management.requests.json)

```json
"archiveWorkflow": {
  "accumulateAs": "workflowItems",
  "type": { "static": "ArchiveWorkflow" },
  "defaults": {}
}
```

---

## Notes

- No catalog setup needed — this test is purely about workflow archiving.
- `ArchiveWorkflow` has no payload; the aggregate ID in `postWorkflowCommands` identifies
  the workflow to archive.
