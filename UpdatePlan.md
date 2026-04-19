# StepWise.Management — Implementation Plan

## Context

The 18 management integration tests currently use hardcoded static GUIDs, old `targets`/`requests` fields removed from `WorkflowDefinition`, and reference convenience REST endpoints. The user wants:
1. **BUILD pattern** (like `two-orders.workflow.json`) — build steps accumulate; step sends the HTTP request
2. **No dedicated/convenience endpoints in tests** — all mutations through `POST /catalogs/commands` and `POST /workflows/commands`
3. **Generated GUIDs** via `{ "generated": "guid" }`, not hardcoded
4. **Request capture via BUILD** — the build step stores request fields; no `captures["name.request"]` needed

The core challenge: `CreateCatalog { Id, Name }` requires the generated GUID in both `catalogId` (CommandBatch) and `payload.id`. Since `{ "static": [...] }` can't include dynamic values, we add an `"object"` type to `FieldValueDefinition` for constructing nested dynamic objects.

---

## Critical Files

| File | Change |
|---|---|
| `StepWise/src/StepWise.Json/WorkflowDefinition.cs` | Add `Object` to `FieldValueDefinition`; add `IsArchived` to `StepDefinition` |
| `StepWise/src/StepWise.Json/JsonValueResolver.cs` | Handle `Object` case |
| `StepWise/src/StepWise.Json/JsonWorkflowRunner.cs` | `BuildItem`, `ResolveCapturePath`, `ExecuteStepAsync` |
| `StepWise.Management/Domain/Catalogs/CatalogAggregate.cs` | Add `ArchiveStep` command |
| `StepWise.Management/Domain/Workflows/WorkflowAggregate.cs` | ID-based step commands, `WorkflowStepRef` |
| `StepWise.Management/Program.cs` | Fix `WorkflowDefinition` constructor, custom GET /workflows/{id} |
| `tests/.../WorkflowTests/Requests/management.requests.json` | Redesign step defs |
| `tests/.../WorkflowTests/targets.json` | New file |
| `tests/.../JsonManagementTests.cs` | Add `RequestPaths` / `TargetsPath` overrides |
| 18 workflow JSON files | Rewrite with BUILD pattern |

---

## 1. StepWise.Json Changes

### A. `FieldValueDefinition` — add `Object` type

In `WorkflowDefinition.cs`:
```csharp
[JsonPropertyName("object")]
public Dictionary<string, FieldValueDefinition>? Object { get; init; }
```

Also add `IsArchived` to `StepDefinition`:
```csharp
public bool IsArchived { get; init; } = false;
```

### B. `JsonValueResolver` — handle `Object` case

Add before the final `throw` in `Resolve`:
```csharp
if (def.Object is not null)
    return new ObjectJsonValue(def.Object);
```

Add new class:
```csharp
public sealed class ObjectJsonValue(Dictionary<string, FieldValueDefinition> fields) : IJsonFieldValue
{
    public object? Resolve(Dictionary<string, object?> captures)
        => fields.ToDictionary(
            kv => kv.Key,
            kv => JsonValueResolver.Resolve(kv.Value).Resolve(captures),
            StringComparer.OrdinalIgnoreCase);
}
```

### C. `BuildItem` — support `captureAs` and store direct capture

```csharp
var captureName = invocation.CaptureAs ?? buildName;
var accumulationKey = $"__build__{captureName}";
// ... accumulate ...
captures[captureName] = resolvedFields;   // enables from: "captureName.fieldName"
return new StepResult(captureName, resolvedFields);
```

### D. `ResolveCapturePath` — greedy prefix + recursive traversal

Replace the current implementation:
```csharp
internal static object? ResolveCapturePath(string path, Dictionary<string, object?> captures)
{
    int lastDot = path.Length;
    while ((lastDot = path.LastIndexOf('.', lastDot - 1)) > 0)
    {
        var key       = path[..lastDot];
        var fieldPath = path[(lastDot + 1)..];
        if (!captures.TryGetValue(key, out var captured)) continue;
        return ResolveFieldPath(captured, fieldPath);
    }
    return captures.TryGetValue(path, out var v) ? v : null;
}

private static object? ResolveFieldPath(object? value, string fieldPath)
{
    var dot   = fieldPath.IndexOf('.');
    var field = dot < 0 ? fieldPath : fieldPath[..dot];
    var rest  = dot < 0 ? null : fieldPath[(dot + 1)..];

    object? fieldValue;
    if (value is Dictionary<string, JsonElement> dictEl)
    {
        var key = dictEl.Keys.FirstOrDefault(k =>
            string.Equals(k, field, StringComparison.OrdinalIgnoreCase));
        fieldValue = key is null ? null : JsonValueResolver.JsonElementToObject(dictEl[key]);
    }
    else if (value is Dictionary<string, object?> dictObj)
    {
        var key = dictObj.Keys.FirstOrDefault(k =>
            string.Equals(k, field, StringComparison.OrdinalIgnoreCase));
        fieldValue = key is null ? null : dictObj[key];
    }
    else return null;

    return rest is null ? fieldValue : ResolveFieldPath(fieldValue, rest);
}
```

