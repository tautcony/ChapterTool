## Why

The Avalonia shell currently concentrates chapter session lifecycle, clip combine/split state, export projection, settings live-apply, tool-window ownership, and most main-window orchestration inside `MainWindowViewModel` (~2k lines across partials) plus a second orchestration layer in `MainWindow.axaml.cs`. That structure still works, but every new workflow adds flags, dual-state sync, and owner coupling. A deliberate decomposition is needed now so later features do not keep growing spaghetti around special-case session flags and imperative control reads.

## What Changes

- Introduce an explicit **chapter workspace / session model** that owns source load state, clip session mode (split vs combined), edit buffer, projection options, and export preferences.
- Preserve and formalize **async anti-stale semantics**: load/append operation revisions, session-identity checks, cancellation, and rejection of late results/progress so overlapping operations cannot corrupt a newer session.
- Collapse multi-flag clip state (`splitClipGroup`, `combinedClipOption`, `currentInfoBelongsToSelectedClip`, `IsClipCombineChecked`, parallel `currentGroup`/`currentInfo`) into a single typed session state with pure transitions.
- Make **bindings the single source of truth** for main-window workflow options (path, save format, naming mode, expression, order shift, frame options). Remove `ReadAdvancedOptions` / `ReadFrameOptions` style push-before-command patterns.
- Keep a single command surface on the ViewModel; reduce window code-behind to view adapters only (pickers, drag/drop, selection indexes, layout, keyboard gesture adaptation).
- Route chapter-grid cell commits through **stable column identity**, not localized header strings.
- Give secondary tools **narrow session ports** instead of holding the full `MainWindowViewModel` owner type for unrelated capabilities.
- Register secondary tool windows through an explicit descriptor/registry instead of string-id switch soup in the window service.
- Share export construction for preview and save through the same injected export service path; stop constructing ad-hoc `ChapterExportService` instances in preview helpers.
- Split oversized shell modules where ownership is already clear: settings tool sections, expression-editor presentation pieces, and tool ViewModels currently packed into grab-bag files.
- Align CLI composition with the application composition root factories for load/export construction without expanding CLI product scope.
- Preserve user-visible workflow behavior, localization, and existing Core/Infrastructure import-export contracts.

## Capabilities

### New Capabilities

- `chapter-workspace-session`: explicit workspace/session ownership for loaded source groups, clip mode transitions, edit buffer, projection state, export preference snapshots, and async revision/anti-stale commit rules used by the shell and secondary tools.

### Modified Capabilities

- `avalonia-ui-shell`: strengthen binding authority, single command surface, stable grid-edit routing, tool dependency boundaries, settings modularization, expression-editor presentation ownership, and shell-side use of the workspace session.
- `supporting-ui-platform-services`: require tool-window registration, composition-root service ownership (no silent default re-instantiation of localization/export/expression services on production paths), and shared load/export factories for CLI and GUI.
- `command-line-conversion-workflows`: require CLI to obtain importer registry / export construction through shared composition factories rather than a parallel private wiring path.
- `tests-build-distribution-assets`: require focused unit and headless coverage for workspace transitions, concurrent load/append anti-stale regressions, binding-authority regressions, tool-port isolation, and per-slice merge verification gates without source-text assertions.

## Impact

- Primary code: `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel*.cs`, `Views/MainWindow.axaml(.cs)`, `Services/AvaloniaWindowService.cs`, `ViewModels/SettingsToolViewModel.cs`, `ViewModels/ToolWindowViewModels.cs`, `Views/Controls/ExpressionEditor*`, `Composition/AppCompositionRoot.cs`, `Cli/ChapterToolCliApplication.cs`.
- New types under Avalonia `Session/` (formal decision): workspace/session, clip session state, projection state, revision tracking, tool window descriptors, tool ports. Pure types may be promoted to Core only in a later change after they prove UI-free.
- Specs: new `chapter-workspace-session`; deltas for `avalonia-ui-shell`, `supporting-ui-platform-services`, `command-line-conversion-workflows`, `tests-build-distribution-assets`.
- Tests: `ChapterTool.Avalonia.Tests`, `ChapterTool.Avalonia.Headless.Tests`, and any composition/CLI tests that construct load/export services.
- Docs: `docs/code-map/avalonia.md` (and core map only if session types land outside Avalonia).
- **No intended format/parser/export-content behavior change.** User-facing workflows remain: load, edit, combine/split, expression project, save, settings live-apply, tools.
- Risk is structural migration risk, not product redesign risk; migration must stay behavior-preserving and shippable in ordered PR slices.
