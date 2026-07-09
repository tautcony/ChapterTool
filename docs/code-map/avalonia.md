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
- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs`

Role split:

- `MainWindow.axaml`: shell layout and bindings
- `MainWindow.axaml.cs`: drag/drop, picker triggers, keyboard/UI-only behavior
- `MainWindowViewModel.cs`: commands, state, workflow orchestration, status/progress, tool windows

### Composition root

Runtime wiring is centralized in:

- `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs`

This is the first file to inspect when dependency wiring or service registration changes.

### Views

- `src/ChapterTool.Avalonia/Views/MainWindow.axaml`
- `src/ChapterTool.Avalonia/Views/Controls/ExpressionEditor.axaml`
- `src/ChapterTool.Avalonia/Views/Tools/SettingsToolView.axaml`
- `src/ChapterTool.Avalonia/Views/Tools/ColorSettingsView.axaml`
- `src/ChapterTool.Avalonia/Views/Tools/LanguageToolView.axaml`
- `src/ChapterTool.Avalonia/Views/Tools/ExpressionToolView.axaml`
- `src/ChapterTool.Avalonia/Views/Tools/TemplateNamesToolView.axaml`
- `src/ChapterTool.Avalonia/Views/Tools/ForwardShiftToolView.axaml`
- `src/ChapterTool.Avalonia/Views/Tools/TextToolView.axaml`

### ViewModels

- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs`
- `src/ChapterTool.Avalonia/ViewModels/SettingsToolViewModel.cs`
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

### Load/save/import behavior exposed in UI

Start with:

- `src/ChapterTool.Avalonia/Services/RuntimeChapterLoadService.cs`
- `src/ChapterTool.Avalonia/Services/RuntimeChapterSaveService.cs`
- `src/ChapterTool.Avalonia/Services/RuntimeChapterImporterRegistry.cs`

If the wiring looks wrong, inspect:

- `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs`

### Expression editor UI

Start with:

- `src/ChapterTool.Avalonia/Views/Tools/ExpressionToolView.axaml`
- `src/ChapterTool.Avalonia/Views/Controls/ExpressionEditor.axaml`
- `src/ChapterTool.Avalonia/ViewModels/ToolWindowViewModels.cs`

### Settings / theme / language UI

Start with:

- `src/ChapterTool.Avalonia/ViewModels/SettingsToolViewModel.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaWindowService.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaThemeApplicationService.cs`
- `src/ChapterTool.Avalonia/Localization/AppLocalizationManager.cs`

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