Enables paths like `"appendStep.payload.stepId"` and `"catalogMeta.catalogId"`.

### E. `ExecuteStepAsync` — handle array JSON responses

Replace the response deserialization:
```csharp
Dictionary<string, JsonElement>? responseDict = null;
if (!string.IsNullOrEmpty(responseJson))
{
    using var doc = JsonDocument.Parse(responseJson);
    if (doc.RootElement.ValueKind == JsonValueKind.Object)
        responseDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            responseJson, HttpExecutor.JsonOptions);
}
captures[captureName] = responseDict;
return new StepResult(captureName, responseDict);
```

---

## 2. CatalogAggregate Changes

Add `ArchiveStep` command + `StepArchived` event:
```csharp
public record StepArchived(string CatalogId, string StepName) : CatalogEvent;
public record ArchiveStep(string StepName);

public static Result<IEnumerable<CatalogEvent>> Handle(CatalogState state, ArchiveStep cmd)
{
    if (!state.Steps.ContainsKey(cmd.StepName))
        return $"Step '{cmd.StepName}' does not exist.";
    return new CatalogEvent[] { new StepArchived(state.Id, cmd.StepName) };
}
```

Apply case:
```csharp
StepArchived evt =>
    state! with { Steps = new Dictionary<string, StepDefinition>(state.Steps)
        { [evt.StepName] = state.Steps[evt.StepName] with { IsArchived = true } } },
```

Update `Dispatch`, `DeserializeCommand`, `DeserializeEvent`.

---

## 3. WorkflowAggregate Changes

### A. New `WorkflowStepRef` type
```csharp
public record WorkflowStepRef(string Id, string StepName, string CatalogId, bool IsArchived = false);
```

### B. Update `WorkflowState`
```csharp
public record WorkflowState(
    string Id, string Name, List<string> CatalogIds,
    List<WorkflowStepRef> Steps,   // was List<StepInvocation>
    List<AssertionDefinition> Assertions, bool IsArchived);
```

### C. Replace step commands/events

**Remove:** `AddStep`, `UpdateStep(int Index, ...)`, `RemoveStep(int Index)` and their events.

**Add commands:**
```csharp
public record AppendStep(string StepId, string StepName, string CatalogId);
public record InsertStepBefore(string BeforeId, string StepId, string StepName, string CatalogId);
public record ArchiveWorkflowStep(string StepId);
public record UpdateWorkflowStep(string StepId, string StepName, string CatalogId);
```

**Add events:**
```csharp
public record WorkflowStepAppended(string Id, WorkflowStepRef Step) : WorkflowEvent;
public record WorkflowStepInserted(string Id, string BeforeId, WorkflowStepRef Step) : WorkflowEvent;
public record WorkflowStepArchived(string Id, string StepId) : WorkflowEvent;
public record WorkflowStepUpdated(string Id, string StepId, WorkflowStepRef Step) : WorkflowEvent;
```

Apply cases:
```csharp
case WorkflowStepAppended evt:
    return state! with { Steps = new List<WorkflowStepRef>(state.Steps) { evt.Step } };

case WorkflowStepInserted evt:
{
    var steps = new List<WorkflowStepRef>(state!.Steps);
    var idx = steps.FindIndex(s => s.Id == evt.BeforeId);
    steps.Insert(idx, evt.Step);
    return state with { Steps = steps };
}

case WorkflowStepArchived evt:
    return state! with { Steps = state.Steps
        .Select(s => s.Id == evt.StepId ? s with { IsArchived = true } : s).ToList() };

case WorkflowStepUpdated evt:
    return state! with { Steps = state.Steps
        .Select(s => s.Id == evt.StepId ? evt.Step : s).ToList() };
```

### D. `AddAssertion` validation
```csharp
public static Result<IEnumerable<WorkflowEvent>> Handle(WorkflowState state, AddAssertion cmd)
{
    var validStepNames = state.Steps
        .Where(s => !s.IsArchived)
        .Select(s => s.StepName)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    foreach (var expr in new[] { cmd.AssertionDefinition.NotEmpty,
                                  cmd.AssertionDefinition.Empty,
                                  cmd.AssertionDefinition.Single }
                         .OfType<string>()
                         .Where(e => e.Contains('.')))
    {
        var stepName = expr[..expr.IndexOf('.')];
        if (!validStepNames.Contains(stepName))
            return $"Step '{stepName}' is not in this workflow.";
    }
    return new WorkflowEvent[] { new AssertionAdded(state.Id, cmd.AssertionDefinition) };
}
```

