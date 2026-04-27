# Dashboard + Navigation Redesign — Design

## Problem
The app has no landing page and no dedicated Targets page. Targets are buried inside Catalogs. The goal is a proper dashboard and first-class navigation for all resource types.

---

## Navigation
Sidebar order: **Catalogs · Targets · Workflows · Runs**  
Default landing page: Catalogs

---

## Dashboard
- Deferred — not in nav for now

---

## Targets page
- List columns: name, base URL, created date
- Archived targets hidden by default; "Show archived" toggle to reveal them
- Actions: Create (name + base URL), Edit inline (name + URL), Archive / Unarchive
- No delete

---

## Catalogs page
- List columns: name, description
- Archived catalogs hidden by default; "Show archived" toggle to reveal them
- Catalog detail opens in a side panel on desktop, new page on mobile (same pattern as workflows)
- Catalog actions: Create, Edit (name + description), Archive / Unarchive

### Catalog step editor
- List columns: step name, tags; supports archive / unarchive, tag filtering
- Step actions: Create, Edit, Archive / Unarchive
- Step fields: name, target (linked to a Target from the Targets page), method (GET / POST / PUT / PATCH / DELETE / HEAD / OPTIONS), path, defaults (field definitions), request shape, response shape, tags (placeholder)
- Polling flag: optional; when enabled, configure retry count and retry duration

### Catalog step defaults
- Edited as raw JSON for now
- Valid field types: `static` (fixed JSON value), `generated` (backend-provided generator), `unfilled` (explicitly blank — required at workflow or run time), `from` (reference to another step's output, resolved at workflow or run time when prior step context is available — optionally includes a fallback value used if the reference fails to resolve)
- Workflow step overrides follow the same rules
- Fields left as `unfilled` with no override become required run-time parameters

### Request and response shape
- Both the request shape and response shape are defined per catalog step
- Both may contain polymorphic lists, represented using tagged unions (option 3) — consistent with the existing JSON format (`{ "static": ... }`, `{ "equal": [...] }`, etc.)
- Shape is defined as raw JSON (hand-written or LLM-assisted) — the same tagged union format used throughout
- Future enhancements: infer from a live run (B), visual field builder (C), import from JSON Schema (shape only) or OpenAPI (shape + endpoint metadata — method, path, params) (D)
- Response shape drives the `from` drill-down in the workflow step and assertion editors

### Fixed value list (per catalog step field)
- A field can define a fixed list of allowed values directly on the catalog step
- At run time, the field renders as a dropdown limited to those values

### Options source (per catalog step field)
- Configured using the same pattern as a workflow step's `from` field:
  - Pick a source catalog step
  - Drill down through its output shape to the list
  - Pick the label field (what the user sees in the dropdown)
  - Pick the value field (what gets submitted as the field value)
- The source step's own unfilled fields must have defaults

---

## Workflows page
- List columns: name, description, tags (placeholder)
- Archived workflows hidden by default
- Actions: Create, Archive / Unarchive, Rename, Edit description, Run (available in both the list row and the edit panel)
- Edit opens a side panel on desktop, a new page on mobile; always in edit mode with an explicit Save button; warns on navigate away with unsaved changes
- Edit covers: name, description, steps, and assertions

### Step editor (within workflow edit)
- Steps and assertions use the same UX pattern: add, edit, remove, drag-and-drop reorder
- Steps show a warning if any `from` fields reference a step that no longer exists or appears later in the order
- Add step options:
  - **Catalog step**: catalog selector (default = all, filterable by name) → pick a step
  - **Sub-workflow**: pick from the list of available workflows; its unfilled fields are surfaced in the step editor using the same field type selector (static / from / generated) and run-time parameter behavior as regular steps
- Available fields per step are driven by the catalog step definition — editor is constrained to those fields only
- Each catalog step definition specifies its output shape, which drives the `from` drill-down (no prior run needed)
- Each field has a type selector: `static` / `from` / `generated`
  - `static`: raw JSON input (any valid JSON — string, number, object, array, etc.)
  - `from`: drill-down menu — pick a prior step, then navigate one level of its defined output shape at a time
  - `generated`: dropdown of generators provided by the backend
- Fields can be left unfilled; unfilled fields become run-time parameters
  - Optional at run time if the catalog step has a default value for that field
  - Required at run time if no default exists

### Assertions editor (within workflow edit)
- Same UX pattern as steps: add, edit, remove, drag-and-drop reorder
- Assertion types: `equal`, `notEqual`, `count`, `empty`, `notEmpty`
- Values reference step outputs using the same `from` drill-down as steps

### Run page
- Opened from the Run button on a workflow
- Before running: user fills in any unfilled fields (run-time parameters)
  - Fields with catalog step defaults are optional; fields without are required
  - Field values can come from an on-demand step execution — a catalog step is linked as the options source for a field; it is run on-demand and its response populates the available options. If that step itself has unfilled fields, those must have defaults to avoid circular dependencies
  - Target overrides: deferred for later
- During running: execution is streamed; each step shows a simple status as it runs
  - If a step is a polling step: show retry count and attempt status inline for that step
- The run page and run detail are the same page — it shows progress while running and detail when complete

---

## Runs page
- List columns: workflow name, pass/fail badge, started at, duration; failed runs also show the error message inline
- Flat list ordered by most recent, no filtering for now
- Run detail opens as a new page (same on desktop and mobile):
  - Step-by-step results, each step collapsed by default, expandable to show request, response, and pass/fail
  - Assertion results — which passed or failed and why
  - Parameters used — field values supplied at run time
  - Full error details if the run failed at the infrastructure level

## Tagging
- Placeholder — tag support on catalog steps and workflows is planned but not designed yet
- Tags will be used for: filtering lists, grouping, and search

---

## Data gaps to address
- `target_summaries` has no `created_at` column — needs a migration and reaction handler update
- No `GET /runs` list endpoint exists yet — needs to be added
