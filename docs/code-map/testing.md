# Test Code Map

This file maps production areas to the test projects and high-signal test files that verify them.

Use ASD-STE100 principles in this document. Keep each sentence short and direct. Keep code identifiers exact.

## Test Projects

- Core behavior:
  - `tests/ChapterTool.Core.Tests`
- Browser WebAssembly workspace behavior:
  - `tests/ChapterTool.Wasm.Tests`
- Infrastructure behavior:
  - `tests/ChapterTool.Infrastructure.Tests`
- Avalonia ViewModels, runtime UI services, localization, CLI:
  - `tests/ChapterTool.Avalonia.Tests`
- Avalonia Headless UI shell/interaction (separate process):
  - `tests/ChapterTool.Avalonia.Headless.Tests`

## Core Test Map

Use `tests/ChapterTool.Core.Tests` when changing pure parsing, editing, transform, or export behavior.

Use `tests/ChapterTool.Wasm.Tests` when you change browser workspace orchestration, byte-based load or reload, templates, selection actions, preview or save projection, or browser localization. The primary file is `tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs`.

High-signal test files:

- importing
  - `tests/ChapterTool.Core.Tests/Importing/TextImporterTests.cs`
  - `tests/ChapterTool.Core.Tests/Importing/CueImporterTests.cs`
  - `tests/ChapterTool.Core.Tests/Importing/DiscImporterTests.cs`
  - `tests/ChapterTool.Core.Tests/Importing/IfoImporterTests.cs`
  - `tests/ChapterTool.Core.Tests/Importing/MplsImporterTests.cs`
  - `tests/ChapterTool.Core.Tests/Importing/XplImporterTests.cs`
  - `tests/ChapterTool.Core.Tests/Importing/MediaChapterImporterTests.cs`
- editing
  - `tests/ChapterTool.Core.Tests/Editing/ChapterEditingServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Editing/ChapterSegmentServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Editing/SampleChapterNameTemplateTests.cs`
- transform
  - `tests/ChapterTool.Core.Tests/Transform/FrameRateServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Transform/ChapterFpsTransformServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Transform/ChapterTimeFormatterTests.cs`
  - `tests/ChapterTool.Core.Tests/Transform/ChapterRoundingTests.cs`
  - `tests/ChapterTool.Core.Tests/Transform/LuaExpressionScriptServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Transform/ExpressionAuthoringServiceTests.cs`
- exporting
  - `tests/ChapterTool.Core.Tests/Exporting/ChapterExportServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Exporting/ChapterOutputProjectionServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Exporting/ChapterConversionServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Exporting/XmlChapterLanguageCatalogTests.cs`

Fixtures:

- `tests/ChapterTool.Core.Tests/Fixtures/`

## Infrastructure Test Map

Use `tests/ChapterTool.Infrastructure.Tests` when changing process/tool/platform/settings behavior or tool-backed import adapters.

High-signal test files:

- tool lookup:
  - `tests/ChapterTool.Infrastructure.Tests/ExternalToolLocatorTests.cs`
- ffprobe:
  - `tests/ChapterTool.Infrastructure.Tests/FfprobeMediaChapterReaderTests.cs`
  - `tests/ChapterTool.Infrastructure.Tests/Importing/FfprobeMediaChapterIntegrationTests.cs`
- MP4 / ATL:
  - `tests/ChapterTool.Infrastructure.Tests/AtlMp4ChapterReaderTests.cs`
  - `tests/ChapterTool.Infrastructure.Tests/Importing/Mp4IntegrationTests.cs`
- Matroska / mkvextract:
  - `tests/ChapterTool.Infrastructure.Tests/MatroskaChapterImporterTests.cs`
  - `tests/ChapterTool.Infrastructure.Tests/Importing/MatroskaIntegrationTests.cs`
- BDMV / eac3to:
  - `tests/ChapterTool.Infrastructure.Tests/BdmvChapterImporterTests.cs`
- process runner:
  - `tests/ChapterTool.Infrastructure.Tests/ProcessRunnerTests.cs`
- platform services:
  - `tests/ChapterTool.Infrastructure.Tests/PlatformServiceTests.cs`
  - `tests/ChapterTool.Infrastructure.Tests/ApplicationLogPanelProviderTests.cs`
- settings persistence:
  - `tests/ChapterTool.Infrastructure.Tests/SettingsMigrationTests.cs`
  - `tests/ChapterTool.Infrastructure.Tests/ChapterToolSettingsFontTests.cs`
  - `tests/ChapterTool.Infrastructure.Tests/ThemePresetCatalogTests.cs`