Update `Dispatch`, `DeserializeCommand`, `DeserializeEvent` for all new types.

---

## 4. Program.cs Changes

### A. Replace `MapAggregate` for workflows with manual endpoints

Remove `app.MapAggregate(name: "workflows", ...)`. Add:
```csharp
app.MapPost("/workflows/commands", async (CommandBatch batch) =>
{
    var result = await workflowHandler.ExecuteAsync(
        batch, WorkflowAggregate.DeserializeCommand, WorkflowAggregate.DeserializeEvent);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.UnprocessableEntity(new { error = result.Error });
});

app.MapGet("/workflows/{id}", async (string id) =>
{
    var events = await eventStore.LoadAsync($"workflows/{id}");
    if (events.Count == 0) return Results.NotFound($"Workflow '{id}' not found.");
    var state = Aggregate.Fold<WorkflowState, WorkflowEvent>(
        events.Select(e => WorkflowAggregate.DeserializeEvent(e.EventType, e.Payload)),
        WorkflowAggregate.Apply)!;
    var projected = state with { Steps = state.Steps.Where(s => !s.IsArchived).ToList() };
    return Results.Ok(projected);
});
```

### B. Fix `/api/workflows/{id}/run` endpoint

```csharp
var mergedTargetUrls = mergedTargets.ToDictionary(
    kv => kv.Key, kv => kv.Value.BaseUrl, StringComparer.OrdinalIgnoreCase);
if (targetOverrides is not null)
    foreach (var (k, v) in targetOverrides)
        mergedTargetUrls[k] = v;

var workflowDef = new WorkflowDefinition(
    Name: workflowState.Name,
    Steps: workflowState.Steps
        .Where(r => !r.IsArchived)
        .Select(r => new StepInvocation { Step = r.StepName })
        .ToList(),
    Assertions: workflowState.Assertions.Count > 0 ? workflowState.Assertions : null);

result = await JsonWorkflowRunner.RunAsync(workflowDef, mergedStepDefs, mergedTargetUrls);
```

### C. Return `passed` at top level from run endpoint

```csharp
return result.Passed
    ? Results.Ok(new { runId, passed = result.Passed, result })
    : Results.UnprocessableEntity(new { runId, passed = result.Passed, result });
```

---

## 5. Test Infrastructure

### A. `targets.json` (new file)
`tests/StepWise.Management.Tests/WorkflowTests/targets.json`:
```json
{ "management": "http://localhost:5000" }
```

### B. `JsonManagementTests.cs` — add overrides
```csharp
public class JsonManagementTests : JsonWorkflowTestBase
{
    protected override IReadOnlyList<string> RequestPaths =>
        ["WorkflowTests/Requests/management.requests.json"];
    protected override string? TargetsPath => "WorkflowTests/targets.json";
    // all 18 [Fact] methods unchanged
}
```

### C. `management.requests.json` — complete redesign

Build-only step defs (all fields required by `StepDefinition` but unused by `BuildItem`):
```json
{
  "steps": {
    "catalogMeta":         { "target": "management", "method": "POST", "path": "/catalogs/commands" },
    "createCatalog":       { "target": "management", "method": "POST", "path": "/catalogs/commands" },
    "upsertStep":          { "target": "management", "method": "POST", "path": "/catalogs/commands" },
    "upsertTarget":        { "target": "management", "method": "POST", "path": "/catalogs/commands" },
    "archiveCatalogStep":  { "target": "management", "method": "POST", "path": "/catalogs/commands" },
    "workflowMeta":        { "target": "management", "method": "POST", "path": "/workflows/commands" },
    "createWorkflow":      { "target": "management", "method": "POST", "path": "/workflows/commands" },
    "renameWorkflow":      { "target": "management", "method": "POST", "path": "/workflows/commands" },
    "addCatalog":          { "target": "management", "method": "POST", "path": "/workflows/commands" },
    "appendStep":          { "target": "management", "method": "POST", "path": "/workflows/commands" },
    "insertStepBefore":    { "target": "management", "method": "POST", "path": "/workflows/commands" },
    "archiveWorkflowStep": { "target": "management", "method": "POST", "path": "/workflows/commands" },
    "updateWorkflowStep":  { "target": "management", "method": "POST", "path": "/workflows/commands" },
    "addAssertion":        { "target": "management", "method": "POST", "path": "/workflows/commands" },
    "archiveWorkflow":     { "target": "management", "method": "POST", "path": "/workflows/commands" },
    "unarchiveWorkflow":   { "target": "management", "method": "POST", "path": "/workflows/commands" },

    "postCatalogCommands": {
      "target": "management", "method": "POST", "path": "/catalogs/commands",
      "defaults": { "catalogId": { "from": "catalogMeta.catalogId" } }
    },
    "getCatalog": {
      "target": "management", "method": "GET", "path": "/catalogs/{catalogId}",
      "defaults": { "catalogId": { "from": "catalogMeta.catalogId" } }
    },
    "postWorkflowCommands": {
      "target": "management", "method": "POST", "path": "/workflows/commands",
      "defaults": { "catalogId": { "from": "workflowMeta.workflowId" } }
    },
    "getWorkflow": {
      "target": "management", "method": "GET", "path": "/workflows/{workflowId}",
      "defaults": { "workflowId": { "from": "workflowMeta.workflowId" } }
    },
    "runWorkflow": {
      "target": "management", "method": "POST", "path": "/api/workflows/{workflowId}/run",
      "defaults": { "workflowId": { "from": "workflowMeta.workflowId" } }
    }
  }
}
```

