# Avalonia Modernization Review

Date: 2026-06-07

Scope: current `src/` implementation, especially `src/ChapterTool.Avalonia`.
This review intentionally ignores legacy WinForms compatibility as a justification and evaluates the code against modern .NET/Avalonia practices.

## Findings

### High: MainWindow manually composes the application object graph

`src/ChapterTool.Avalonia/Views/MainWindow.axaml.cs` constructs the formatter, settings stores, load/save services, editing services, window service, shell service, frame-rate service, and `MainWindowViewModel` directly. `App.axaml.cs` also directly constructs `MainWindow`.

Impact:
- Service lifetime and ownership are unclear.
- Platform-specific implementations are hard to replace.
- Window and ViewModel tests require custom fakes instead of normal service substitution.
- Startup composition is split across `App`, `MainWindow`, and service constructors.

Recommendation:
- Introduce a composition root in application startup.
- Register services and ViewModels through `Microsoft.Extensions.DependencyInjection` or an equivalent lightweight container.
- Let `App` resolve `MainWindow` and let `MainWindow` receive its ViewModel and view services through constructor injection.

### High: MainWindow still synchronizes control state manually

`MainWindow.Refresh()` pushes state into controls directly: status text, progress, grid item source, clip item source, checkbox state, combo indexes, visibility, command state notifications, and option fields.

Impact:
- UI correctness depends on every command remembering to call `Refresh()`.
- State is duplicated between controls and ViewModel.
- New UI paths can easily become stale.
- This limits compiled binding and makes UI behavior harder to test at the ViewModel level.

Recommendation:
- Move UI-facing state to bindable ViewModel properties.
- Bind `Text`, `Value`, `ItemsSource`, `SelectedIndex`, `IsVisible`, `IsChecked`, and `IsEnabled` in XAML.
- Keep code-behind only for platform-specific UI interactions such as file pickers, drag/drop, and view-specific event argument adaptation.

### High: MainWindowViewModel is not observable

`MainWindowViewModel` exposes many mutable properties such as `StatusText`, `Progress`, `SelectedClipIndex`, `RoundFrames`, `SelectedFrameRateIndex`, `SaveFormat`, and option flags, but it does not implement `INotifyPropertyChanged`.

Impact:
- XAML cannot reliably bind to ViewModel state without manual refresh.
- Command `CanExecute` changes must be raised externally.
- Observable collections only cover rows and clip options; scalar state remains imperative.

Recommendation:
- Implement `INotifyPropertyChanged`, or use a ViewModel toolkit such as CommunityToolkit.Mvvm or ReactiveUI.
- Replace private setters plus manual refresh with observable properties.
- Raise command state changes from the ViewModel when the underlying state changes.

### Medium: Compiled bindings are disabled

`MainWindow.axaml` sets `x:CompileBindings="False"` and does not define a typed data context.

Impact:
- Broken property and command bindings are discovered only at runtime.
- Refactoring ViewModel properties or command names is riskier.
- Tests need string assertions to catch binding regressions.

Recommendation:
- Add `x:DataType` where supported and enable compiled bindings.
- Split large XAML into smaller typed views if necessary.
- Prefer strongly typed ViewModel commands and properties over element-name bindings into the window.

### Medium: Secondary windows are generated imperatively

`AvaloniaWindowService` creates generic `Window` instances and builds preview, log, color, language, expression, template, zones, and forward-shift content with C# controls and click handlers.

Impact:
- The secondary UI is difficult to style consistently.
- Behavior is coupled to generated controls instead of ViewModels.
- The implementation repeats the WinForms-style pattern of building UI in code.

Recommendation:
- Create dedicated XAML views and ViewModels for secondary dialogs/tools.
- Use `Window`/`UserControl` classes with binding and commands.
- Keep `IWindowService` responsible for showing views, not constructing their internal UI trees.

### Medium: RuntimeChapterLoadService manually constructs importer dependencies

