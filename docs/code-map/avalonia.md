# Avalonia Code Map

`src/ChapterTool.Avalonia` owns the desktop shell, GUI launch compatibility, view/viewmodel coordination, runtime orchestration, localization, and theme application.

Use ASD-STE100 principles in this document. Keep each sentence short and direct. Keep code identifiers exact.

## Ownership

### Application shell

Startup and main shell entry points:

- `src/ChapterTool.Avalonia/Program.cs`
- `src/ChapterTool.Avalonia/Diagnostics/SentryStartupConfiguration.cs`
- `src/ChapterTool.Avalonia/App.axaml`
- `src/ChapterTool.Avalonia/App.axaml.cs`
- `src/ChapterTool.Avalonia/Views/MainWindow.axaml`
- `src/ChapterTool.Avalonia/Views/MainWindow.axaml.cs`
- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs` (partial shell)
- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.Settings.cs`
- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.ImportExport.cs`
- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.Expression.cs`
- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.Editing.cs`
- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.StatusLog.cs`

Main-window workflow owners under `src/ChapterTool.Avalonia/Workflows/` use the same `ChapterWorkspace`:

- `LoadSaveWorkflow.cs` — revision/session-aware load, append, and save service orchestration
- `ClipEditingCoordinator.cs` — clip selection/combine transitions plus cell and frame edits written through the workspace
- `ProjectionFacade.cs` — workspace-backed projection, preview/save options, and chapter-row materialization
- `StatusDiagnosticsPresenter.cs` — localized status/progress rendering and structured diagnostic logging

Role split:

- `MainWindow.axaml`: shell layout and bindings
- `MainWindow.axaml.cs`: drag/drop, picker triggers, keyboard/UI-only behavior
- `MainWindowViewModel` partials:
  - `.cs`: fields, ctor, bindable state, command wiring, window/shell helpers
  - `.Settings.cs`: load/apply preferences and language persistence
  - `.ImportExport.cs`: load/save/append workflows and export options
  - `.Expression.cs`: Lua expression apply/validate and output projection
  - `.Editing.cs`: clip selection, row edits, combine/split, frame-rate transforms
  - `.StatusLog.cs`: status text, diagnostics localization, logging, localized option refresh

### Session (clip / workspace)

Typed chapter session state lives under Avalonia `Session/` (not Core for this change):

- `src/ChapterTool.Avalonia/Session/ClipSession.cs` — `SplitClipSession` / `CombinedClipSession` and pure transitions (`FromLoad`, `Select`, `ToggleCombine`, `Restore`, `Append`, `WriteBack`)
- `src/ChapterTool.Avalonia/Session/ProjectionState.cs` — naming mode, order shift, expression fields, last-successful projection cache
- `src/ChapterTool.Avalonia/Session/ExportPreferences.cs` — save format, XML language, text encoding, BOM, save directory
- `src/ChapterTool.Avalonia/Session/ChapterWorkspace.cs` — workspace facade: source path, clip session, edit buffer, owned `ProjectionState` + `ExportPreferences`, load/append revision + session-token commit APIs (`CreateExportOptions` / `CreateExportOptionsForProjectedInfo` read workspace-owned snapshots)
- `src/ChapterTool.Avalonia/Session/Ports/ShellPorts.cs` — narrow tool ports (`IExpressionSessionPort`, `IPreferenceSink`, …)
- `src/ChapterTool.Avalonia/Session/Ports/MainWindowPortAdapters.cs` — concrete main-window adapters that own expression, preference, export, naming, and chapter-edit port behavior

`MainWindowViewModel` is the bindable shell and holds one `ChapterWorkspace`. Bindable projection/export properties facade workspace state (workspace is the owner) and command handlers delegate workflow orchestration to the `Workflows/` collaborators. Load/append progress and results commit only through workspace revision rules; preview/save use composition-injected `ChapterExportService` with options from the workspace snapshot.