---

## 6. Workflow JSON Files — BUILD Pattern

### Common reusable sequences

**Create catalog:**
```json
{ "build": "catalogMeta", "with": { "catalogId": { "generated": "guid" } } },
{ "build": "createCatalog", "with": {
    "type": { "static": "CreateCatalog" },
    "payload": { "object": { "id": { "from": "catalogMeta.catalogId" }, "name": { "static": "Test Catalog" } }}
}},
{ "step": "postCatalogCommands", "with": { "commands": { "from": "__build__createCatalog" } } }
```

**Upsert catalog step:**
```json
{ "build": "upsertStep", "with": {
    "type": { "static": "UpsertStep" },
    "payload": { "static": { "stepName": "myStep", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/catalogs" } } }
}},
{ "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } }
```

**Create workflow:**
```json
{ "build": "workflowMeta", "with": { "workflowId": { "generated": "guid" } } },
{ "build": "createWorkflow", "with": {
    "type": { "static": "CreateWorkflow" },
    "payload": { "object": { "id": { "from": "workflowMeta.workflowId" }, "name": { "static": "Test WF" } }}
}},
{ "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__createWorkflow" } } }
```

**Append step to workflow:**
```json
{ "build": "appendStep", "with": { "type": { "static": "AppendStep" }, "payload": { "object": {
    "stepId": { "generated": "guid" }, "stepName": { "static": "myStep" }, "catalogId": { "from": "catalogMeta.catalogId" }
}}}},
{ "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__appendStep" } } }
```
Access step ID later via: `from: "appendStep.payload.stepId"`

**Add catalog to workflow:**
```json
{ "build": "addCatalog", "with": { "type": { "static": "AddCatalog" }, "payload": { "object": { "catalogId": { "from": "catalogMeta.catalogId" } }}}},
{ "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__addCatalog" } } }
```

---

### catalog-01-create.workflow.json
```json
{
  "name": "Catalog_01_Create",
  "steps": [
    { "build": "catalogMeta", "with": { "catalogId": { "generated": "guid" } } },
    { "build": "createCatalog", "with": { "type": { "static": "CreateCatalog" }, "payload": { "object": { "id": { "from": "catalogMeta.catalogId" }, "name": { "static": "Test Catalog 1" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__createCatalog" } } },
    { "step": "getCatalog" }
  ],
  "assertions": [{ "equal": ["getCatalog.name", "Test Catalog 1"] }]
}
```

### catalog-02-add-step.workflow.json
```json
{
  "name": "Catalog_02_AddStep",
  "steps": [
    { "build": "catalogMeta", "with": { "catalogId": { "generated": "guid" } } },
    { "build": "createCatalog", "with": { "type": { "static": "CreateCatalog" }, "payload": { "object": { "id": { "from": "catalogMeta.catalogId" }, "name": { "static": "Test Catalog" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__createCatalog" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "step", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/catalogs" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "step": "getCatalog" }
  ],
  "assertions": [{ "notEmpty": "getCatalog.steps" }]
}
```

### catalog-03-upsert-step.workflow.json
```json
{
  "name": "Catalog_03_UpsertStep",
  "steps": [
    { "build": "catalogMeta", "with": { "catalogId": { "generated": "guid" } } },
    { "build": "createCatalog", "with": { "type": { "static": "CreateCatalog" }, "payload": { "object": { "id": { "from": "catalogMeta.catalogId" }, "name": { "static": "Test Catalog" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__createCatalog" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "getStatus", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/catalogs" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "getStatus", "stepDefinition": { "target": "management", "method": "POST", "path": "/api/catalogs" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "step": "getCatalog" }
  ],
  "assertions": [{ "notEmpty": "getCatalog.steps" }]
}
```

