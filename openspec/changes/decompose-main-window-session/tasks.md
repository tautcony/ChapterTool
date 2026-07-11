## 0. Pre-implementation gate (closed decisions)

- [x] 0.1 Close architecture Open Questions as formal decisions in `design.md` (language tool retained; workspace in Avalonia `Session/`; separate A→F slices; settings modularization only; expression theming in Slice E).
- [x] 0.2 Capture async revision/anti-stale contract in `chapter-workspace-session` and concurrent regression requirements in tests delta.
- [x] 0.3 Capture per-slice full-solution verification gates in design and tests delta.
- [x] 0.4 Run `openspec validate "decompose-main-window-session" --strict` after the plan revision and keep it green before Slice A coding starts.

## 1. Slice A — Typed clip session + anti-stale preservation

- [x] 1.1 Add `ClipSession` (`Split` / `Combined`) and pure transition helpers for load, select, combine, restore, append, and edit/frame write-back under Avalonia `Session/`.
- [x] 1.2 Migrate combine/restore/append/select/update paths to the typed session **without dropping** load operation revision, append session-identity checks, late-progress discard, or cancellation non-commit behavior.
- [x] 1.3 Delete sticky booleans once unused (`currentInfoBelongsToSelectedClip`, dual backup fields as independent state).
- [x] 1.4 Add unit tests for combine, restore, append, load replacement clearing combined mode, and write-back ownership without Avalonia controls.
- [x] 1.5 **Mandatory concurrent regressions (merge blockers):** preserve/adapt overlapping-load coverage; preserve/adapt `OlderAppendResultDoesNotOverwriteNewerLoad`; cover late progress from a superseded load being ignored.
- [x] 1.6 Update `docs/code-map/avalonia.md` for clip-session ownership under Avalonia `Session/`.
- [x] 1.7 Focused verification: `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj`.
- [x] 1.8 Slice A merge gate: `dotnet build src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj` (or solution restore/build if needed), then `dotnet test ChapterTool.Avalonia.slnx` as a single full-solution command (do not start other `dotnet test` processes in parallel).

## 2. Slice B — ChapterWorkspace facade + revision ownership

- [x] 2.1 Introduce `ChapterWorkspace` under Avalonia `Session/` owning source metadata, clip session, current chapter set, projection state, export preferences, and revision/session-token commit APIs.
- [x] 2.2 Delegate main ViewModel load/save/edit/expression/refresh paths through workspace APIs; keep ViewModel as bindable shell.
- [x] 2.3 Route load/append progress and results only through current-revision commit rules; cancelled ops must not partially replace session state.
- [x] 2.4 Route preview and save through the same workspace projection/export snapshot and composition-injected export/projection services; remove ad-hoc `new ChapterExportService` from preview.
- [x] 2.5 Add unit tests for projection invalid retention, expression apply atomicity, export-preference snapshot coherence, and cancellation/non-commit.
- [x] 2.6 **Mandatory concurrent regressions still green after the move:** overlapping loads; older append vs newer load; late progress ignored.
- [x] 2.7 Focused verification: Avalonia unit tests for expression/import/export/session coverage.
- [x] 2.8 Slice B merge gate: build affected projects and `dotnet test ChapterTool.Avalonia.slnx`.

## 3. Slice C — Binding authority, commands, grid identity

- [x] 3.1 Bind path, save format, naming mode, expression, order shift, and frame options as the authoritative state; remove production `ReadAdvancedOptions` / `ReadFrameOptions` / format control scrapes.
- [x] 3.2 Reduce window command wrappers to view-parameter adapters only; unify shortcut routing onto the same ViewModel command paths.
- [x] 3.3 Route DataGrid cell commits through stable column identity (`Tag`/id/enum); remove bilingual header-string matching.
- [x] 3.4 Add/update unit and Headless tests proving bound options flow into save/preview and that zh/en/ja localization still commits to the correct edit path.
- [x] 3.5 Focused verification: Avalonia unit tests, then (after unit tests finish) `dotnet test tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj`.
- [x] 3.6 Slice C merge gate: after focused commands complete, build affected projects if needed and run `dotnet test ChapterTool.Avalonia.slnx` as one full-solution command.

