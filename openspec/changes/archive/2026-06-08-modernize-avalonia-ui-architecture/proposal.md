## Why

The Avalonia rewrite now preserves the ChapterTool workflow, but the current UI architecture still relies on code-behind composition, manual control synchronization, imperative secondary-window construction, and ad hoc async command execution. Modernizing these seams is needed before the shell grows further, because UI correctness, testability, platform substitution, and compiled binding safety all depend on observable ViewModels and a clear application composition root.

## What Changes

- Introduce an application composition root that registers services, importers, ViewModels, and windows through dependency injection or an equivalent container.
- Convert main-window scalar state and command availability to observable ViewModel properties so XAML bindings, command state, status, progress, clip selection, and options update without `MainWindow.Refresh()` pushing values into controls.
- Replace hidden command shim buttons with visible commands, context menu items, toolbar/menu entries, key bindings, or directly testable ViewModel commands.
- Add an async command abstraction or equivalent pattern that awaits work, observes exceptions, and exposes execution state for UI commands.
- Move secondary tools from imperatively generated generic windows toward dedicated XAML views and ViewModels, with `IWindowService` responsible for display orchestration rather than building internal control trees.
- Replace per-load importer dependency construction with injected infrastructure services and an importer registry/factory model.
- Enable typed/compiled bindings for the main shell once observable ViewModels and typed views are in place.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `avalonia-ui-shell`: Main-window state, commands, bindings, shortcuts, hidden command shims, async command execution, and compiled binding requirements become stricter.
- `supporting-ui-platform-services`: Application composition and secondary window responsibilities become explicit service/view contracts.
- `disc-playlist-media-importers`: Runtime importer dispatch must use injected dependencies and a registry/factory instead of constructing importer infrastructure inside each load operation.

## Impact

- Affects `src/ChapterTool.Avalonia`, especially `App.axaml.cs`, `Views/MainWindow.axaml`, `Views/MainWindow.axaml.cs`, ViewModels, and auxiliary window services.
- Affects `src/ChapterTool.Infrastructure` importer loading services, external tool location, process runner use, native dependency services, and importer registration.
- May add a dependency on `Microsoft.Extensions.DependencyInjection` or a lightweight local composition container.
- Requires focused Avalonia UI/static tests, ViewModel tests, importer dispatch tests, and full solution validation to protect the existing load/save/edit/export workflows.