### catalog-04-archive-step.workflow.json
```json
{
  "name": "Catalog_04_ArchiveStep",
  "steps": [
    { "build": "catalogMeta", "with": { "catalogId": { "generated": "guid" } } },
    { "build": "createCatalog", "with": { "type": { "static": "CreateCatalog" }, "payload": { "object": { "id": { "from": "catalogMeta.catalogId" }, "name": { "static": "Test Catalog" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__createCatalog" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "step", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/catalogs" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "build": "archiveCatalogStep", "with": { "type": { "static": "ArchiveStep" }, "payload": { "static": { "stepName": "step" } }}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__archiveCatalogStep" } } },
    { "step": "getCatalog" }
  ],
  "assertions": [{ "notEmpty": "getCatalog.steps" }]
}
```

### catalog-05-add-target.workflow.json
```json
{
  "name": "Catalog_05_AddTarget",
  "steps": [
    { "build": "catalogMeta", "with": { "catalogId": { "generated": "guid" } } },
    { "build": "createCatalog", "with": { "type": { "static": "CreateCatalog" }, "payload": { "object": { "id": { "from": "catalogMeta.catalogId" }, "name": { "static": "Test Catalog" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__createCatalog" } } },
    { "build": "upsertTarget", "with": { "type": { "static": "UpsertTarget" }, "payload": { "static": { "name": "management", "targetDefinition": { "baseUrl": "http://localhost:5000" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertTarget" } } },
    { "step": "getCatalog" }
  ],
  "assertions": [{ "notEmpty": "getCatalog.targets" }]
}
```

### workflow-06-create.workflow.json
```json
{
  "name": "Workflow_06_Create",
  "steps": [
    { "build": "workflowMeta", "with": { "workflowId": { "generated": "guid" } } },
    { "build": "createWorkflow", "with": { "type": { "static": "CreateWorkflow" }, "payload": { "object": { "id": { "from": "workflowMeta.workflowId" }, "name": { "static": "Test Workflow 6" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__createWorkflow" } } },
    { "step": "getWorkflow" }
  ],
  "assertions": [
    { "equal": ["getWorkflow.name", "Test Workflow 6"] },
    { "empty": "getWorkflow.steps" }
  ]
}
```

### workflow-07-rename.workflow.json
```json
{
  "name": "Workflow_07_Rename",
  "steps": [
    { "build": "workflowMeta", "with": { "workflowId": { "generated": "guid" } } },
    { "build": "createWorkflow", "with": { "type": { "static": "CreateWorkflow" }, "payload": { "object": { "id": { "from": "workflowMeta.workflowId" }, "name": { "static": "Test WF" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__createWorkflow" } } },
    { "build": "renameWorkflow", "with": { "type": { "static": "RenameWorkflow" }, "payload": { "static": { "name": "Renamed Workflow 7" } }}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__renameWorkflow" } } },
    { "step": "getWorkflow" }
  ],
  "assertions": [{ "equal": ["getWorkflow.name", "Renamed Workflow 7"] }]
}
```

### workflow-08-append-step.workflow.json
```json
{
  "name": "Workflow_08_AppendStep",
  "steps": [
    { "build": "catalogMeta", "with": { "catalogId": { "generated": "guid" } } },
    { "build": "createCatalog", "with": { "type": { "static": "CreateCatalog" }, "payload": { "object": { "id": { "from": "catalogMeta.catalogId" }, "name": { "static": "Test Catalog" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__createCatalog" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "getStatus", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/catalogs" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "build": "workflowMeta", "with": { "workflowId": { "generated": "guid" } } },
    { "build": "createWorkflow", "with": { "type": { "static": "CreateWorkflow" }, "payload": { "object": { "id": { "from": "workflowMeta.workflowId" }, "name": { "static": "Test WF 8" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__createWorkflow" } } },
    { "build": "appendStep", "with": { "type": { "static": "AppendStep" }, "payload": { "object": { "stepId": { "generated": "guid" }, "stepName": { "static": "getStatus" }, "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__appendStep" } } },
    { "step": "getWorkflow" }
  ],
  "assertions": [{ "notEmpty": "getWorkflow.steps" }]
}
```