## 4. Slice D — Tool ports and window registry

- [x] 4.1 Define narrow ports (`IExpressionSessionPort`, `IPreferenceSink` / language controller, `IExportPreferencePort`, and only other ports justified by real tools).
- [x] 4.2 Implement ports on workspace/main shell; refactor tool ViewModels to depend on ports instead of concrete `MainWindowViewModel` for unrelated capabilities; keep dedicated Language tool on the shared preference path.
- [x] 4.3 Replace `AvaloniaWindowService` title/content/placeholder string switches with a tool-window registration table/factories.
- [x] 4.4 Require composition-owned localizer/export/expression instances on production paths; remove silent `new AppLocalizationManager()` / divergent defaults where a real dependency is expected.
- [x] 4.5 Add unit tests constructing expression/settings/language/preview tools against fakes/ports; cover tool open/lifecycle where window service behavior changes.
- [x] 4.6 Focused verification: Avalonia unit tests, then focused Headless tool-view tests after the unit command finishes.
- [x] 4.7 Slice D merge gate: after focused commands complete, build affected projects if needed and run `dotnet test ChapterTool.Avalonia.slnx` as one full-solution command.

## 5. Slice E — Settings modules and ExpressionEditor presentation

- [ ] 5.1 Split settings into orchestrator + child modules (output defaults, external tools, appearance already separate, about/runtime) with **no multi-page settings UX redesign**.
- [ ] 5.2 Split `ToolWindowViewModels.cs` into per-tool files; retain Language tool.
- [ ] 5.3 Split ExpressionEditor presentation (colorizer, diagnostics, completion) into focused types/files; map themeable chrome/category colors to application theme/semantic resources **in this slice**.
- [ ] 5.4 Keep or add focused tests for settings live-apply/dirty/close and expression editor authoring behavior; verify category coloring is not locked to a hard-coded private palette.
- [ ] 5.5 Focused verification: Avalonia unit tests, then focused Headless for settings/expression surfaces after the unit command finishes.
- [ ] 5.6 Slice E merge gate: after focused commands complete, build affected projects if needed and run `dotnet test ChapterTool.Avalonia.slnx` as one full-solution command.

## 6. Slice F — Shared CLI/GUI factories and final validation

- [ ] 6.1 Expose composition-root factory methods for importer registry and export construction reusable by CLI and GUI.
- [ ] 6.2 Point CLI default construction at shared factories while preserving test injection seams and CLI no-expression product scope.
- [ ] 6.3 Add/update composition and CLI tests proving shared factory use and injectable overrides.
- [ ] 6.4 Finalize `docs/code-map/avalonia.md` (workspace under Avalonia `Session/`; shared factories).
- [ ] 6.5 Run `openspec validate "decompose-main-window-session" --strict`.
- [ ] 6.6 Focused verification: CLI/composition tests + Avalonia unit tests as needed.
- [ ] 6.7 Slice F / change-complete merge gate: when a focused Headless command is needed, finish it first; then run `dotnet test ChapterTool.Avalonia.slnx` as one full-solution command.

## 7. Shared verification rules (all slices)

- [ ] 7.1 Never treat focused-only green as sufficient for an independently mergeable slice; full `dotnet test ChapterTool.Avalonia.slnx` is required at each slice exit.
- [ ] 7.2 Do not launch multiple external `dotnet test` processes in parallel. Finish any focused Headless command before starting the full-solution gate. Headless remains process-isolated via its dedicated project/testhost; a single solution-level `dotnet test` that includes Headless is allowed.
- [ ] 7.3 Do not weaken or delete concurrent load/append anti-stale tests to make a refactor pass.