`SettingsMigrationTests` is the primary behavior coverage for the versioned `settings.json` document. It covers aggregate persistence, snapshot caching, ignored predecessor files, version-zero upgrade, current-version no-rewrite behavior, invalid and future versions, corrupt active-file preservation, and concurrent aggregate updates. `ChapterToolSettingsFontTests` covers font normalization in the unified `font` content.

Fixtures:

- `tests/ChapterTool.Infrastructure.Tests/Fixtures/Importing/Media/`

## Avalonia Test Map

Use `tests/ChapterTool.Avalonia.Tests` for ViewModels, runtime services, localization, and CLI. Use `tests/ChapterTool.Avalonia.Headless.Tests` for rendered UI and interaction workflows. The Headless project uses a separate testhost process. Non-UI unit tests do not share that process.

High-signal test files:

- view models
  - `tests/ChapterTool.Avalonia.Tests/ViewModels/MainWindowViewModelTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/ViewModels/SettingsToolViewModelTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/ViewModels/ToolWindowViewModelTests.cs`
- commands and services
  - `tests/ChapterTool.Avalonia.Tests/Commands/UiCommandTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Services/`
  - `tests/ChapterTool.Avalonia.Tests/Services/AvaloniaFontFamilyCatalogTests.cs`
- CLI
  - `tests/ChapterTool.Avalonia.Tests/Cli/ChapterToolCliApplicationTests.cs`
- localization
  - `tests/ChapterTool.Avalonia.Tests/Localization/LocalizationTests.cs`
- headless shell/interaction/integration
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/MainWindowHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/MainWindowInteractionHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/MainWindowStateHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/ToolViewsHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/SettingsToolHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/AvaloniaWindowServiceHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/MainWindowHeadlessTestHost.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Services/AvaloniaThemeApplicationServiceTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Composition/AppCompositionRootFontTests.cs`

Theme preset coverage is concentrated in `ThemePresetCatalogTests`, `SettingsToolViewModelTests`, `AvaloniaThemeApplicationServiceTests`, and `SettingsToolHeadlessTests`. The Headless workflow switches representative light and dark presets. It verifies the live palette preview, application variant, semantic resources, and DataGrid column-header brushes.

The settings Headless workflows verify the footer settings-folder action, including its left-side placement, accessible label, and routed shell target.

Font settings coverage is concentrated in `ChapterToolSettingsFontTests`, `AvaloniaFontFamilyCatalogTests`, `AppCompositionRootFontTests`, `SettingsToolViewModelTests`, and `SettingsToolHeadlessTests`. Catalog and ViewModel tests verify active-language family display names without changing canonical identity. The Headless workflow selects UI and monospace families. It verifies per-family options, live semantic resources, normal/editor/preview/table-cell surfaces, UI-font table headers and order-shift labels, monospace order-shift input, accessible previews, Save and Discard outcomes, and icon visibility.

Headless tests share one Avalonia UI session in their test process. Close `Popup` and `ContextMenu` surfaces in `finally`. Dispose directly constructed `IDisposable` DataContexts. Use `MainWindowHeadlessTestHost` so window disposal also detaches its content tree. Await a real initialization task for asynchronous startup. Do not use fixed delays or polling to infer completion.

The diagnosis, timing comparisons, affected tests, and repeatable triage procedure are recorded in `docs/testing/headless-performance.md`.

## Quick Routing

- parsing or export semantics changed: start in `tests/ChapterTool.Core.Tests`
- external tool, settings, process, or platform boundary changed: start in `tests/ChapterTool.Infrastructure.Tests`
- viewmodel, CLI, localization, or runtime UI orchestration changed: start in `tests/ChapterTool.Avalonia.Tests`
- XAML shell, rendered controls, or Headless interaction flows changed: start in `tests/ChapterTool.Avalonia.Headless.Tests`

## Distribution Verification

Coverage entry point:

- `scripts/test-coverage.sh` runs the four test projects in sequence. `scripts/coverage.runsettings` configures Coverlet collection. The script excludes generated `*.g.cs` files. It writes XML and HTML output under `artifacts/coverage`.

- Maintained publish entry points:
  - `scripts/publish.sh`
  - `scripts/publish.ps1`
  - `.github/workflows/dotnet-ci.yml`
- Current distribution notes:
  - `dist/README.md`
- The legacy Windows NSIS installer inputs are retired. Future installer work should consume the `src/ChapterTool.Avalonia` publish output and derive version metadata from `Directory.Build.props`.