### workflow-09-insert-before.workflow.json
```json
{
  "name": "Workflow_09_InsertStepBefore",
  "steps": [
    { "build": "catalogMeta", "with": { "catalogId": { "generated": "guid" } } },
    { "build": "createCatalog", "with": { "type": { "static": "CreateCatalog" }, "payload": { "object": { "id": { "from": "catalogMeta.catalogId" }, "name": { "static": "Test Catalog" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__createCatalog" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "stepA", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/catalogs" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "stepB", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/catalogs" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "stepC", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/catalogs" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "build": "workflowMeta", "with": { "workflowId": { "generated": "guid" } } },
    { "build": "createWorkflow", "with": { "type": { "static": "CreateWorkflow" }, "payload": { "object": { "id": { "from": "workflowMeta.workflowId" }, "name": { "static": "Test WF 9" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__createWorkflow" } } },
    { "build": "appendStep", "with": { "type": { "static": "AppendStep" }, "payload": { "object": { "stepId": { "generated": "guid" }, "stepName": { "static": "stepA" }, "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__appendStep" } } },
    { "build": "appendStep", "captureAs": "appendStepB", "with": { "type": { "static": "AppendStep" }, "payload": { "object": { "stepId": { "generated": "guid" }, "stepName": { "static": "stepB" }, "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__appendStepB" } } },
    { "build": "insertStepBefore", "with": { "type": { "static": "InsertStepBefore" }, "payload": { "object": { "beforeId": { "from": "appendStepB.payload.stepId" }, "stepId": { "generated": "guid" }, "stepName": { "static": "stepC" }, "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__insertStepBefore" } } },
    { "step": "getWorkflow" }
  ],
  "assertions": [{ "notEmpty": "getWorkflow.steps" }]
}
```

> `captureAs: "appendStepB"` on the build step stores it under `appendStepB` and `__build__appendStepB`.

### workflow-10-remove-step.workflow.json
```json
{
  "name": "Workflow_10_RemoveStep",
  "steps": [
    { "build": "catalogMeta", "with": { "catalogId": { "generated": "guid" } } },
    { "build": "createCatalog", "with": { "type": { "static": "CreateCatalog" }, "payload": { "object": { "id": { "from": "catalogMeta.catalogId" }, "name": { "static": "Test Catalog" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__createCatalog" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "step", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/catalogs" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "build": "workflowMeta", "with": { "workflowId": { "generated": "guid" } } },
    { "build": "createWorkflow", "with": { "type": { "static": "CreateWorkflow" }, "payload": { "object": { "id": { "from": "workflowMeta.workflowId" }, "name": { "static": "Test WF 10" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__createWorkflow" } } },
    { "build": "appendStep", "with": { "type": { "static": "AppendStep" }, "payload": { "object": { "stepId": { "generated": "guid" }, "stepName": { "static": "step" }, "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__appendStep" } } },
    { "build": "archiveWorkflowStep", "with": { "type": { "static": "ArchiveWorkflowStep" }, "payload": { "object": { "stepId": { "from": "appendStep.payload.stepId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__archiveWorkflowStep" } } },
    { "step": "getWorkflow" }
  ],
  "assertions": [{ "empty": "getWorkflow.steps" }]
}
```

### workflow-11-update-step.workflow.json
```json
{
  "name": "Workflow_11_UpdateStep",
  "steps": [
    { "build": "catalogMeta", "with": { "catalogId": { "generated": "guid" } } },
    { "build": "createCatalog", "with": { "type": { "static": "CreateCatalog" }, "payload": { "object": { "id": { "from": "catalogMeta.catalogId" }, "name": { "static": "Test Catalog" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__createCatalog" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "stepA", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/catalogs" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "stepB", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/workflows" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "build": "workflowMeta", "with": { "workflowId": { "generated": "guid" } } },
    { "build": "createWorkflow", "with": { "type": { "static": "CreateWorkflow" }, "payload": { "object": { "id": { "from": "workflowMeta.workflowId" }, "name": { "static": "Test WF 11" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__createWorkflow" } } },
    { "build": "appendStep", "with": { "type": { "static": "AppendStep" }, "payload": { "object": { "stepId": { "generated": "guid" }, "stepName": { "static": "stepA" }, "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__appendStep" } } },
    { "build": "updateWorkflowStep", "with": { "type": { "static": "UpdateWorkflowStep" }, "payload": { "object": { "stepId": { "from": "appendStep.payload.stepId" }, "stepName": { "static": "stepB" }, "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__updateWorkflowStep" } } },
    { "step": "getWorkflow" }
  ],
  "assertions": [{ "notEmpty": "getWorkflow.steps" }]
}
```

### workflow-12-bad-assertion.workflow.json
```json
{
  "name": "Workflow_12_BadAssertion",
  "steps": [
    { "build": "catalogMeta", "with": { "catalogId": { "generated": "guid" } } },
    { "build": "createCatalog", "with": { "type": { "static": "CreateCatalog" }, "payload": { "object": { "id": { "from": "catalogMeta.catalogId" }, "name": { "static": "Test Catalog" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__createCatalog" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "ping", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/catalogs" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "build": "workflowMeta", "with": { "workflowId": { "generated": "guid" } } },
    { "build": "createWorkflow", "with": { "type": { "static": "CreateWorkflow" }, "payload": { "object": { "id": { "from": "workflowMeta.workflowId" }, "name": { "static": "Test WF 12" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__createWorkflow" } } },
    { "build": "appendStep", "with": { "type": { "static": "AppendStep" }, "payload": { "object": { "stepId": { "generated": "guid" }, "stepName": { "static": "ping" }, "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__appendStep" } } },
    { "build": "addAssertion", "with": { "type": { "static": "AddAssertion" }, "payload": { "static": { "assertionDefinition": { "notEmpty": "nonExistentStep.field" } } }}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__addAssertion" } } }
  ]
}
```

