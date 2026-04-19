# Plan: execution-16-execute.workflow.json

## Goal

Set up a catalog, create a workflow with a step and an assertion, start an execution,
poll until complete, assert it passed with no errors.

---

## Target workflow JSON

```json
{
  "name": "Execution_16_Execute",
  "steps": [
    { "workflow": "SetupCatalogWithStep" },
    { "build": "createWorkflow" },
    { "build": "appendStep" },
    { "build": "addAssertion", "with": {
        "assertion": { "static": { "equal": ["stepOutputs[0].status", "success"] } }
    }},
    { "step": "postWorkflowCommands" },
    { "step": "listWorkflows" },
    { "build": "startExecution" },
    { "step": "postExecutionCommands" },
    {
      "poll": "getExecution",
      "until": { "equal": ["getExecution.status", "completed"] },
      "intervalMs": 500,
      "timeoutMs": 10000
    }
  ],
  "assertions": [
    { "equal": ["getExecution.passed", "true"] },
    { "empty": "getExecution.errors" }
  ]
}
```

---

## New step definitions (management.requests.json)

```json
"listWorkflows": {
  "target": "management",
  "method": "GET",
  "path": "/workflows"
},
"startExecution": {
  "accumulateAs": "executionItems",
  "type": { "static": "StartExecution" },
  "defaults": {
    "workflowId": { "from": "listWorkflows[0].id" }
  }
},
"postExecutionCommands": {
  "target": "management",
  "method": "POST",
  "path": "/executions/commands",
  "defaults": {
    "aggregateId": { "generated": "guid" },
    "commands": { "from": "executionItems" }
  }
},
"getExecution": {
  "target": "management",
  "method": "GET",
  "path": "/executions/{aggregateId}",
  "defaults": {
    "aggregateId": { "from": "postExecutionCommands.aggregateId" }
  }
}
```

---

## Notes

- `listWorkflows` has no parent aggregate — no filter defaults.
- `startExecution` references `listWorkflows[0].id` as the workflow to execute.
- Execution status values: `completed`, `failed`, `in-progress`. Poll terminates on `completed`.
- `stepOutputs` items have shape: `{ id, status, errors, request, response }`.
  `status` values: `success`, `failed`, `in-progress`.
- The stored assertion references `stepOutputs[0].status` which is resolved in the
  execution output context, not the workflow authoring context.
- `getExecution` polls every 500ms until `status == "completed"`, timing out after 10s.

## Open

- Test 16 catalog step setup is not fully specified. The step definition needs to target
  a real endpoint that will return a predictable response so the `stepOutputs[0].status`
  assertion is meaningful. SetupCatalogWithStep uses generic defaults — this test may need
  to override the target URL and path explicitly.
