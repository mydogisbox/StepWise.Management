# Plan: 01-catalog-create-target.workflow.json

## Goal

Create a target, GET it, assert the baseUrl.

---

## Target workflow JSON

```json
{
  "name": "Catalog_01_CreateTarget",
  "steps": [
    { "build": "createTarget", "with": { "baseUrl": { "static": "http://localhost:5000" } } },
    { "step": "postTargetCommands" },
    { "step": "getTarget" }
  ],
  "assertions": [
    { "equal": ["getTarget.baseUrl", "http://localhost:5000"] }
  ]
}
```

---

## New step definitions (management.requests.json)

```json
"createTarget": {
  "accumulateAs": "targetItems",
  "type": { "static": "CreateTarget" },
  "defaults": {
    "baseUrl": { "static": "http://localhost:5000" }
  }
},
"postTargetCommands": {
  "target": "management",
  "method": "POST",
  "path": "/targets/commands",
  "defaults": {
    "aggregateId": { "generated": "guid" },
    "commands": { "from": "targetItems" }
  }
},
"getTarget": {
  "target": "management",
  "method": "GET",
  "path": "/targets/{aggregateId}",
  "defaults": {
    "aggregateId": { "from": "postTargetCommands.aggregateId" }
  }
}
```
