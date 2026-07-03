## 1. Composition Root

- [x] 1.1 Add or select the application composition mechanism and register Core, Infrastructure, Avalonia services, ViewModels, and windows in startup.
- [x] 1.2 Change `App` to resolve `MainWindow` from composition and remove direct main-window construction.
- [x] 1.3 Change `MainWindow` to receive its ViewModel and view-only services through constructor injection.
- [x] 1.4 Add composition smoke tests that resolve `MainWindow`, primary ViewModels, window service, load/save services, and importer registry.

## 2. Observable ViewModels And Bindings

- [x] 2.1 Add observable ViewModel infrastructure or adopt an existing toolkit pattern for property change notification.
- [x] 2.2 Convert `MainWindowViewModel` scalar UI state, option state, selection state, visibility flags, and capability flags to observable properties.
- [x] 2.3 Update command availability so relevant state changes raise `CanExecuteChanged` without requiring window refresh calls.
- [x] 2.4 Bind main-window text, progress, item sources, selections, checkboxes, visibility, enabled state, and option fields in XAML.
- [x] 2.5 Remove routine status/progress/grid/clip/option synchronization from `MainWindow.Refresh()` and delete the method if it no longer has a valid responsibility.
- [x] 2.6 Add or update ViewModel and Avalonia static tests for property notifications, bound state, command availability, and absence of routine manual refresh synchronization.

## 3. Commands, Shortcuts, And Compiled Bindings

- [x] 3.1 Add an async command abstraction or equivalent pattern that awaits work, observes exceptions, exposes busy state where useful, and raises availability changes.
- [x] 3.2 Migrate load, save, edit, clip selection, combine, shortcut, and auxiliary command paths away from fire-and-forget `ExecuteAsync()` calls.
- [x] 3.3 Replace hidden command shim controls with visible command surfaces, context menu items, key/input bindings, or directly exposed ViewModel commands.
- [x] 3.4 Add tests proving hidden command shim controls are absent and affected actions remain reachable.
- [x] 3.5 Add typed data contexts and enable compiled bindings for the main window after observable properties and commands are stable.
- [x] 3.6 Add build/static test coverage that fails when main-window binding targets are renamed or removed.

## 4. Secondary Windows

- [x] 4.1 Create dedicated XAML views and ViewModels for preview and log windows while preserving open, close, reopen, display, and copy behavior.
- [x] 4.2 Create dedicated XAML views and ViewModels for color and language tools while preserving settings persistence and localization behavior.
- [x] 4.3 Create dedicated XAML views and ViewModels for expression, template, zones, and forward-shift tools while preserving result-returning behavior.
- [x] 4.4 Refactor `IWindowService` implementations so they show typed views and coordinate owner/result behavior instead of constructing each tool's internal control tree inline.
- [x] 4.5 Add or update auxiliary window tests for ViewModel behavior and open/close/reopen lifecycle.

## 5. Importer Registry

- [x] 5.1 Define an importer registry or factory contract for runtime source dispatch by extension and source characteristics.
- [x] 5.2 Register external tool locator, process runner, native dependency service, importer adapters, and importer factories through composition.
- [x] 5.3 Refactor `RuntimeChapterLoadService` to dispatch through injected registrations and remove per-load construction of importer infrastructure.
- [x] 5.4 Add importer dispatch tests using replacement registrations, fake external tool locators, fake process runners, and fake native dependency services.
- [x] 5.5 Verify repeated load operations use registered lifetimes rather than recreating infrastructure manually inside `LoadAsync`.

## 6. Verification

- [x] 6.1 Run `openspec validate "modernize-avalonia-ui-architecture" --strict`.
- [x] 6.2 Run `dotnet test tests\ChapterTool.Avalonia.Tests\ChapterTool.Avalonia.Tests.csproj --no-restore`.
- [x] 6.3 Run `dotnet build src\ChapterTool.Avalonia\ChapterTool.Avalonia.csproj --no-restore`.
- [x] 6.4 Run `dotnet test ChapterTool.Avalonia.slnx --no-restore`.
- [x] 6.5 For UI layout-affecting changes, capture default, wide, and narrow screenshots under `artifacts/`.
