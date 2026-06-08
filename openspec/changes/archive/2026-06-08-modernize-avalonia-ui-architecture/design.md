## Context

`docs/avalonia-modernization-review.md` identifies that the current Avalonia implementation still has several legacy-style architectural patterns: `MainWindow` and `App` manually compose the object graph, `MainWindow.Refresh()` synchronizes scalar ViewModel state into controls, `MainWindowViewModel` is not observable, compiled bindings are disabled, secondary windows are generated imperatively, importer dependencies are constructed inside the runtime load service, and some async command calls are fire-and-forget.

The existing rewrite specs already require a ViewModel-driven shell, injectable platform services, and UI-independent importers. This change tightens those contracts so the implementation can move from behavior-preserving Avalonia code toward a maintainable MVVM architecture without changing the user-visible ChapterTool workflow.

## Goals / Non-Goals

**Goals:**

- Centralize application startup composition so service lifetimes and platform substitutions are explicit.
- Make main-window scalar state, option state, selection state, progress, status, and command availability observable and bindable.
- Reduce `MainWindow.axaml.cs` to view-specific concerns: file picker adapters, drag/drop argument adaptation, selection extraction when Avalonia requires it, and other platform UI interactions.
- Replace hidden command shim controls with visible commands, context menus, key/input bindings, or directly exposed ViewModel commands.
- Ensure async commands are awaited, exception-aware, and able to expose execution state.
- Move secondary tools toward dedicated typed XAML views and ViewModels while preserving `IWindowService` as the display boundary.
- Inject importer dependencies once through composition and route loads through a registry/factory model.
- Enable typed/compiled bindings after observable ViewModels are available.

**Non-Goals:**

- Redesign the ChapterTool workflow, terminology, supported formats, or existing save/import behavior.
- Replace every window in a single commit if a staged migration is needed.
- Introduce ReactiveUI or CommunityToolkit.Mvvm as a hard requirement; a small local observable base and async command abstraction is acceptable if it fits the codebase better.
- Remove platform service abstractions that already isolate Windows-only behavior.
- Convert Core parsing/export logic beyond changes required for importer registration.

## Decisions

1. Use a single application composition root.
   - `App` will create a service provider during startup, register Core, Infrastructure, Avalonia services, ViewModels, and windows, then resolve `MainWindow`.
   - `MainWindow` will receive its ViewModel and any view-only services through constructor injection.
   - Alternative considered: keep construction in `MainWindow` and only extract helper factories. This leaves lifetime ownership split across `App`, `MainWindow`, and services, so it does not address the review's high-severity finding.

2. Prefer incremental MVVM infrastructure over a broad framework migration.
   - Introduce an observable ViewModel base and `AsyncCommand`/`RelayCommand` equivalents if the repo does not already have one.
   - Commands will raise `CanExecuteChanged` from ViewModel state changes and will observe exceptions through a consistent error path.
   - Alternative considered: adopt a full MVVM framework immediately. That can be valuable later, but a local abstraction keeps the first modernization scoped and reduces churn.

3. Bind main-window state from XAML instead of calling `Refresh()` for normal UI synchronization.
   - Scalar properties such as `StatusText`, `Progress`, `SelectedClipIndex`, `RoundFrames`, `SelectedFrameRateIndex`, `SaveFormat`, option flags, and capability flags will raise property change notifications.
   - Existing observable collections remain responsible for row and clip lists.
   - Code-behind may still translate Avalonia-specific event args into ViewModel calls, but it must not be the normal source of truth for visible state.

4. Replace hidden command shims with real command surfaces.
   - Commands must be reachable through visible buttons, menus, context menus, key/input bindings, or ViewModel properties used directly by tests.
   - Hidden buttons used only to host command bindings will be removed because they degrade accessibility and couple tests to implementation details.
   - Where a command is optional or platform-gated, visibility/enabled state will be bound to capability properties.

5. Stage compiled binding enablement after ViewModel typing.
   - Add `x:DataType` to typed views and enable compiled bindings once the relevant bindings target observable properties and strongly typed commands.
   - Large sections may be split into typed user controls if that reduces binding ambiguity.
   - This avoids enabling compiled bindings before the ViewModel surface is ready and creating noisy transitional failures.

6. Make secondary windows first-class views.
   - Preview, log, color, language, expression, template, zones, and forward-shift tools should move to dedicated `Window` or `UserControl` classes with their own ViewModels.
   - `IWindowService` remains responsible for locating owners, showing dialogs/windows, and returning results, not constructing large C# control trees and wiring click handlers internally.
   - A staged migration can keep old imperative windows temporarily only when each remaining window has a task and regression coverage.

7. Use an importer registry/factory for runtime load routing.
   - Register `IExternalToolLocator`, `IProcessRunner`, `INativeDependencyService`, importer adapters, and importer factories in composition.
   - `RuntimeChapterLoadService` will dispatch by source characteristics to injected importers rather than constructing tool locators, process runners, or native services inside `LoadAsync`.
   - This keeps importer behavior replaceable in tests and avoids recreating dependency graphs per load.

## Risks / Trade-offs

- Binding migration can temporarily break UI state updates -> add focused ViewModel and Avalonia static tests before removing `Refresh()` responsibilities.
- Command execution behavior can change ordering -> await async event handlers and add tests around load/save/edit failures and command busy state.
- DI registration mistakes can surface only at startup -> add composition smoke tests that resolve `MainWindow`, primary ViewModels, window service, and importer registry.
- Compiled bindings may require XAML splits or type annotations -> enable them after property/command names stabilize and include a build gate.
- Migrating all secondary windows at once may be too broad -> stage windows behind the same service contract while keeping each converted window covered by open/close/reopen tests.

## Migration Plan

1. Add composition registration and startup resolution tests without changing user-visible behavior.
2. Add observable ViewModel base and command abstractions, then migrate `MainWindowViewModel` scalar state and command availability.
3. Replace `MainWindow.Refresh()` control synchronization with bindings and narrowly scoped event adapters.
4. Remove hidden command shims after commands are reachable through visible surfaces or key/context bindings.
5. Convert secondary windows to typed XAML/ViewModel pairs in small batches.
6. Register importer dependencies and replace runtime construction with registry/factory dispatch.
7. Add typed data contexts and enable compiled bindings.
8. Run focused Avalonia tests, build the app project, run full solution tests, and capture UI screenshots for layout-sensitive changes.

## Open Questions

- Whether to use `Microsoft.Extensions.DependencyInjection` directly or a small local composition container. The default assumption is `Microsoft.Extensions.DependencyInjection` because it is standard, testable, and sufficient for this scope.
  - Yes
- Whether all secondary windows should be converted in this change or split into follow-up changes after the main shell and importer architecture land. The tasks allow staged conversion while keeping the final requirement explicit.\
  - Yes
