# Avalonia Code Map

`src/ChapterTool.Avalonia.UI` owns the shared Avalonia shell, ViewModels, workflows, resources, and semantic platform ports. `src/ChapterTool.Avalonia` owns the desktop shell and desktop adapter composition.

Use ASD-STE100 principles in this document. Keep each sentence short and direct. Keep code identifiers exact.

## Ownership

### Shared application shell

Shared workflow entry points:

- `src/ChapterTool.Avalonia.UI/Views/MainView.axaml`
- `src/ChapterTool.Avalonia.UI/Views/MainView.axaml.cs`
- `src/ChapterTool.Avalonia.UI/ViewModels/MainWindowViewModel.cs`
- `src/ChapterTool.Avalonia.UI/Workflows/`
- `src/ChapterTool.Avalonia.UI/PlatformPorts/`

### Desktop application shell

Startup and main shell entry points:

- `src/ChapterTool.Avalonia/Program.cs`
- `src/ChapterTool.Avalonia/Diagnostics/SentryStartupConfiguration.cs`
- `src/ChapterTool.Avalonia/App.axaml`
- `src/ChapterTool.Avalonia/App.axaml.cs`
- `src/ChapterTool.Avalonia/Views/MainWindow.axaml`
- `src/ChapterTool.Avalonia/Views/MainWindow.axaml.cs`

Main-window workflow owners under `src/ChapterTool.Avalonia.UI/Workflows/` use the same `ChapterWorkspace`:

- `LoadSaveWorkflow.cs` — revision/session-aware load, append, and save service orchestration
- `ClipEditingCoordinator.cs` — clip selection/combine transitions plus cell and frame edits written through the workspace
- `ProjectionFacade.cs` — workspace-backed projection, preview/save options, and chapter-row materialization
- `StatusDiagnosticsPresenter.cs` — localized status/progress rendering and structured diagnostic logging

Role split:

- `MainView.axaml`: shared shell layout and bindings
- `MainView.axaml.cs`: drag/drop, keyboard routing, and UI-only adapter commands
- `UiOperationBoundary.cs`: common asynchronous UI exception and cancellation boundary
- Pure workflow commands bind to `MainWindowViewModel` (`SaveCommand`, `ReloadCommand`, `PreviewCommand`, `RefreshCommand`, `CombineCommand`, `OpenRelatedMediaCommand`, and tool-window commands)
- `MainWindowViewModel` partials:
  - `.cs`: fields, ctor, bindable state, command wiring, window/shell helpers
  - `.Settings.cs`: load/apply preferences and language persistence
  - `.ImportExport.cs`: load/save/append workflows, export options, and chapter-name template path application (`LoadChapterNameTemplateFromPathAsync`)
  - `.Expression.cs`: Lua expression apply/validate and output projection
  - `.Editing.cs`: clip selection, row edits, combine/split, frame-rate transforms
  - `.StatusLog.cs`: status text, diagnostics localization, logging, localized option refresh

### Session (clip / workspace)

Shared session kernel lives in Core:

- `src/ChapterTool.Core/Session/ClipSession.cs` — `SplitClipSession` / `CombinedClipSession` and pure transitions
- `src/ChapterTool.Core/Session/ChapterWorkspace.cs` — path, clip session, edit buffer, projection, export preferences, revision commit rules
- `src/ChapterTool.Core/Session/ProjectionState.cs`
- `src/ChapterTool.Core/Session/ExportPreferences.cs`

Avalonia owns only host ports:

- `src/ChapterTool.Avalonia.UI/PlatformPorts/SessionPorts/ShellPorts.cs` — narrow tool ports (`IExpressionSessionPort`, `IPreferenceSink`, …)
- `src/ChapterTool.Avalonia.UI/PlatformPorts/SessionPorts/MainWindowPortAdapters.cs` — concrete main-window adapters

`MainWindowViewModel` is the bindable shell and holds one Core `ChapterWorkspace`. Bindable projection/export properties facade workspace state. Command handlers delegate workflow orchestration to the `Workflows/` collaborators. Load/append commits use workspace revision rules.

### Composition root

Runtime wiring is centralized in:

- `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs`
- `src/ChapterTool.Avalonia/Composition/AppCompositionOptions.cs`
- `src/ChapterTool.Avalonia/Composition/LoggingModule.cs`
- `src/ChapterTool.Avalonia/Composition/WorkspaceModule.cs`
- `src/ChapterTool.Avalonia/Composition/InfrastructureModule.cs`
- `src/ChapterTool.Avalonia/Composition/AvaloniaPlatformModule.cs`
- `src/ChapterTool.Avalonia/Composition/AuxiliaryToolsModule.cs`
- `src/ChapterTool.Avalonia/Composition/ApplicationShellModule.cs`

`AppCompositionRoot` builds the Autofac container and owns its application lifetime scope. It resolves the main window only after `ValidateProductionComposition()` succeeds during desktop startup.

The modules own logging, workspace, infrastructure, Avalonia platform, auxiliary-tool, and application-shell registrations. Shared formatter, expression, export, settings, localization, external-tool, catalog, and host services use one application lifetime instance.

`AppCompositionRoot.CreateHostComposition()` remains the explicit desktop boundary for shared Avalonia composition. Compatibility factory methods resolve services from the same scope. They do not create a second desktop graph.

Window-bound file picker, settings picker, and clipboard services use host-owned factories. The root does not register a global `Window` or `Control` instance.

`AppCompositionOptions.ConfigureOverrides` adds test registrations before the container is built. `RegisterProductionModules = false` creates an intentionally incomplete graph for missing-registration tests.

`src/ChapterTool.Avalonia.UI/PlatformPorts/AuxiliaryTools.cs` owns `ToolId`, typed auxiliary-tool requests and results, host service groups, catalog descriptors, and the embedded presenter contract. `EmbeddedAuxiliaryToolHost.cs` provides the single-content host implementation. `UnavailableHostAdapters.cs` provides explicit no-op adapters for unavailable capabilities.

`BrowserPortableAdapters.cs` remains in the shared assembly because it contains no browser API implementation. It defines the bounded source-read behavior and the `IBrowserFileAccess` host port. A browser host owns the `IBrowserFileAccess` implementation.

For GUI production paths, one `AppCompositionRoot` shares the formatter, expression engine, authoring service, export service, process runner, and external-tool locator across the main window and tool windows. `ExpressionEditor` receives `IExpressionAuthoringService` through the host composition and the typed `ToolCreationContext`. Its private fallback is limited to direct design-time or test construction.

The lifetime contract is covered by `tests/ChapterTool.Avalonia.Headless.Tests/Composition/AppCompositionRootIdentityHeadlessTests.cs` and `tests/ChapterTool.Avalonia.Headless.Tests/Composition/AutofacCompositionHeadlessTests.cs`. These tests cover shared identity, validation failure, override precedence, and repeated disposal.

Production code uses constructor injection for required dependencies. Production code must not use property injection or field injection. Direct constructors remain available for focused tests that build a small fake graph.

This is the first file to inspect when dependency wiring or service registration changes.

### Views

- `src/ChapterTool.Avalonia/Views/MainWindow.axaml`
- `src/ChapterTool.Avalonia.UI/Views/MainView.axaml`
- `src/ChapterTool.Avalonia.UI/Views/Controls/ExpressionEditor.axaml`
- `src/ChapterTool.Avalonia.UI/Views/Tools/LogToolView.axaml`
- `src/ChapterTool.Avalonia.UI/Views/Tools/SettingsToolView.axaml`
- `src/ChapterTool.Avalonia.UI/Views/Tools/LanguageToolView.axaml`
- `src/ChapterTool.Avalonia.UI/Views/Tools/ExpressionToolView.axaml`
- `src/ChapterTool.Avalonia.UI/Views/Tools/TemplateNamesToolView.axaml`
- `src/ChapterTool.Avalonia.UI/Views/Tools/ForwardShiftToolView.axaml`
- `src/ChapterTool.Avalonia.UI/Views/Tools/TextToolView.axaml`

### Imported theme resources

`src/ChapterTool.Avalonia.UI/Resources/` owns the shared and imported user interface resources.

- `Themes.axaml` contains the imported light and dark token dictionaries. Surface colors use `Brush.*` tokens.
- `Styles.axaml` contains the reusable control styles for Avalonia 12.1. Shared classes include `flat`, `icon_button`, `toolFooter`, `toolToolbar`, `optionLabel`, `optionCell`, `gridEditor`, and `frameText`.
- `SharedResources.axaml` keeps fonts, font-size tokens, and semantic `ChapterTool.*` brushes for frame accuracy, diagnostics, log levels, and expression highlighting.
- `NOTICE.md` records the source, license scope, exclusions, and compatibility adaptations.
- Only `Themes.axaml` and `Styles.axaml` contain adapted SourceGit MIT material.

