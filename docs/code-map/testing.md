# Test Code Map

This file maps production areas to the test projects and high-signal test files that verify them.

## Test Projects

- Core behavior:
  - `tests/ChapterTool.Core.Tests`
- Infrastructure behavior:
  - `tests/ChapterTool.Infrastructure.Tests`
- Avalonia shell, runtime UI services, localization, CLI:
  - `tests/ChapterTool.Avalonia.Tests`

## Core Test Map

Use `tests/ChapterTool.Core.Tests` when changing pure parsing, editing, transform, or export behavior.

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
  - `tests/ChapterTool.Infrastructure.Tests/FontSettingsStoreTests.cs`
  - `tests/ChapterTool.Infrastructure.Tests/ThemePresetCatalogTests.cs`

Fixtures:

- `tests/ChapterTool.Infrastructure.Tests/Fixtures/Importing/Media/`

## Avalonia Test Map

Use `tests/ChapterTool.Avalonia.Tests` when changing UI shell, view models, runtime UI services, localization, headless interaction flows, or CLI behavior.

High-signal test files:

- view models
  - `tests/ChapterTool.Avalonia.Tests/ViewModels/MainWindowViewModelTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/ViewModels/SettingsToolViewModelTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/ViewModels/ToolWindowViewModelTests.cs`
- commands and services
  - `tests/ChapterTool.Avalonia.Tests/Commands/UiCommandTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Services/`
  - `tests/ChapterTool.Avalonia.Tests/Services/AvaloniaThemeApplicationServiceTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Services/AvaloniaFontApplicationServiceTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Services/AvaloniaFontFamilyCatalogTests.cs`
- CLI
  - `tests/ChapterTool.Avalonia.Tests/Cli/ChapterToolCliApplicationTests.cs`
- localization
  - `tests/ChapterTool.Avalonia.Tests/Localization/LocalizationTests.cs`
- headless shell/interaction/integration
  - `tests/ChapterTool.Avalonia.Tests/Headless/MainWindowHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Headless/MainWindowInteractionHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Headless/MainWindowStateHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Headless/LocalizationAndLayoutHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Headless/ToolViewsHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Headless/SettingsToolHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Headless/MainWindowHeadlessTestHost.cs`

Theme preset coverage is concentrated in `ThemePresetCatalogTests`, `SettingsToolViewModelTests`, `AvaloniaThemeApplicationServiceTests`, and `SettingsToolHeadlessTests`. The Headless workflow switches representative light/dark presets and verifies the live palette preview, application variant, semantic resources, and existing DataGrid column-header brushes.

Font settings coverage is concentrated in `FontSettingsStoreTests`, `AvaloniaFontFamilyCatalogTests`, `AvaloniaFontApplicationServiceTests`, `AppCompositionRootFontTests`, `SettingsToolViewModelTests`, and `SettingsToolHeadlessTests`. Catalog/ViewModel tests verify active-language family display names without changing canonical identity. The Headless workflow selects different UI/monospace families and verifies virtualized per-family options, live semantic resources, existing normal/editor/preview/table-cell surfaces, UI-font table headers and order-shift labels, monospace order-shift numeric entry, accessible previews, Save/Discard outcomes, and icon visibility.

## Quick Routing

- parsing or export semantics changed: start in `tests/ChapterTool.Core.Tests`
- external tool, settings, process, or platform boundary changed: start in `tests/ChapterTool.Infrastructure.Tests`
- view, viewmodel, CLI, localization, or runtime UI orchestration changed: start in `tests/ChapterTool.Avalonia.Tests`

## Distribution Verification

- Maintained publish entry points:
  - `scripts/publish.sh`
  - `scripts/publish.ps1`
  - `.github/workflows/dotnet-ci.yml`
- Current distribution notes:
  - `dist/README.md`
- The legacy Windows NSIS installer inputs are retired. Future installer work should consume the `src/ChapterTool.Avalonia` publish output and derive version metadata from `Directory.Build.props`.