### Composition root

Runtime wiring is centralized in:

- `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs`

The GUI composition root uses `ChapterToolRuntimeComposition` for runtime importer construction. It passes its formatter, tool locator, process runner, and media readers to preserve shared GUI service instances.

For GUI production paths, one `AppCompositionRoot` shares the formatter, expression engine, authoring service, export service, process runner, and external-tool locator across the main window and tool windows. `ExpressionEditor` receives `IExpressionAuthoringService` through `MainWindowViewModel` or `ToolWindowCreateContext`. Its private fallback is limited to direct design-time or test construction.

The lifetime contract is covered by `tests/ChapterTool.Avalonia.Headless.Tests/Composition/AppCompositionRootIdentityHeadlessTests.cs`: formatter, expression authoring, export, and external-tool locator identities are shared within one GUI root.

This is the first file to inspect when dependency wiring or service registration changes.

### Views

- `src/ChapterTool.Avalonia/Views/MainWindow.axaml`
- `src/ChapterTool.Avalonia/Views/Controls/ExpressionEditor.axaml`
- `src/ChapterTool.Avalonia/Views/Tools/SettingsToolView.axaml`
- `src/ChapterTool.Avalonia/Views/Tools/LanguageToolView.axaml`
- `src/ChapterTool.Avalonia/Views/Tools/ExpressionToolView.axaml`
- `src/ChapterTool.Avalonia/Views/Tools/TemplateNamesToolView.axaml`
- `src/ChapterTool.Avalonia/Views/Tools/ForwardShiftToolView.axaml`
- `src/ChapterTool.Avalonia/Views/Tools/TextToolView.axaml`

### ViewModels

- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel*.cs`
- `src/ChapterTool.Avalonia/ViewModels/SettingsToolViewModel.cs`
- `src/ChapterTool.Avalonia/ViewModels/SettingsAppearanceViewModel.cs`
- `src/ChapterTool.Avalonia/ViewModels/ChapterExpressionValidation.cs`
- `src/ChapterTool.Avalonia/ViewModels/ChapterSaveDirectory.cs`
- `src/ChapterTool.Avalonia/ViewModels/ToolWindowViewModels.cs`
- `src/ChapterTool.Avalonia/ViewModels/ChapterRowViewModel.cs`
- `src/ChapterTool.Avalonia/ViewModels/UiCommand.cs`
- `src/ChapterTool.Avalonia/ViewModels/ShortcutRouter.cs`

### Runtime and UI services

- `src/ChapterTool.Avalonia/Services/RuntimeChapterLoadService.cs`
- `src/ChapterTool.Avalonia/Services/RuntimeChapterSaveService.cs`
- `src/ChapterTool.Infrastructure/Importing/Runtime/RuntimeChapterImporterRegistry.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaWindowService.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaFilePickerService.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaSettingsPickerService.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaThemeApplicationService.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaFontApplicationService.cs`
- `src/ChapterTool.Avalonia/Services/IFontFamilyCatalog.cs`
- `src/ChapterTool.Avalonia/Services/FontFamilyCatalogEntry.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaFontFamilyCatalog.cs`
- `src/ChapterTool.Avalonia/Services/FontSettingsResolver.cs`

### CLI

- `src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.cs`
- `src/ChapterTool.CommandLine/Cli/ChapterToolCliCommands.cs`
- `src/ChapterTool.CommandLine/Cli/ChapterToolCliSupport.cs`
- `src/ChapterTool.CommandLine/Cli/CliConsole.cs`

### Localization

- `src/ChapterTool.Avalonia/Localization/AppLocalizationManager.cs`
- `src/ChapterTool.Avalonia/Localization/IAppLocalizer.cs`
- `src/ChapterTool.Avalonia/Localization/AppLocalizationResources.cs`
- `src/ChapterTool.Avalonia/Localization/AppLanguage.cs`
- `src/ChapterTool.Avalonia/Localization/Resources/Strings.zh-CN.resx`
- `src/ChapterTool.Avalonia/Localization/Resources/Strings.en-US.resx`
- `src/ChapterTool.Avalonia/Localization/Resources/Strings.ja-JP.resx`

## Feature Lookup

### Main window layout, binding, workflow zones

Start with:

- `src/ChapterTool.Avalonia/Views/MainWindow.axaml`
- `src/ChapterTool.Avalonia/Views/MainWindow.axaml.cs`
- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs`