`App.axaml` loads these resources after the Avalonia base themes. It loads ChapterTool product styles after the imported theme layer.

The Load control is a `SplitButton`. Reload and Append MPLS live in its flyout. Change FPS is a visible `icon_button` next to the frame-rate selector.

### ViewModels

- `src/ChapterTool.Avalonia.UI/ViewModels/MainWindowViewModel*.cs`
- `src/ChapterTool.Avalonia.UI/ViewModels/SettingsToolViewModel.cs` (settings monologue by design; appearance lives in `SettingsAppearanceViewModel`)
- `src/ChapterTool.Avalonia.UI/ViewModels/SettingsAppearanceViewModel.cs`
- `src/ChapterTool.Avalonia.UI/ViewModels/ChapterExpressionValidation.cs`
- `src/ChapterTool.Avalonia.UI/ViewModels/ChapterSaveDirectory.cs`
- `src/ChapterTool.Avalonia.UI/ViewModels/Tools/`
- `src/ChapterTool.Avalonia.UI/ViewModels/ChapterRowViewModel.cs`
- `src/ChapterTool.Avalonia.UI/ViewModels/UiCommand.cs`
- `src/ChapterTool.Avalonia.UI/ViewModels/ShortcutRouter.cs`
- `src/ChapterTool.Avalonia.UI/ViewModels/Tools/LogToolViewModel.cs`

### Runtime and UI services

- `src/ChapterTool.Avalonia/Services/RuntimeChapterLoadService.cs`
- `src/ChapterTool.Avalonia/Services/RuntimeChapterSaveService.cs`
- `src/ChapterTool.Avalonia/Services/ChapterNameTemplateReader.cs`
- `src/ChapterTool.Infrastructure/Importing/Runtime/RuntimeChapterImporterRegistry.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaWindowService.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaFilePickerService.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaSettingsPickerService.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaThemeApplicationService.cs`
- `src/ChapterTool.Avalonia.UI/PlatformPorts/AvaloniaFontApplicationService.cs`
- `src/ChapterTool.Avalonia.UI/PlatformPorts/IFontFamilyCatalog.cs`
- `src/ChapterTool.Avalonia/Services/FontFamilyCatalogEntry.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaFontFamilyCatalog.cs`
- `src/ChapterTool.Avalonia/Services/FontSettingsResolver.cs`

### CLI

- `src/ChapterTool.CommandLine/ChapterToolCliHost.cs`
- `src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.cs`
- `src/ChapterTool.CommandLine/Cli/ChapterToolCliCommands.cs`
- `src/ChapterTool.CommandLine/Cli/ChapterToolCliSupport.cs`
- `src/ChapterTool.CommandLine/Cli/CliLocalizationManager.cs`
- `src/ChapterTool.CommandLine/Resources/Locales/`
- `src/ChapterTool.CommandLine/Cli/CliConsole.cs`
- `src/ChapterTool.CommandLine/Program.cs`
- `src/ChapterTool.CommandLine/ChapterTool.CommandLine.csproj`
- `src/ChapterTool.CommandLine/README.md`

### Localization

- `src/ChapterTool.Avalonia.UI/Localization/AppLocalizationManager.cs`
- `src/ChapterTool.Avalonia.UI/Localization/IAppLocalizer.cs`
- `src/ChapterTool.Avalonia.UI/Localization/AppLocalizationResources.cs`
- `src/ChapterTool.Avalonia.UI/Localization/AppLanguage.cs`
- `src/ChapterTool.Avalonia.UI/Localization/Resources/Locales/`
- `src/ChapterTool.Avalonia.UI/Localization/AvaloniaLocalizationResourceAdapter.cs`

## Feature Lookup

### Main window layout, binding, workflow zones

Start with:

- `src/ChapterTool.Avalonia/Views/MainWindow.axaml`
- `src/ChapterTool.Avalonia/Views/MainWindow.axaml.cs`
- `src/ChapterTool.Avalonia.UI/ViewModels/MainWindowViewModel.cs`

### Main command workflow

Start with:

- `src/ChapterTool.Avalonia.UI/ViewModels/MainWindowViewModel.cs`

