# Plan: catalog-02-create-catalog.workflow.json

## Goal

Create a catalog, GET it, assert the name.

---

## Target workflow JSON

```json
{
  "name": "Catalog_02_Create",
  "steps": [
    { "build": "createCatalog", "with": {
        "name": { "static": "Test Catalog 2" }
    }},
    { "step": "postCatalogCommands" },
    { "step": "getCatalog" }
  ],
  "assertions": [{ "equal": ["getCatalog.name", "Test Catalog 2"] }]
}
```

---

## Step definitions (management.requests.json)

```json
"createCatalog": {
  "accumulateAs": "catalogItems",
  "type": { "static": "CreateCatalog" },
  "defaults": {
    "name": { "generated": "guid" }
  }
},
"postCatalogCommands": {
  "target": "management",
  "method": "POST",
  "path": "/catalogs/commands",
  "defaults": {
    "aggregateId": { "generated": "guid" },
    "commands": { "from": "catalogItems" }
  }
},
"getCatalog": {
  "target": "management",
  "method": "GET",
  "path": "/catalogs/{aggregateId}",
  "defaults": {
    "aggregateId": { "from": "postCatalogCommands.aggregateId" }
  }
}
```