### workflow-13-add-assertion.workflow.json
```json
{
  "name": "Workflow_13_AddAssertion",
  "steps": [
    { "build": "catalogMeta", "with": { "catalogId": { "generated": "guid" } } },
    { "build": "createCatalog", "with": { "type": { "static": "CreateCatalog" }, "payload": { "object": { "id": { "from": "catalogMeta.catalogId" }, "name": { "static": "Test Catalog" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__createCatalog" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "ping", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/catalogs" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "build": "workflowMeta", "with": { "workflowId": { "generated": "guid" } } },
    { "build": "createWorkflow", "with": { "type": { "static": "CreateWorkflow" }, "payload": { "object": { "id": { "from": "workflowMeta.workflowId" }, "name": { "static": "Test WF 13" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__createWorkflow" } } },
    { "build": "appendStep", "with": { "type": { "static": "AppendStep" }, "payload": { "object": { "stepId": { "generated": "guid" }, "stepName": { "static": "ping" }, "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__appendStep" } } },
    { "build": "addAssertion", "with": { "type": { "static": "AddAssertion" }, "payload": { "static": { "assertionDefinition": { "notEmpty": "ping.field" } } }}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__addAssertion" } } },
    { "step": "getWorkflow" }
  ],
  "assertions": [{ "notEmpty": "getWorkflow.assertions" }]
}
```

### workflow-14-archive.workflow.json
```json
{
  "name": "Workflow_14_Archive",
  "steps": [
    { "build": "workflowMeta", "with": { "workflowId": { "generated": "guid" } } },
    { "build": "createWorkflow", "with": { "type": { "static": "CreateWorkflow" }, "payload": { "object": { "id": { "from": "workflowMeta.workflowId" }, "name": { "static": "Test WF 14" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__createWorkflow" } } },
    { "build": "archiveWorkflow", "with": { "type": { "static": "ArchiveWorkflow" }, "payload": { "static": {} }}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__archiveWorkflow" } } },
    { "step": "getWorkflow" }
  ],
  "assertions": [{ "equal": ["getWorkflow.isArchived", "true"] }]
}
```

### workflow-15-unarchive.workflow.json
```json
{
  "name": "Workflow_15_Unarchive",
  "steps": [
    { "build": "workflowMeta", "with": { "workflowId": { "generated": "guid" } } },
    { "build": "createWorkflow", "with": { "type": { "static": "CreateWorkflow" }, "payload": { "object": { "id": { "from": "workflowMeta.workflowId" }, "name": { "static": "Test WF 15" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__createWorkflow" } } },
    { "build": "archiveWorkflow", "with": { "type": { "static": "ArchiveWorkflow" }, "payload": { "static": {} }}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__archiveWorkflow" } } },
    { "build": "unarchiveWorkflow", "with": { "type": { "static": "UnarchiveWorkflow" }, "payload": { "static": {} }}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__unarchiveWorkflow" } } },
    { "step": "getWorkflow" }
  ],
  "assertions": [{ "equal": ["getWorkflow.isArchived", "false"] }]
}
```

### execution-16-run.workflow.json
```json
{
  "name": "Execution_16_RunWorkflow",
  "steps": [
    { "build": "catalogMeta", "with": { "catalogId": { "generated": "guid" } } },
    { "build": "createCatalog", "with": { "type": { "static": "CreateCatalog" }, "payload": { "object": { "id": { "from": "catalogMeta.catalogId" }, "name": { "static": "Test Catalog" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__createCatalog" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "getCatalogById", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/catalogs" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "build": "upsertTarget", "with": { "type": { "static": "UpsertTarget" }, "payload": { "static": { "name": "management", "targetDefinition": { "baseUrl": "http://localhost:5000" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertTarget" } } },
    { "build": "workflowMeta", "with": { "workflowId": { "generated": "guid" } } },
    { "build": "createWorkflow", "with": { "type": { "static": "CreateWorkflow" }, "payload": { "object": { "id": { "from": "workflowMeta.workflowId" }, "name": { "static": "Test WF 16" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__createWorkflow" } } },
    { "build": "addCatalog", "with": { "type": { "static": "AddCatalog" }, "payload": { "object": { "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__addCatalog" } } },
    { "build": "appendStep", "with": { "type": { "static": "AppendStep" }, "payload": { "object": { "stepId": { "generated": "guid" }, "stepName": { "static": "getCatalogById" }, "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__appendStep" } } },
    { "step": "runWorkflow" }
  ],
  "assertions": [{ "equal": ["runWorkflow.passed", "true"] }]
}
```

