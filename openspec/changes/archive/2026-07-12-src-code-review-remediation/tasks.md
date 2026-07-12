## 0. Planning gate

- [x] 0.1 Confirm documented finite caps for every untrusted MPLS length/count (default proposal: `ReadExactBytes` hard ceiling of 64 MiB plus tighter playlist, subpath, stream-table, mark, and extension limits) in code constants and tests.
- [x] 0.2 Run `openspec validate "src-code-review-remediation" --strict` and keep green before coding.

## 1. Slice A — Import parser safety (XML + binary bounds)

- [x] 1.1 Add a shared secure XML load helper (prohibit DTD / null resolver / no external resolution) under Core import utilities.
- [x] 1.2 Route `XmlChapterImporter` path, stream, and text entry points through the secure helper.
- [x] 1.3 Route `XplChapterImporter` XML load through equivalent secure settings.
- [x] 1.4 Add allocation and iteration bounds to disc binary reads: reject negative/oversized exact reads, validate every MPLS container's declared length before reading/skipping, and cap all untrusted collection counts before `List<T>` allocation.
- [x] 1.5 Add Core tests: valid Matroska XML still imports; DTD/external-entity style payloads fail closed; oversized and signed-overflow binary lengths fail without huge allocation; oversized play-item/subpath counts and `length < consumed` fail closed; valid MPLS fixtures still import.
- [x] 1.6 Focused verification: `dotnet test tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj` (restore/build first if needed).
- [x] 1.7 Slice A merge gate: `dotnet test ChapterTool.Avalonia.slnx` as a single full-solution command.
- [x] 1.8 Update `docs/code-map/core.md` if new secure-XML/binary helper entry points should be listed.

## 2. Slice B — Settings modules: wire or delete

- [x] 2.1 Decide path A (wire modules as real owners) or path B (delete unused module types); record the choice in the PR description. Path B selected: the unused settings modules were deleted; include this decision in the PR summary.
- [x] 2.2 If path A: move external-tools path/status and output-defaults state into modules; have `SettingsToolViewModel` aggregate dirty/save/live-apply through them, with the production settings workflow observably reading and persisting module-owned state. Not applicable: Path B selected.
- [x] 2.3 If path B: delete `SettingsExternalToolsModule`, `SettingsOutputDefaultsModule`, and `SettingsAboutModule` (or any still-unreferenced equivalents); keep `SettingsAppearanceViewModel` as the real appearance boundary.
- [x] 2.4 Preserve settings load/save/discard/live-apply behavior and existing unit/Headless settings tests; if path A is selected, add behavior coverage proving a module-owned edit is persisted and live-applied.
- [x] 2.5 Focused verification: Avalonia unit tests for settings; Headless settings tests if UI bindings change (sequential after unit tests).
- [x] 2.6 Slice B merge gate: `dotnet test ChapterTool.Avalonia.slnx`.
- [x] 2.7 Update `docs/code-map/avalonia.md` for settings ownership after wire-or-delete.

## 3. Slice C — Workspace-consuming shell coordinators

- [x] 3.1 Introduce `LoadSaveWorkflow` (or equivalent) owning load/append/save orchestration via `ChapterWorkspace` revision/session commit APIs; preserve anti-stale semantics.
- [x] 3.2 Introduce `ClipEditingCoordinator` (or equivalent) for select/combine/cell edit/frame ops that write back through the workspace.
- [x] 3.3 Introduce `ProjectionFacade` (or equivalent) for expression/naming/order projection and row materialization shared with preview/save option building.
- [x] 3.4 Introduce `StatusDiagnosticsPresenter` (or equivalent) for status keys, progress messages, and diagnostic localization/logging.
- [x] 3.5 Thin `MainWindowViewModel` to bindable façade + commands that delegate to coordinators; keep tool ports working.
- [x] 3.6 Preserve mandatory concurrent regressions: overlapping loads; older append vs newer load; late progress ignored.
- [x] 3.7 Preserve expression apply/invalid retention, save/preview shared projection, and edit/combine unit coverage.
- [x] 3.8 Focused verification: Avalonia unit tests for ViewModel/session/expression/import-export.
- [x] 3.9 If UI wiring changes: sequential Headless main-window tests. Not applicable: no XAML or binding contract changed.
- [x] 3.10 Slice C merge gate: `dotnet test ChapterTool.Avalonia.slnx`.
- [x] 3.11 Update `docs/code-map/avalonia.md` for coordinator ownership and entry points.
- [x] 3.12 Confirm primary `MainWindowViewModel.cs` stays under ~1000 lines after extraction (or document residual and next cut if still over). Primary file: 971 lines.

