# Avalonia Code Map

`src/ChapterTool.Avalonia` owns the desktop shell, CLI entrypoints, view/viewmodel coordination, runtime orchestration, localization, and theme application.

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
- `src/ChapterTool.Avalonia/Session/ChapterWorkspace.cs` — workspace facade: source path, clip session, edit buffer, projection cache, export-preference snapshots, load/append revision + session-token commit APIs

`MainWindowViewModel` is the bindable shell and holds one `ChapterWorkspace`. Load/append progress and results commit only through workspace revision rules; preview uses the composition-injected `ChapterExportService` (same construction path as save), not an ad-hoc export instance.

### Composition root

Runtime wiring is centralized in:

- `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs`

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
- `src/ChapterTool.Avalonia/Services/RuntimeChapterImporterRegistry.cs`
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

- `src/ChapterTool.Avalonia/Cli/ChapterToolCliApplication.cs`
- `src/ChapterTool.Avalonia/Cli/ChapterToolCliCommands.cs`
- `src/ChapterTool.Avalonia/Cli/ChapterToolCliSupport.cs`
- `src/ChapterTool.Avalonia/Cli/CliConsole.cs`

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

- `src/ChapterTool.Avalonia/Services/AvaloniaWindowService.cs`

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
- `src/ChapterTool.Avalonia/Services/RuntimeChapterImporterRegistry.cs`

`RuntimeChapterSaveService` applies UI save-file concerns such as output directory selection, generated file path diagnostics, and the selected `ChapterExportOptions.TextEncoding` / `EmitBom` behavior around Core export content.

If the wiring looks wrong, inspect:

- `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs`

### Expression editor UI

Start with:

- `src/ChapterTool.Avalonia/Views/Tools/ExpressionToolView.axaml`
- `src/ChapterTool.Avalonia/Views/Controls/ExpressionEditor.axaml`
- `src/ChapterTool.Avalonia/Views/MainWindow.axaml.cs`
- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs`
- `src/ChapterTool.Avalonia/ViewModels/ToolWindowViewModels.cs`
- `src/ChapterTool.Core/Transform/ExpressionAuthoringService.cs`

Behavior coverage is concentrated in `ExpressionAuthoringServiceTests`, `MainWindowViewModelTests`, `MainWindowInteractionHeadlessTests`, and `ToolViewsHeadlessTests` for Lua tokens/completions, delayed edit diagnostics, live valid projections, editing-key routing, and single-editor multiline expansion.

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

Output defaults such as the configured save directory, save format, XML language, text encoding, BOM emission, and frame tolerance live in `SettingsToolViewModel` and flow into `MainWindowViewModel.ApplyLivePreferences` (session save format is applied only via `ApplyLoadedSettings` at startup). A directory chosen from the main-window save workflow updates only the current session and does not overwrite the configured default. `AppCompositionRoot` constructs one `ChapterToolSettingsStore` shared directly by runtime consumers; startup loads one aggregate snapshot for theme and font, while the settings tool loads once, dirty-checks a single `ChapterToolSettings` snapshot, and commits all child changes once. It also passes the resolved settings directory through `AvaloniaWindowService` so the settings footer can open the owning folder through `IShellService`.

Main-window selectors with runtime-localized display text, including the automatic frame-rate option, use `SelectorDisplayOption` collections owned by `MainWindowViewModel`; item and selection-box templates bind the same mutable display value so open lists and current selections refresh together.

Appearance is preset-only and owned by `SettingsAppearanceViewModel` (bound as `Appearance.*` from `SettingsToolView`). It owns localized preset options, font family catalogs, live selection, and palette preview metadata. `AvaloniaThemeApplicationService` resolves the catalog preset (including semantic frame/diagnostic colors from `ThemePalette`), updates application brushes and the Avalonia light/dark variant, while `App.axaml` owns shared control and `DataGridColumnHeader` semantic styles.

Font appearance is split into independent UI and monospace families. `AvaloniaFontFamilyCatalog` snapshots and canonicalizes system fonts, lazily resolves localized family metadata for the active UI culture, and keeps canonical names for persistence. `AvaloniaFontApplicationService` resolves unavailable choices and updates `ChapterTool.UiFontFamily` and `ChapterTool.MonospaceFontFamily`. `App.axaml` applies the UI family through window inheritance and table headers, while chapter `DataGridCell`, `OrderShiftBox`, `ExpressionEditor`, and `TextToolView` consume the monospace resource so existing surfaces refresh at runtime without changing icon fonts.

### CLI behavior

Start with:

- `src/ChapterTool.Avalonia/Program.cs`
- `src/ChapterTool.Avalonia/Cli/ChapterToolCliApplication.cs`

Use `ChapterToolCliCommands.cs` and `ChapterToolCliSupport.cs` for DotMake.CommandLine command definitions, bound launch-plan analysis, and supported format definitions.

### Localization changes

Start with:

- `src/ChapterTool.Avalonia/Localization/Resources/`

If resource projection or language switching behavior changes, inspect:

- `src/ChapterTool.Avalonia/Localization/AppLocalizationManager.cs`