If keyboard routing matters:

- `src/ChapterTool.Avalonia.UI/ViewModels/ShortcutRouter.cs`

If command execution semantics change:

- `src/ChapterTool.Avalonia.UI/ViewModels/UiCommand.cs`

### Tool windows

Start with:

- `src/ChapterTool.Avalonia/Services/StandardToolCatalogFactory.cs` — standard `ToolDescriptor` construction
- `src/ChapterTool.Avalonia/Services/AvaloniaWindowService.cs` — Native Window host lifecycle and typed catalog lookup
- `src/ChapterTool.Avalonia.UI/PlatformPorts/EmbeddedAuxiliaryToolHost.cs` — Embedded host lifecycle and presenter updates
- `src/ChapterTool.Avalonia.UI/PlatformPorts/AuxiliaryTools.cs` — typed identifiers, catalog, host, and presenter contracts
- `src/ChapterTool.Avalonia.UI/PlatformPorts/SessionPorts/ShellPorts.cs` — narrow tool ports (`IExpressionSessionPort`, `IPreferenceSink`, `IExportPreferencePort`, …)
- `src/ChapterTool.Avalonia.UI/PlatformPorts/SessionPorts/WorkspaceToolSession.cs` — workspace tool-session facade and shell notification port

Then inspect the matching pair in:

- `src/ChapterTool.Avalonia.UI/Views/Tools/`
- `src/ChapterTool.Avalonia.UI/ViewModels/`

### Application log window

Start with:

- `src/ChapterTool.Avalonia.UI/ViewModels/Tools/LogToolViewModel.cs`
- `src/ChapterTool.Avalonia.UI/Views/Tools/LogToolView.axaml`
- `src/ChapterTool.Avalonia/Services/StandardToolCatalogFactory.cs`
- `src/ChapterTool.Contracts/PlatformPorts/IApplicationLogService.cs`
- `src/ChapterTool.Infrastructure/Platform/ApplicationLogPanelProvider.cs`

The ViewModel owns the filtered projection, selection, localized display text, and copy commands. The provider owns bounded history and live entry notifications. The view uses the imported master-detail composition and resources.

### Clip combine / multi-entry session

Start with:

- `src/ChapterTool.Core/Session/ClipSession.cs`
- `src/ChapterTool.Avalonia.UI/ViewModels/MainWindowViewModel.Editing.cs`
- `src/ChapterTool.Avalonia.UI/ViewModels/MainWindowViewModel.ImportExport.cs`

Pure transition coverage: `tests/ChapterTool.Core.Tests/Session/ClipSessionTests.cs`. Concurrent load/append anti-stale coverage remains in `MainWindowViewModelTests`.

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

- `src/ChapterTool.Avalonia.UI/Views/Tools/ExpressionToolView.axaml`
- `src/ChapterTool.Avalonia.UI/Views/Controls/ExpressionEditor.axaml`
- `src/ChapterTool.Avalonia/Views/MainWindow.axaml.cs`
- `src/ChapterTool.Avalonia.UI/ViewModels/MainWindowViewModel.cs`
- `src/ChapterTool.Avalonia.UI/ViewModels/Tools/`
- `src/ChapterTool.Core/Transform/ExpressionAuthoringService.cs`

Behavior coverage is concentrated in `ExpressionAuthoringServiceTests`, `MainWindowViewModelTests`, `MainWindowInteractionHeadlessTests`, and `ToolViewsHeadlessTests` for Lua tokens/completions, delayed edit diagnostics, live valid projections, editing-key routing, and single-editor multiline expansion.
`AppCompositionRootIdentityHeadlessTests` additionally exercises both production XAML editor hosts with a sentinel authoring service, including initial binding and subsequent text edits.

### Settings / theme / language UI

Start with:

- `src/ChapterTool.Avalonia.UI/ViewModels/SettingsToolViewModel.cs`
- `src/ChapterTool.Avalonia.UI/ViewModels/SettingsAppearanceViewModel.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaWindowService.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaThemeApplicationService.cs`
- `src/ChapterTool.Avalonia.UI/PlatformPorts/AvaloniaFontApplicationService.cs`
- `src/ChapterTool.Avalonia.UI/PlatformPorts/IFontFamilyCatalog.cs`
- `src/ChapterTool.Avalonia/Services/AvaloniaFontFamilyCatalog.cs`
- `src/ChapterTool.Avalonia.UI/Localization/AppLocalizationManager.cs`
- `src/ChapterTool.Avalonia.UI/Views/Tools/SettingsToolView.axaml`
- `src/ChapterTool.Avalonia/App.axaml`

