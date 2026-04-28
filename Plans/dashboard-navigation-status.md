# Dashboard + Navigation Redesign — Status

See `Plans/dashboard-navigation-redesign.md` for the full design spec.

## Done

- Navigation sidebar (Catalogs · Targets · Workflows · Runs), default landing on Catalogs
- Targets page — list, create, edit, archive/unarchive, show-archived toggle, created-date column
- Catalogs page — list, create, edit (name + description), archive/unarchive, step editor within detail
- Workflows page — list, create, edit panel (name, description, steps, assertions), archive/unarchive, run + poll
- Runs page — list (workflow name / pass-fail / started / duration), run detail with expandable steps
- `target_summaries.created_at` — migration 009 and `TargetReactions.cs` update written (uncommitted)
- `GET /runs` list endpoint — present in Program.cs

## Remaining

### 1. Dashboard
Show the 5 most recent test run summaries somewhere in the UI (cards: workflow name, pass/fail badge, started timestamp, duration; each navigates to run detail). The nav sidebar order is Catalogs · Targets · Workflows · Runs — no Dashboard nav item — so this is likely a section at the top of the Catalogs landing page or a small panel in the sidebar footer.

### 2. Streaming execution
During a run, show step-by-step status as each step executes. Plan says "each step shows a simple status as it runs; if a step is a polling step, show retry count and attempt status inline." Currently the UI polls the run endpoint every 500ms and shows nothing until the run finishes — there's no per-step progress.

Requires backend support: the `WorkflowResult` returned today is a single blob. Streaming step status would need either:
- Server-sent events / WebSocket from the execution service, or
- Per-step `RecordStepResult` events on the run aggregate (the planned `StartRun → RecordStepResult → CompleteRun` model)

### 3. Run-time parameters
Before triggering a run, surface any unfilled fields so the user can fill them in. Fields with catalog step defaults are optional; fields without are required.

Requires:
- Backend: a way to identify which workflow step fields are unfilled vs. have defaults
- Frontend: a pre-run modal or page that collects parameter values before dispatching the run

### 4. Workflow step `from` reference warnings
In the step editor, warn when a step's `from` reference names a step that doesn't exist in the workflow or appears later in the order (would be an unresolvable reference at run time).

### 5. Field type selector on step defaults
Each field on a catalog step or workflow step override should have a type selector: `static` (raw JSON) / `from` (reference to a prior step's output) / `generated` (backend-provided generator). Currently no defaults editing exists in the UI at all.

The `from` type should use a drill-down: pick a prior step → navigate the step's response shape one level at a time.

Requires:
- Backend: `GET /api/generators` or equivalent to enumerate available generators
- Backend: response shape defined on catalog steps drives the drill-down (already has `responseShape` column)
- Frontend: field editor component with type selector and conditional input

### 6. Catalog step: response/request shape, fixed value list, options source
- **Request/response shape** — define the shape as raw JSON (hand-written); response shape drives the `from` drill-down in step and assertion editors
- **Fixed value list** — a field can define a fixed list of allowed values; renders as a dropdown at run time
- **Options source** — a field can be backed by a catalog step run on-demand; its response populates dropdown options (label + value field configured)

### 7. Tags
Placeholder — tag support on catalog steps and workflows is planned but not designed yet. Will be used for filtering lists, grouping, and search.

### 8. Unsaved-changes warning
The workflow edit panel should warn on navigate-away when there are unsaved changes to name or description.
