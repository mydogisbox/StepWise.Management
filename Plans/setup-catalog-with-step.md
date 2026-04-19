# Plan: setup-catalog-with-step.workflow.json

## Goal

Shared setup workflow: create a target, create a catalog, create a catalog step referencing
the target. Embedded by workflows 08–13 via `{ "workflow": "SetupCatalogWithStep" }`.

List captures produced here (`listTargets`, `listCatalogs`, `listCatalogSteps`) are available
to the parent workflow's subsequent steps via the step def defaults on `appendStep`,
`insertStepBefore`, etc.

---

## Workflow JSON

```json
{
  "name": "SetupCatalogWithStep",
  "steps": [
    { "build": "createTarget" },
    { "step": "postTargetCommands" },
    { "step": "listTargets" },
    { "build": "createCatalog" },
    { "step": "postCatalogCommands" },
    { "step": "listCatalogs" },
    { "build": "upsertStep" },
    { "step": "postCatalogStepCommands" },
    { "step": "listCatalogSteps" }
  ]
}
```

---

## Notes

- No assertions — shared workflows' assertions are skipped when embedded.
- No `captureAs` needed anywhere — each step runs exactly once.
- List steps follow each post and provide the IDs needed by downstream step def defaults.
  `upsertStep` uses `listTargets[0].id` and `listCatalogs[0].id`; `appendStep` and
  `insertStepBefore` use `listCatalogSteps[0].id` and `listCatalogs[0].id`.
- Single GET steps are omitted — no aggregate state is asserted on in setup.
