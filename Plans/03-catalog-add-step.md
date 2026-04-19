# Plan: 03-catalog-add-step.workflow.json

## Goal

Create a target, create a catalog, create a catalog step referencing the target,
GET the catalog step, assert all step fields.

---

## Target workflow JSON

```json
{
  "name": "Catalog_03_AddStep",
  "steps": [
    { "build": "createTarget" },
    { "step": "postTargetCommands" },
    { "step": "listTargets" },
    { "build": "createCatalog" },
    { "step": "postCatalogCommands" },
    { "step": "listCatalogs" },
    { "build": "upsertStep", "with": {
        "stepName": { "static": "getStatus" },
        "stepDefinition": {
          "method": { "static": "GET" },
          "path": { "static": "/api/status" },
          "defaults": { "static": { "param": "value1" } }
        }
    }},
    { "step": "postCatalogStepCommands" },
    { "step": "getCatalogStep" }
  ],
  "assertions": [
    { "equal": ["getCatalogStep.stepName", "getStatus"] },
    { "equal": ["getCatalogStep.targetId", "listTargets[0].id"] },
    { "equal": ["getCatalogStep.catalogId", "listCatalogs[0].id"] },
    { "equal": ["getCatalogStep.method", "GET"] },
    { "equal": ["getCatalogStep.path", "/api/status"] },
    { "equal": ["getCatalogStep.defaults.param", "value1"] }
  ]
}
```

---

## New step definitions (management.requests.json)

```json
"listTargets": {
  "target": "management",
  "method": "GET",
  "path": "/targets"
},
"listCatalogs": {
  "target": "management",
  "method": "GET",
  "path": "/catalogs"
},
"upsertStep": {
  "accumulateAs": "catalogStepItems",
  "type": { "static": "UpsertStep" },
  "defaults": {
    "catalogId": { "from": "listCatalogs[0].id" },
    "stepName": { "generated": "guid" },
    "stepDefinition": {
      "targetId": { "from": "listTargets[0].id" },
      "method": { "static": "GET" },
      "path": { "static": "/api/status" },
      "defaults": { "static": {} }
    }
  }
},
"postCatalogStepCommands": {
  "target": "management",
  "method": "POST",
  "path": "/catalog-steps/commands",
  "defaults": {
    "aggregateId": { "generated": "guid" },
    "commands": { "from": "catalogStepItems" }
  }
},
"listCatalogSteps": {
  "target": "management",
  "method": "GET",
  "path": "/catalog-steps",
  "defaults": {
    "catalogId": { "from": "postCatalogCommands.aggregateId" }
  }
},
"getCatalogStep": {
  "target": "management",
  "method": "GET",
  "path": "/catalog-steps/{aggregateId}",
  "defaults": {
    "aggregateId": { "from": "postCatalogStepCommands.aggregateId" }
  }
}
```

---

## Notes

- `listTargets` and `listCatalogs` have no parent aggregate — no filter defaults.
- `listCatalogSteps` filters by `catalogId` (its parent aggregate).
- `listTargets[0].id` and `listCatalogs[0].id` are used directly in assertions for
  `getCatalogStep.targetId` and `getCatalogStep.catalogId` — no single GET needed.
- `upsertStep` no longer carries an `id` field — the aggregate identity is the
  `aggregateId` generated in `postCatalogStepCommands`.
- `upsertStep` overrides `stepName`, `method`, `path`, and `stepDefinition.defaults`.