`RuntimeChapterLoadService` creates `ExternalToolLocator`, `ProcessRunner`, importer instances, and native dependency services inside `LoadAsync`.

Impact:
- The service is both an importer dispatcher and a dependency factory.
- Dependencies are recreated per load operation.
- Importer behavior is harder to override in tests or platform-specific builds.

Recommendation:
- Register importers and infrastructure services through DI.
- Use an importer registry keyed by extension/source type.
- Inject `IExternalToolLocator`, `IProcessRunner`, `INativeDependencyService`, and importer factories.

### Medium: Async command execution is sometimes fire-and-forget

Several places call `ExecuteAsync()` without awaiting the returned `ValueTask`, especially for grid edits, insert/delete shortcuts, clip selection, and combine.

Impact:
- Exceptions can be lost.
- UI refresh can run before command work completes.
- Ordering bugs are possible when commands become truly asynchronous.

Recommendation:
- Use an async command abstraction that observes exceptions and exposes execution state.
- Await asynchronous command execution in event handlers.
- Avoid returning completed tasks from wrappers that start asynchronous work without awaiting it.

### Medium: Hidden controls are used as command shims

`MainWindow.axaml` still contains hidden buttons such as `SaveToButton`, `AppendMplsButton`, `CombineButton`, `OpenMediaButton`, `ColorButton`, `ExpressionButton`, `TemplateButton`, `ZonesButton`, and `ForwardShiftButton`.

Impact:
- The visual tree contains controls that are not part of the visible UI.
- Tests and automation IDs are coupled to non-interactive implementation details.
- Command discoverability and accessibility are worse than proper menus/key bindings.

Recommendation:
- Remove hidden command shim controls.
- Expose actions through visible menu items, toolbar buttons, context menus, key bindings, or command palette patterns.
- Test commands through ViewModel/window command properties instead of hidden controls.

### Low: Keyboard shortcuts are manually parsed in code-behind

`MainWindow.axaml.cs` parses `KeyEventArgs` into string gestures and routes them through `ShortcutRouter`.

Impact:
- Shortcut definitions are hidden in code rather than declared next to commands.
- It duplicates framework input binding behavior.
- Adding or changing shortcuts requires manual parsing logic.

Recommendation:
- Use Avalonia key/input bindings where possible.
- Bind keyboard gestures directly to commands.
- Keep only shortcuts that genuinely require dynamic parameters in code.

### Low: MainWindow has too many responsibilities

`MainWindow.axaml.cs` currently handles startup loading, file picking, saving option extraction, command wrappers, drag/drop, keyboard routing, grid edit translation, selection extraction, command state refresh, layout breakpoints, and settings directory discovery.

Impact:
- The file is difficult to reason about.
- UI behavior is hard to test without launching the window.
- Responsibilities that belong to ViewModels, behaviors, services, or bindings are concentrated in code-behind.

Recommendation:
- Move state and user workflows into ViewModels.
- Move platform interactions into injected services.
- Move layout behavior into XAML/adaptive layout patterns where practical.
- Use small behaviors/adapters for grid edit and drag/drop event argument translation.

## Suggested Modernization Order

1. Add observable ViewModel support and bind scalar state from XAML.
2. Move service construction into an application composition root.
3. Remove hidden command shim controls and replace them with visible commands or key bindings.
4. Split secondary windows into XAML views and ViewModels.
5. Replace manual importer construction with an injected importer registry.
6. Enable compiled bindings after ViewModels are typed and observable.
7. Replace fire-and-forget command calls with an async command abstraction.

## Verification Targets

Future modernization work should preserve:
- Loading new source files repeatedly from the primary load button.
- MPLS clip selector visibility and clip switching.
- Clip selector right-click combine behavior.
- Frame count display and frame-rate/rounding refresh behavior.
- Save/export behavior after edits and option changes.
- Keyboard shortcuts and context menu actions.