### execution-17-cross-reference.workflow.json
```json
{
  "name": "Execution_17_CrossReference",
  "steps": [
    { "build": "catalogMeta", "with": { "catalogId": { "generated": "guid" } } },
    { "build": "createCatalog", "with": { "type": { "static": "CreateCatalog" }, "payload": { "object": { "id": { "from": "catalogMeta.catalogId" }, "name": { "static": "Test Catalog" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__createCatalog" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "step1", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/catalogs" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "step2", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/workflows" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "build": "upsertTarget", "with": { "type": { "static": "UpsertTarget" }, "payload": { "static": { "name": "management", "targetDefinition": { "baseUrl": "http://localhost:5000" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertTarget" } } },
    { "build": "workflowMeta", "with": { "workflowId": { "generated": "guid" } } },
    { "build": "createWorkflow", "with": { "type": { "static": "CreateWorkflow" }, "payload": { "object": { "id": { "from": "workflowMeta.workflowId" }, "name": { "static": "Test WF 17" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__createWorkflow" } } },
    { "build": "addCatalog", "with": { "type": { "static": "AddCatalog" }, "payload": { "object": { "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__addCatalog" } } },
    { "build": "appendStep", "with": { "type": { "static": "AppendStep" }, "payload": { "object": { "stepId": { "generated": "guid" }, "stepName": { "static": "step1" }, "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__appendStep" } } },
    { "build": "appendStep", "captureAs": "appendStep2", "with": { "type": { "static": "AppendStep" }, "payload": { "object": { "stepId": { "generated": "guid" }, "stepName": { "static": "step2" }, "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__appendStep2" } } },
    { "build": "addAssertion", "with": { "type": { "static": "AddAssertion" }, "payload": { "static": { "assertionDefinition": { "notEmpty": "step1.id" } } }}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__addAssertion" } } },
    { "step": "runWorkflow" }
  ],
  "assertions": [{ "equal": ["runWorkflow.passed", "true"] }]
}
```

### execution-18-assertion.workflow.json
```json
{
  "name": "Execution_18_StoredAssertion",
  "steps": [
    { "build": "catalogMeta", "with": { "catalogId": { "generated": "guid" } } },
    { "build": "createCatalog", "with": { "type": { "static": "CreateCatalog" }, "payload": { "object": { "id": { "from": "catalogMeta.catalogId" }, "name": { "static": "Test Catalog" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__createCatalog" } } },
    { "build": "upsertStep", "with": { "type": { "static": "UpsertStep" }, "payload": { "static": { "stepName": "getCatalogById", "stepDefinition": { "target": "management", "method": "GET", "path": "/api/catalogs" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertStep" } } },
    { "build": "upsertTarget", "with": { "type": { "static": "UpsertTarget" }, "payload": { "static": { "name": "management", "targetDefinition": { "baseUrl": "http://localhost:5000" } }}}},
    { "step": "postCatalogCommands", "with": { "commands": { "from": "__build__upsertTarget" } } },
    { "build": "workflowMeta", "with": { "workflowId": { "generated": "guid" } } },
    { "build": "createWorkflow", "with": { "type": { "static": "CreateWorkflow" }, "payload": { "object": { "id": { "from": "workflowMeta.workflowId" }, "name": { "static": "Test WF 18" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__createWorkflow" } } },
    { "build": "addCatalog", "with": { "type": { "static": "AddCatalog" }, "payload": { "object": { "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__addCatalog" } } },
    { "build": "appendStep", "with": { "type": { "static": "AppendStep" }, "payload": { "object": { "stepId": { "generated": "guid" }, "stepName": { "static": "getCatalogById" }, "catalogId": { "from": "catalogMeta.catalogId" } }}}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__appendStep" } } },
    { "build": "addAssertion", "with": { "type": { "static": "AddAssertion" }, "payload": { "static": { "assertionDefinition": { "notEmpty": "getCatalogById.name" } } }}},
    { "step": "postWorkflowCommands", "with": { "commands": { "from": "__build__addAssertion" } } },
    { "step": "runWorkflow" }
  ],
  "assertions": [{ "equal": ["runWorkflow.passed", "true"] }]
}
```

---

## 7. Verification

1. Start Postgres with `stepwise_management` DB, run migrations 001–003
2. `dotnet run --project src/StepWise.Management`
3. `dotnet test tests/StepWise.Management.Tests` — all 18 tests pass
