# Plan: execution-18-execute-with-step-defaults.workflow.json

## Goal

Set up a catalog, create a workflow with a step, set step-level defaults, execute it,
poll until complete, assert the execution passed and the step output reflects the defaults.

---

## Target workflow JSON

```json
{
  "name": "Execution_18_ExecuteWithStepDefaults",
  "steps": [
    { "workflow": "SetupCatalogWithStep" },
    { "build": "createWorkflow" },
    { "build": "appendStep" },
    { "build": "setStepDefaults", "with": {
        "defaults": { "static": { "param": "value1" } }
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
    { "empty": "getExecution.errors" },
    { "equal": ["getExecution.stepOutputs[?id=appendStep.step.id].request.param", "value1"] }
  ]
}
```

---

## Notes

- Identical setup to execution-16 with a `setStepDefaults` build added before posting.
- `setStepDefaults` defaults `id` to `appendStep.step.id` so no override is needed.
- The third assertion uses field lookup syntax (`[?id=...]`) to find the step output
  matching the appended step's id, then checks the step-level defaults were merged into
  the request (`stepOutputs[?id=...].request.param`).
