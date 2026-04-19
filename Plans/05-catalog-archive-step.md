# Plan: catalog-05-archive-step.workflow.json

## Goal

Create a target, create a catalog, create a catalog step, archive it, GET the catalog step,
assert `isArchived = true`.

---

## Target workflow JSON

```json
{
  "name": "Catalog_05_ArchiveStep",
  "steps": [
    { "build": "createTarget" },
    { "step": "postTargetCommands" },
    { "step": "listTargets" },
    { "build": "createCatalog" },
    { "step": "postCatalogCommands" },
    { "step": "listCatalogs" },
    { "build": "upsertStep" },
    { "build": "archiveStep" },
    { "step": "postCatalogStepCommands" },
    { "step": "getCatalogStep" }
  ],
  "assertions": [
    { "equal": ["getCatalogStep.isArchived", "true"] }
  ]
}
```

---

## New step definitions (management.requests.json)

```json
"archiveStep": {
  "accumulateAs": "catalogStepItems",
  "type": { "static": "ArchiveStep" },
  "defaults": {}
}
```

---

## Notes

- `upsertStep` and `archiveStep` accumulate into the same `catalogStepItems` list and are
  dispatched together in a single `postCatalogStepCommands` call. Both commands target the
  same catalog step aggregate.
- `archiveStep` carries no payload — it operates on the aggregate itself.
- `listTargets` and `listCatalogs` follow their respective post steps and provide IDs to
  `upsertStep` defaults. `getTarget` and `getCatalog` are omitted — no target or catalog
  properties are asserted on.
- `getCatalogStep` returns the final state; `isArchived` is asserted against `true`.
