# Plan: 04-catalog-upsert-step.workflow.json

## Goal

Create two targets and a catalog, create a catalog step referencing the first target,
upsert the same step to reference the second target with a new method and path.
Assert all fields changed.

---

## Target workflow JSON

```json
{
  "name": "Catalog_04_UpsertStep",
  "steps": [
    { "build": "createTarget" },
    { "step": "postTargetCommands", "captureAs": "postedTarget", "with": {
        "commands": { "static": [{ "type": "CreateTarget", "payload": { "baseUrl": "http://localhost:5000" } }] }
    }},
    { "step": "listTargets" },
    { "build": "createCatalog" },
    { "step": "postCatalogCommands" },
    { "step": "listCatalogs" },
    { "build": "upsertStep", "with": {
        "stepName": { "static": "getStatus" },
        "stepDefinition": {
          "targetId": { "from": "listTargets[0].id" },
          "method": { "static": "GET" },
          "path": { "static": "/api/catalogs" },
          "defaults": { "static": { "param": "value1" } }
        }
    }},
    { "step": "postCatalogStepCommands", "captureAs": "postedStep" },
    { "build": "upsertStep", "with": {
        "stepName": { "static": "getStatus" },
        "stepDefinition": {
          "targetId": { "from": "listTargets[1].id" },
          "method": { "static": "POST" },
          "path": { "static": "/api/catalogs/v2" },
          "defaults": { "static": { "param": "value2" } }
        }
    }},
    { "step": "postCatalogStepCommands", "with": {
        "aggregateId": { "from": "postedStep.aggregateId" }
    }},
    { "step": "getCatalogStep" }
  ],
  "assertions": [
    { "equal": ["getCatalogStep.stepName", "getStatus"] },
    { "equal": ["getCatalogStep.targetId", "listTargets[0].id"] },
    { "equal": ["getCatalogStep.method", "POST"] },
    { "equal": ["getCatalogStep.path", "/api/catalogs/v2"] },
    { "equal": ["getCatalogStep.defaults.param", "value2"] }
  ]
}
```

---

## Notes

- Two separate target aggregates are needed. The first uses the build/post pattern;
  the second uses explicit `commands` in `with` on a second `postTargetCommands` call
  to avoid growing the `targetItems` accumulation.
- `listTargets` follows both posts; `listTargets[0]` is the first target,
  `listTargets[1]` is the second. These are used in the `upsertStep` with-overrides.
- `getTarget captureAs=target2` appears only for the round-trip assertion on
  `getCatalogStep.targetId`. `target1` is not asserted on so its GET is omitted.
- The catalog step is a separate aggregate — the first upsert creates it, the second
  upsert updates it. Two separate `postCatalogStepCommands` calls are used.
- The second `postCatalogStepCommands` overrides `aggregateId` from `postedStep.aggregateId`
  to target the same catalog step aggregate.
- `getCatalogStep` runs twice — the final call's capture is used in assertions.