Output defaults, external-tool paths and statuses, and runtime/footer display state live in `SettingsToolViewModel`; it flows live preferences through `PreferenceSinkAdapter` (session save format is applied only when startup settings are loaded). There are no unused `Settings*Module` placeholder types. A directory chosen from the main-window save workflow updates only the current session and does not overwrite the configured default. `AppCompositionRoot` constructs one `ChapterToolSettingsStore` shared directly by runtime consumers; startup loads one aggregate snapshot for theme and font, while the settings tool loads once, dirty-checks a single `ChapterToolSettings` snapshot, and commits all child changes once. It also passes the resolved settings directory through `AvaloniaWindowService` so the settings footer can open the owning folder through `IShellService`.

Main-window selectors with runtime-localized display text, including the automatic frame-rate option, use `SelectorDisplayOption` collections owned by `MainWindowViewModel`; item and selection-box templates bind the same mutable display value so open lists and current selections refresh together. `DisplayOptionCoordinator` owns localized option construction, clip-list incremental synchronization, and frame-rate index mapping, while `ChapterCellEdit` and `ChapterGridColumnIds` are standalone binding-contract types.

Secondary tool windows consume the stable interfaces in `PlatformPorts/SessionPorts/ShellPorts.cs` through `MainWindowPortAdapters`. The adapters own expression application and validation, live preference application, language persistence, export/naming projection, and chapter-edit commands; `MainWindowViewModel` does not implement those ports.

Appearance is preset-only and owned by `SettingsAppearanceViewModel` (bound as `Appearance.*` from `SettingsToolView`). It owns localized preset options, font family catalogs, live selection, and palette preview metadata. `AvaloniaThemeApplicationService` resolves the catalog preset. It updates ChapterTool semantic brushes, all imported `Color.*` tokens, and the Avalonia light or dark variant. `App.axaml` loads the imported theme foundation and applies later ChapterTool product styles.

Font appearance is split into independent UI and monospace families. `AvaloniaFontFamilyCatalog` snapshots and canonicalizes system fonts, lazily resolves localized family metadata for the active UI culture, and keeps canonical names for persistence. `AvaloniaFontApplicationService` resolves unavailable choices and updates `ChapterTool.UiFontFamily` and `ChapterTool.MonospaceFontFamily`. `App.axaml` applies the UI family through window inheritance and table headers, while chapter `DataGridCell`, `OrderShiftBox`, `ExpressionEditor`, and `TextToolView` consume the monospace resource so existing surfaces refresh at runtime without changing icon fonts.

### CLI behavior

Start with:

- `src/ChapterTool.CommandLine/ChapterToolCliHost.cs`
- `src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.cs`
- `src/ChapterTool.CommandLine/Program.cs`
- `src/ChapterTool.Avalonia/Program.cs`

Use `ChapterTool.CommandLine/Cli/ChapterToolCliCommands.cs` and `ChapterTool.CommandLine/Cli/ChapterToolCliSupport.cs` for DotMake command definitions, command parsing, and supported format definitions. The Avalonia program does not reference or dispatch CLI commands. The merged CommandLine executable delegates process startup to `ChapterToolCliHost`.

`src/ChapterTool.CommandLine/ChapterTool.CommandLine.csproj` owns the `ChapterTool` NuGet package metadata. The package installs the `chaptertool` command. `.github/workflows/dotnet-ci.yml` uploads the CLI package as `ChapterTool-Cli-nuget` and uploads each Avalonia runtime output as `ChapterTool-Avalonia-<runtime>`. `.github/workflows/nuget-publish.yml` publishes the tool and `ChapterTool.Core` from the same version tag.

### Localization changes

Start with:

- `src/ChapterTool.Avalonia.UI/Localization/Resources/Locales/`
- `src/ChapterTool.CommandLine/Cli/CliLocalizationManager.cs`

If resource projection or language switching behavior changes, inspect:

- `src/ChapterTool.Avalonia.UI/Localization/AppLocalizationManager.cs`
- `src/ChapterTool.Avalonia.UI/Localization/AvaloniaLocalizationResourceAdapter.cs`
