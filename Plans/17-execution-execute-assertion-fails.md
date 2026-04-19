# Plan: execution-17-execute-assertion-fails.workflow.json

## Goal

Set up a catalog, create a workflow with a step and a deliberately failing assertion,
execute it, poll until complete, assert the execution failed with errors.

---

## Target workflow JSON

```json
{
  "name": "Execution_17_ExecuteAssertionFails",
  "steps": [
    { "workflow": "SetupCatalogWithStep" },
    { "build": "createWorkflow" },
    { "build": "appendStep" },
    { "build": "addAssertion", "with": {
        "assertion": { "static": { "equal": ["stepOutputs[0].status", "wrong-value"] } }
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
    { "equal": ["getExecution.passed", "false"] },
    { "notEmpty": "getExecution.errors" }
  ]
}
```

---

## Notes

- Identical setup to execution-16 except the stored assertion is deliberately wrong:
  `appendStep.id == "wrong-value"` will never match.
- The execution still completes (status reaches `"completed"`) — it is not an error in the
  infrastructure sense, just a failed assertion in the workflow result.
- Asserts `passed == false` and `errors` is non-empty to confirm the failure is reported.
