# Plan: workflow-12-bad-assertion.workflow.json

## Goal

Set up a catalog, create a workflow with a step, add an assertion that references a step that
does not exist in the workflow. Assert the post returns 422.

---

## Target workflow JSON

```json
{
  "name": "Workflow_12_BadAssertion",
  "steps": [
    { "workflow": "SetupCatalogWithStep" },
    { "build": "createWorkflow" },
    { "build": "appendStep" },
    { "build": "addAssertion", "with": {
        "assertion": { "static": { "equal": ["nonExistentStep.id", "appendStep.step.id"] } }
    }},
    { "step": "postWorkflowCommands", "captureAs": "errorResponse" }
  ],
  "assertions": [
    { "equal": ["errorResponse.statusCode", "422"] }
  ]
}
```

---

## New step definitions (management.requests.json)

```json
"addAssertion": {
  "accumulateAs": "workflowItems",
  "type": { "static": "AddAssertion" },
  "defaults": {
    "assertion": { "static": {} }
  }
}
```

---

## Notes

- No `getWorkflow` step — the post is expected to fail so there is nothing to retrieve.
- `postWorkflowCommands` uses `captureAs: "errorResponse"` to capture the 422 response body
  rather than treating the non-2xx as a test failure.
- The bad assertion references `nonExistentStep.id` which is not produced by any step in the
  workflow, making it invalid.
- The `addAssertion` step def default for `assertion` is an empty object; the workflow always
  provides an explicit value via `with`.