## 4. Slice D — Command surface + ExpressionEditor injection

- [x] 4.1 Collapse remaining dual business semantics in `MainWindow.axaml.cs` so wrappers only gather picker paths / selection indexes and forward to ViewModel commands.
- [x] 4.2 Have `AppCompositionRoot` own a single `IExpressionAuthoringService`; pass it through the main-window ViewModel and `ToolWindowCreateContext` into both XAML-created `ExpressionEditor` hosts via an Avalonia property binding.
- [x] 4.3 Remove permanent production reliance on `new ExpressionAuthoringService()` inside the control; retain a fallback only for explicit design-time/test construction if required.
- [x] 4.4 Add a sentinel/fake authoring-service Headless test proving both the main-window and expression-tool editors invoke the composition-provided service, while preserving existing highlighting/completion/diagnostic coverage. Evidence: `AppCompositionRootIdentityHeadlessTests.Production_editor_hosts_use_the_same_composed_authoring_service`.
- [x] 4.5 Focused verification: Avalonia unit tests, then Headless tests that touch main window / expression UI (sequential).
- [x] 4.6 Slice D merge gate: `dotnet test ChapterTool.Avalonia.slnx`.
- [x] 4.7 Update `docs/code-map/avalonia.md` for expression editor service wiring.

## 5. Slice E — Composition lifecycle + required production deps

- [x] 5.1 Share single formatter, expression engine/authoring, export service inputs, and external tool locator instances per `AppCompositionRoot` lifetime on production paths.
- [x] 5.2 Make production `MainWindowViewModel` / settings tool construction pass non-null shell and settings store (adjust tests to inject fakes explicitly rather than relying on product null meaning “optional feature”).
- [x] 5.3 Add/adjust composition smoke or unit coverage that asserts reference identity (or a documented equivalent policy factory) for formatter, expression engine/authoring service, export inputs, and external-tool locator across GUI importer, save, main-window, settings-window, and expression-editor paths. Evidence: `AppCompositionRootIdentityHeadlessTests.Composition_reuses_shared_services_across_runtime_factories_and_tool_windows`.
- [x] 5.4 Reaffirm Lua sandbox library surface with a focused Core test if not already covered in Slice A (no io/os/package; math/string/table + helpers only). Existing `LuaExpressionScriptServiceTests` exercises safe math and rejects `io.open`.
- [x] 5.5 Slice E merge gate: `dotnet test ChapterTool.Avalonia.slnx`.
- [x] 5.6 Final docs pass: `docs/code-map/*` consistency with shipped ownership; cross-link `docs/code-review-src.md` from the change PR summary. Evidence: updated `docs/code-map/core.md`, `docs/code-map/avalonia.md`, and change proposal/review links.

## 6. Close-out

- [x] 6.1 Run `openspec validate "src-code-review-remediation" --strict`. Evidence: passed after implementation and test gates.
- [x] 6.2 Ensure all tasks above are checked with PR references or notes where useful. Evidence: task notes include focused test/composition evidence; remaining residual risks stay documented in `docs/code-review-src.md`.
- [x] 6.3 When implementation is complete and verified, archive the change with delta sync into `openspec/specs/` (do not archive with sync skipped). Evidence: archived as `openspec/changes/archive/2026-07-12-src-code-review-remediation/`; CLI synced all six delta specs before moving the change.