### Main command workflow

Start with:

- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs`

If keyboard routing matters:

- `src/ChapterTool.Avalonia/ViewModels/ShortcutRouter.cs`

If command execution semantics change:

- `src/ChapterTool.Avalonia/ViewModels/UiCommand.cs`

### Tool windows

Start with:

- `src/ChapterTool.Avalonia/Services/ToolWindowRegistry.cs` — tool id → title resource + content factory table
- `src/ChapterTool.Avalonia/Services/AvaloniaWindowService.cs` — host lifecycle; iterates registry
- `src/ChapterTool.Avalonia/Session/Ports/ShellPorts.cs` — narrow tool ports (`IExpressionSessionPort`, `IPreferenceSink`, `IExportPreferencePort`, …)

Then inspect the matching pair in:

- `src/ChapterTool.Avalonia/Views/Tools/`
- `src/ChapterTool.Avalonia/ViewModels/`

### Clip combine / multi-entry session

Start with:

- `src/ChapterTool.Avalonia/Session/ClipSession.cs`
- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.Editing.cs`
- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.ImportExport.cs`

Pure transition coverage: `tests/ChapterTool.Avalonia.Tests/Session/ClipSessionTests.cs`. Concurrent load/append anti-stale coverage remains in `MainWindowViewModelTests`.

### Load/save/import behavior exposed in UI

Start with:

- `src/ChapterTool.Avalonia/Services/RuntimeChapterLoadService.cs`
- `src/ChapterTool.Avalonia/Services/RuntimeChapterSaveService.cs`
- `src/ChapterTool.Infrastructure/Importing/Runtime/RuntimeChapterImporterRegistry.cs`

`RuntimeChapterSaveService` applies UI save-file concerns such as output directory selection, generated file path diagnostics, and the selected `ChapterExportOptions.TextEncoding` / `EmitBom` behavior around Core export content.

If the wiring looks wrong, inspect:

- `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs`

### Expression editor UI

Presentation types live under `Views/Controls/Expression/`:

- `ExpressionThemeBrushes.cs` — theme resource keys for category/chrome colors
- `ExpressionColorizer.cs`
- `ExpressionDiagnosticPresentation.cs`
- `ExpressionCompletionPresentation.cs`
- `ExpressionEditor.axaml(.cs)` — control shell

Start with:

- `src/ChapterTool.Avalonia/Views/Tools/ExpressionToolView.axaml`
- `src/ChapterTool.Avalonia/Views/Controls/ExpressionEditor.axaml`
- `src/ChapterTool.Avalonia/Views/MainWindow.axaml.cs`
- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs`
- `src/ChapterTool.Avalonia/ViewModels/ToolWindowViewModels.cs`
- `src/ChapterTool.Core/Transform/ExpressionAuthoringService.cs`

Behavior coverage is concentrated in `ExpressionAuthoringServiceTests`, `MainWindowViewModelTests`, `MainWindowInteractionHeadlessTests`, and `ToolViewsHeadlessTests` for Lua tokens/completions, delayed edit diagnostics, live valid projections, editing-key routing, and single-editor multiline expansion.
`AppCompositionRootIdentityHeadlessTests` additionally exercises both production XAML editor hosts with a sentinel authoring service, including initial binding and subsequent text edits.

### Settings / theme / language UI

Start with:

- `src/ChapterTool.Avalonia/ViewModels/SettingsToolViewModel.cs`
- `src/ChapterTool.Avalonia/ViewModels/SettingsAppearanceViewModel.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaWindowService.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaThemeApplicationService.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaFontApplicationService.cs`
- `src/ChapterTool.Avalonia/Services/IFontFamilyCatalog.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaFontFamilyCatalog.cs`
- `src/ChapterTool.Avalonia/Localization/AppLocalizationManager.cs`
- `src/ChapterTool.Avalonia/Views/Tools/SettingsToolView.axaml`
- `src/ChapterTool.Avalonia/App.axaml`

Output defaults, external-tool paths and statuses, and runtime/footer display state live in `SettingsToolViewModel`; it flows live preferences through `PreferenceSinkAdapter` (session save format is applied only when startup settings are loaded). There are no unused `Settings*Module` placeholder types. A directory chosen from the main-window save workflow updates only the current session and does not overwrite the configured default. `AppCompositionRoot` constructs one `ChapterToolSettingsStore` shared directly by runtime consumers; startup loads one aggregate snapshot for theme and font, while the settings tool loads once, dirty-checks a single `ChapterToolSettings` snapshot, and commits all child changes once. It also passes the resolved settings directory through `AvaloniaWindowService` so the settings footer can open the owning folder through `IShellService`.

Main-window selectors with runtime-localized display text, including the automatic frame-rate option, use `SelectorDisplayOption` collections owned by `MainWindowViewModel`; item and selection-box templates bind the same mutable display value so open lists and current selections refresh together. `DisplayOptionCoordinator` owns localized option construction, clip-list incremental synchronization, and frame-rate index mapping, while `ChapterCellEdit` and `ChapterGridColumnIds` are standalone binding-contract types.

Secondary tool windows consume the stable interfaces in `Session/Ports/ShellPorts.cs` through `MainWindowPortAdapters`. The adapters own expression application and validation, live preference application, language persistence, export/naming projection, and chapter-edit commands; `MainWindowViewModel` does not implement those ports.

Appearance is preset-only and owned by `SettingsAppearanceViewModel` (bound as `Appearance.*` from `SettingsToolView`). It owns localized preset options, font family catalogs, live selection, and palette preview metadata. `AvaloniaThemeApplicationService` resolves the catalog preset (including semantic frame/diagnostic colors from `ThemePalette`), updates application brushes and the Avalonia light/dark variant, while `App.axaml` owns shared control and `DataGridColumnHeader` semantic styles.

Font appearance is split into independent UI and monospace families. `AvaloniaFontFamilyCatalog` snapshots and canonicalizes system fonts, lazily resolves localized family metadata for the active UI culture, and keeps canonical names for persistence. `AvaloniaFontApplicationService` resolves unavailable choices and updates `ChapterTool.UiFontFamily` and `ChapterTool.MonospaceFontFamily`. `App.axaml` applies the UI family through window inheritance and table headers, while chapter `DataGridCell`, `OrderShiftBox`, `ExpressionEditor`, and `TextToolView` consume the monospace resource so existing surfaces refresh at runtime without changing icon fonts.

### CLI behavior

Start with:

- `src/ChapterTool.CommandLine/ChapterToolCliHost.cs`
- `src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.cs`
- `src/ChapterTool.Cli/Program.cs`
- `src/ChapterTool.Avalonia/Program.cs`

Use `ChapterTool.CommandLine/Cli/ChapterToolCliCommands.cs` and `ChapterTool.CommandLine/Cli/ChapterToolCliSupport.cs` for DotMake command definitions, bound launch-plan analysis, and supported format definitions. The Avalonia program uses the typed facade for GUI compatibility. The standalone program delegates process startup to the same facade.

### Localization changes

Start with:

- `src/ChapterTool.Avalonia/Localization/Resources/`

If resource projection or language switching behavior changes, inspect:

- `src/ChapterTool.Avalonia/Localization/AppLocalizationManager.cs`
