## Why

A full-source maintainability and security review (`docs/code-review-src.md`, 2026-07-12) found Core and Infrastructure in good shape after `decompose-main-window-session`, but left clear residual risk: the Avalonia shell still centers a ~2k-line ViewModel with incomplete extractions (dead Settings “modules”, dual command wrappers, ExpressionEditor self-wiring), and import paths lack explicit security baselines. Those gaps should be specified and fixed now before more features grow around the same hotspots.

## What Changes

- **Hardening (P0):** Secure Matroska/XML import against DTD/external entity resolution and bound untrusted binary playlist allocations.
- **Shell orchestration (P1):** Extract load/save, clip-edit, projection, and status/diagnostic orchestration from `MainWindowViewModel` into workspace-consuming coordinators so partials are not the only decomposition strategy; keep the ViewModel as a thin bindable shell.
- **Settings ownership (P1):** Either wire `Settings*Module` types as real ownership boundaries (state + dirty/save/live-apply segments) or delete unused module types—no permanent half-extraction.
- **Command surface (P1):** Further collapse `MainWindow` dual `UiCommand` wrappers so business can-execute and semantics stay single-sourced on the ViewModel; window code remains view adapters only.
- **Expression composition (P1):** Inject shared expression authoring/engine services into `ExpressionEditor` instead of constructing private defaults outside the composition root.
- **Composition lifecycle (P2):** Clarify singleton vs per-use factories for formatter, tool locator, export, and expression engine; reduce optional production dependencies that hide invariants.
- **Verification:** Focused unit/Core importer tests for parser safety; Avalonia unit + Headless for shell slices; full-solution gates per merge slice.
- **Docs:** Update `docs/code-map/` for new orchestration ownership; keep `docs/code-review-src.md` as the review source of truth (optional cross-link).

No intended chapter format semantics, export-content, or CLI product-scope expansion.
Telemetry remains enabled by default under the current product decision; changing Sentry enablement, DSN handling, or PII defaults is explicitly outside this change.

Review source and residual-risk record: [`docs/code-review-src.md`](../../docs/code-review-src.md); implementation follow-up checklist: [`docs/code-review-remediation-plan.md`](../../docs/code-review-remediation-plan.md).

## Capabilities

### Modified Capabilities

- `chapter-importers-text-xml-matroska-vtt`: require secure XML loading (no DTD/external entity resolution) for Matroska XML import paths.
- `disc-playlist-media-importers`: require bounded reads/allocations when parsing untrusted disc playlist binary lengths (MPLS and shared binary helpers).
- `chapter-core-transform-export`: reaffirm Lua sandbox library surface and execution budget; keep evaluation free of host I/O libraries.
- `avalonia-ui-shell`: require shell workflow coordinators, real settings modularization (or removal of dead modules), single command surface completion, and expression-editor service injection.
- `supporting-ui-platform-services`: composition-root lifecycle for shared services and production non-null platform dependencies where the product always needs them.
- `tests-build-distribution-assets`: coverage and merge gates for security hardening and shell orchestration slices without source-text assertions.

## Impact

- **Code (security):** `XmlChapterImporter`, `XplChapterImporter` (and any shared XML load helper), `BinaryReadExtensions` / `MplsPlaylistFile`.
- **Code (shell):** `MainWindowViewModel*`, new Avalonia orchestration types (e.g. under `Session/` or `ViewModels/Workflows/`), `MainWindow.axaml.cs`, `SettingsToolViewModel` + `ViewModels/Settings/*`, `ExpressionEditor.axaml.cs`, `AppCompositionRoot`.
- **Specs:** deltas for the modified capabilities above.
- **Tests:** Core importer/binary parser tests; Avalonia unit + Headless; no parallel multi-project `dotnet test` (solution or sequential).
- **Docs:** `docs/code-map/avalonia.md`, `docs/code-map/core.md` / `infrastructure.md` if ownership moves; reference `docs/code-review-src.md`, including its recorded telemetry-policy exclusion.
- **Risk:** Structural migration risk for shell slices (behavior-preserving); security changes should be low UX impact.
- **Related prior work:** Continues after `openspec/changes/decompose-main-window-session` (workspace/session already introduced; this change finishes residual review debt rather than redoing session types).
