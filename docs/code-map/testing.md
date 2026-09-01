# Test Code Map

This file maps production areas to the test projects and high-signal test files that verify them.

Use ASD-STE100 principles in this document. Keep each sentence short and direct. Keep code identifiers exact.

## Test Projects

- Shared test helpers:
  - `tests/ChapterTool.TestSupport`
  - `TestRepository` locates the repository root from `ChapterTool.slnx`.
  - `TestApplicationLogger` builds loggers for Avalonia unit and Headless tests.
- Core behavior:
  - `tests/ChapterTool.Core.Tests`
- Browser WebAssembly workspace behavior:
  - `tests/ChapterTool.Wasm.Tests`
- Node.js package behavior (Vitest, one worker for the WASM runtime):
  - `packages/chaptertool/test/chaptertool.test.ts`
  - `packages/chaptertool/test/core-api.test.ts`
  - `packages/chaptertool/test/api-loader.test.ts`
- Infrastructure behavior:
  - `tests/ChapterTool.Infrastructure.Tests`
- CommandLine workflows:
  - `tests/ChapterTool.CommandLine.Tests`
- Avalonia ViewModels, runtime UI services, and desktop localization:
  - `tests/ChapterTool.Avalonia.Tests`
- Avalonia Headless UI shell/interaction (separate process):
  - `tests/ChapterTool.Avalonia.Headless.Tests`

Desktop composition coverage:

- `tests/ChapterTool.Avalonia.Headless.Tests/Composition/AutofacCompositionHeadlessTests.cs` validates missing registrations, test overrides, and repeated disposal.
- `tests/ChapterTool.Avalonia.Headless.Tests/Composition/AppCompositionRootIdentityHeadlessTests.cs` validates shared service identity and shell resolution.
- `tests/ChapterTool.Avalonia.Headless.Tests` references Autofac only to test the desktop composition boundary.

## Core Test Map

Use `tests/ChapterTool.Core.Tests` when changing pure parsing, editing, transform, or export behavior.

Use `tests/ChapterTool.Wasm.Tests` when you change the Blazor browser workspace, bounded byte input, browser settings, or browser export paths. The primary file is `tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs`. `tests/ChapterTool.Wasm.Tests/WasmBrowserShortcutGuardTests.cs` covers the browser shortcut guard.

Use `packages/chaptertool/test/chaptertool.test.ts` when you change the Node.js package entry point, TypeScript input conversion, or npm runtime packaging. Use `packages/chaptertool/test/api-loader.test.ts` when you change retryable .NET WebAssembly startup. Use `packages/chaptertool/test/core-api.test.ts` when you change the portable Core API mapping. Run `npm test` from `packages/chaptertool`. The command bundles the TypeScript source, checks its types, and generates `dist/` before Vitest runs the Node.js tests through the package export map. `packages/chaptertool/vitest.config.mjs` keeps the process-wide WebAssembly runtime in one test worker.

The `.NET 10 CI` workflow builds `dist/` once for changes under `packages/chaptertool`. The build and test job runs `npm run typecheck` and `npm run test:built` against this output. The npm pack job downloads the same output. It runs `npm run pack:verify` without lifecycle scripts. The pack check installs the generated tarball into a temporary consumer and calls `ChapterTool.import`. The `Publish to npm` workflow uses npm Trusted Publishing after a successful version tag run. Configure the GitHub Actions trusted publisher for this workflow and the `npm` environment on npmjs.com.

High-signal test files:

- importing
  - `tests/ChapterTool.Core.Tests/Importing/TextImporterTests.cs`
  - `tests/ChapterTool.Core.Tests/Importing/CueImporterTests.cs`
  - `tests/ChapterTool.Core.Tests/Importing/DiscImporterTests.cs`
  - `tests/ChapterTool.Core.Tests/Importing/MovieObjectNavigationTests.cs`
  - `tests/ChapterTool.Core.Tests/Importing/MplsExtensionDataTests.cs`
  - `tests/ChapterTool.Core.Tests/Importing/IfoImporterTests.cs`
  - `tests/ChapterTool.Core.Tests/Importing/MplsImporterTests.cs`
  - `tests/ChapterTool.Core.Tests/Importing/XplImporterTests.cs`
  - `tests/ChapterTool.Core.Tests/Importing/MediaChapterImporterTests.cs`
- editing
  - `tests/ChapterTool.Core.Tests/Editing/ChapterEditingServiceTests.cs` (delete-rows timing and frame display options coverage)
  - `tests/ChapterTool.Core.Tests/Editing/ChapterSegmentServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Editing/SampleChapterNameTemplateTests.cs`
  - `tests/ChapterTool.Core.Tests/Importing/ChapterContentServiceTests.cs`
- transform
  - `tests/ChapterTool.Core.Tests/Transform/FrameRateServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Transform/ChapterFpsTransformServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Transform/ChapterTimeFormatterTests.cs`
  - `tests/ChapterTool.Core.Tests/Transform/ChapterRoundingTests.cs`
  - `tests/ChapterTool.Core.Tests/Transform/ChapterExpressionServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Transform/LuaExpressionScriptServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Transform/ExpressionAuthoringServiceTests.cs`
- exporting
  - `tests/ChapterTool.Core.Tests/Exporting/ChapterExportServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Exporting/ChapterOutputProjectionServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Exporting/ChapterConversionServiceTests.cs`
  - `tests/ChapterTool.Core.Tests/Exporting/XmlChapterLanguageCatalogTests.cs`
  - `tests/ChapterTool.Core.Tests/Exporting/OutputTextEncodingTests.cs`
  - `tests/ChapterTool.Core.Tests/Exporting/ChapterSavePathTests.cs`
- boundaries and localization
  - `tests/ChapterTool.Core.Tests/Boundaries/PortableInputPolicyTests.cs`
  - `tests/ChapterTool.Core.Tests/Localization/UiLanguageCodeTests.cs`

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
- BDMV:
  - `tests/ChapterTool.Infrastructure.Tests/Importing/BdmvImporterTests.cs`
  - `tests/ChapterTool.Infrastructure.Tests/Importing/BdmvBdjoNavigationTests.cs`

BDMV compatibility tests:

- `tests/ChapterTool.Core.Tests/Importing/MovieObjectNavigationTests.cs` covers MovieObject decoding, bounded HDMV execution, PSR/GPR behavior, control events, arithmetic, and compare options. The `ResolverUsesDesiredSourceBitsForBitCompare` theory protects the `libbluray` `INSN_BC` operand direction.
- `tests/ChapterTool.Core.Tests/Importing/DiscImporterTests.cs`, `IndexImporterTests.cs`, and `ClpiImporterTests.cs` cover the managed INDEX and CLPI boundaries that correspond to `libbluray/src/libbluray/bdnav/`.
- `tests/ChapterTool.Infrastructure.Tests/Importing/BdmvImporterTests.cs` and `BdmvBdjoNavigationTests.cs` cover navigation evidence, primary/backup selection, and playlist fallback. They do not execute the native `libbluray` code.
- process runner:
  - `tests/ChapterTool.Infrastructure.Tests/ProcessRunnerTests.cs`
- runtime composition:
  - `tests/ChapterTool.Infrastructure.Tests/ChapterToolRuntimeCompositionTests.cs`
- platform services:
  - `tests/ChapterTool.Infrastructure.Tests/PlatformServiceTests.cs`
  - `tests/ChapterTool.Infrastructure.Tests/ApplicationLogPanelProviderTests.cs`
  - `tests/ChapterTool.Infrastructure.Tests/ApplicationLogFileExporterTests.cs`
- settings persistence:
  - `tests/ChapterTool.Infrastructure.Tests/SettingsMigrationTests.cs`
  - `tests/ChapterTool.Infrastructure.Tests/CorruptSettingsFileTests.cs`
  - `tests/ChapterTool.Infrastructure.Tests/ChapterToolSettingsFontTests.cs`
  - `tests/ChapterTool.Infrastructure.Tests/ThemePresetCatalogTests.cs`

`SettingsMigrationTests` is the primary behavior coverage for the versioned `settings.json` document. It covers aggregate persistence, snapshot caching, ignored predecessor files, version-zero upgrade, current-version no-rewrite behavior, invalid and future versions, corrupt active-file preservation, and concurrent aggregate updates. `ChapterToolSettingsFontTests` covers font normalization in the unified `font` content.

Fixtures:

- `tests/ChapterTool.Infrastructure.Tests/Fixtures/Importing/Media/`

## Avalonia Test Map

Use `tests/ChapterTool.CommandLine.Tests` for DotMake binding and CLI workflows. Use `tests/ChapterTool.Avalonia.Tests` for ViewModels, runtime services, and localization. Use `tests/ChapterTool.Avalonia.Headless.Tests` for rendered UI and interaction workflows. The Headless project uses a separate testhost process. Non-UI unit tests do not share that process.

High-signal test files:

- view models
  - `tests/ChapterTool.Avalonia.Tests/ViewModels/MainWindowViewModelTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/ViewModels/SettingsToolViewModelTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/ViewModels/SettingsSnapshotCoordinatorTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/ViewModels/ToolWindowViewModelTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/ViewModels/ToolViewModelPortConstructionTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/ViewModels/LogToolViewModelTests.cs`

LogTool coverage is split by boundary. `LogToolViewModelTests` covers list-first projection, severity and text filters, compact summaries, explicit inspector selection, search highlights, flat structured properties, raw values, live updates, eviction handling, localization, and secondary command state. `tests/ChapterTool.Infrastructure.Tests/ApplicationLogPanelProviderTests.cs` covers append-order snapshots, minimum-level filtering, bounded retention, clear notifications, and concurrent access. `tests/ChapterTool.Infrastructure.Tests/ApplicationLogFileExporterTests.cs` covers UTF-8 JSON and CSV output, deterministic ordering, CSV quoting, output paths, and recoverable failures. `tests/ChapterTool.Avalonia.Headless.Tests/Headless/AuxiliaryToolHeadlessTests.cs` and `UiResourceResolutionHeadlessTests.cs` cover rendered list and inspector workflows, keyboard close behavior, responsive layouts, and locale resource resolution.
- commands and services
  - `tests/ChapterTool.Avalonia.Tests/Commands/UiCommandTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Services/RuntimeChapterLoadServiceTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Services/RuntimeChapterSaveServiceTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Services/AvaloniaPickerServiceTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Services/AvaloniaFontFamilyCatalogTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Services/ChapterImporterRegistryTests.cs`
- views and expression presentation
  - `tests/ChapterTool.Avalonia.Tests/Views/MainViewLayoutTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/Views/ExpressionThemeBrushesTests.cs`
- cross-host contracts
  - `tests/ChapterTool.Avalonia.Tests/PlatformPorts/AuxiliaryToolContractTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/PlatformPorts/SharedBoundaryContractTests.cs`
- architecture and guards
  - `tests/ChapterTool.Avalonia.Tests/Architecture/HostDependencyBoundaryTests.cs`
  - `tests/ChapterTool.Avalonia.Tests/NoAvaloniaHeadlessAttributeGuardTests.cs`
- CLI
  - `tests/ChapterTool.CommandLine.Tests/Cli/ChapterToolCliApplicationTests.cs`
  - `src/ChapterTool.CommandLine/ChapterToolCliHost.cs`
  - `src/ChapterTool.CommandLine/Program.cs`
- desktop localization
  - `tests/ChapterTool.Avalonia.Tests/Localization/LocalizationTests.cs`
- headless shell/interaction/integration
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/MainWindowHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/MainWindowInteractionHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/MainWindowStateHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/ToolViewsHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/SettingsToolHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/AvaloniaWindowServiceHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/AvaloniaSettingsCloseConfirmationHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/AuxiliaryToolHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/EmbeddedPresenterHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/UiDesignSystemHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/UiResourceResolutionHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/UiScreenshotCaptureHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/MainWindowHeadlessTestHost.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/HeadlessTestCollectionGuardTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Services/AvaloniaThemeApplicationServiceTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Composition/AutofacCompositionHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Composition/AppCompositionRootIdentityHeadlessTests.cs`
  - `tests/ChapterTool.Avalonia.Headless.Tests/Composition/AppCompositionRootFontTests.cs`

Use `tests/ChapterTool.Avalonia.Tests/PlatformPorts/AuxiliaryToolContractTests.cs` for typed tool identifiers, duplicate catalog validation, custom descriptor selection, embedded reuse, disposal, and unknown-tool results. Use `tests/ChapterTool.Avalonia.Headless.Tests/Headless/AvaloniaWindowServiceHeadlessTests.cs` for Native Window close confirmation and content detachment. Keep Native Window and Headless test projects in separate processes.

Theme preset coverage is concentrated in `ThemePresetCatalogTests`, `SettingsToolViewModelTests`, `AvaloniaThemeApplicationServiceTests`, and `SettingsToolHeadlessTests`. The Headless workflow switches representative light and dark presets. It verifies the live palette preview, application variant, semantic resources, and DataGrid column-header brushes.

Editing-preference coverage for delete-rows timing and frame display is in `ChapterEditingServiceTests` at the Core level and `SettingsToolViewModelTests` for draft/apply lifecycle.

Imported theme resource coverage is in `AvaloniaThemeApplicationServiceTests`. The tests resolve representative theme brushes and the configured monospace font through the runtime resource tree. They verify every imported `Color.*` token for a dark preset. Headless workflow tests verify visible `Optris.Icons.Avalonia.FontAwesome` icons.

Log projection coverage is in `LogToolViewModelTests` against `LogEntryViewModel`; log orchestration remains in `LogToolViewModel`. Log user-interface behavior is in `AuxiliaryToolHeadlessTests`. These tests verify the list-first default, explicit details actions, selection retention, filtering and search, copy and clear actions, live append and bounded eviction, structured and raw disclosures, theme changes, localization, and narrow replacement layout.

The settings Headless workflows verify the footer settings-folder action, including its left-side placement, accessible label, and routed shell target.

Font settings coverage is concentrated in `ChapterToolSettingsFontTests`, `AvaloniaFontFamilyCatalogTests`, `AppCompositionRootFontTests`, `SettingsToolViewModelTests`, and `SettingsToolHeadlessTests`. Catalog and ViewModel tests verify active-language family display names without changing canonical identity. The Headless workflow selects UI and monospace families. It verifies per-family options, live semantic resources, normal/editor/preview/table-cell surfaces, UI-font table headers and order-shift labels, monospace order-shift input, accessible previews, Save and Discard outcomes, and icon visibility.

Headless tests share one Avalonia UI session in their test process. Close `Popup` and `ContextMenu` surfaces in `finally`. Dispose directly constructed `IDisposable` DataContexts. Use `MainWindowHeadlessTestHost` and `MainWindowHeadlessTestHost.CloseWindowAsync` so window disposal also detaches its content tree. Await a real initialization task for asynchronous startup. Do not use fixed delays or polling to infer completion.

`AvaloniaWindowService` also detaches the content tree when an auxiliary window closes. Tests that create this service must use `using` and must verify `Content == null` when they cover window cleanup. A closed window is not clean until its content tree and event subscriptions are released.

The diagnosis, timing comparisons, affected tests, and repeatable triage procedure are recorded in `docs/testing/headless-performance.md`.

## Quick Routing

- parsing or export semantics changed: start in `tests/ChapterTool.Core.Tests`
- external tool, settings, process, or platform boundary changed: start in `tests/ChapterTool.Infrastructure.Tests`
- CLI binding or workflow changed: start in `tests/ChapterTool.CommandLine.Tests`
- viewmodel, localization, or runtime UI orchestration changed: start in `tests/ChapterTool.Avalonia.Tests`
- XAML shell, rendered controls, or Headless interaction flows changed: start in `tests/ChapterTool.Avalonia.Headless.Tests`
- Node.js package or npm runtime packaging changed: start in `packages/chaptertool/test/chaptertool.test.ts` and `packages/chaptertool/test/api-loader.test.ts`

## Analyzer Report

`scripts/report-analyzers.py` collects compiler and analyzer diagnostics at build time. It does not run tests. It is faster than `scripts/test-coverage.py`, which runs every test assembly. It skips the browser-wasm projects because they add no C# metrics and their Emscripten builds are slow.

Usage:

- `python3 scripts/report-analyzers.py` prints all diagnostics from every project.
- `python3 scripts/report-analyzers.py -Prefix SA` prints only StyleCop diagnostics.
- `python3 scripts/report-analyzers.py -Rebuild -Prefix CA1502` prints cyclomatic complexity diagnostics above the threshold.

The script requires Python 3 and the .NET SDK. It builds each project in dependency order. It writes one SARIF file per project per target framework under `artifacts/analyzers/raw`. It merges these files into `artifacts/analyzers/analyzers.sarif`.

Options:

- `-Configuration <name>` sets the build configuration. The default is `Release`.
- `-Prefix <prefix>` keeps only diagnostics whose rule ID starts with the prefix. Examples: `SA`, `CA1502`.
- `-Rebuild` forces a full rebuild. Use it when the report is empty or stale. The analyzer does not rerun for up-to-date projects.
- `-NoRestore` skips the restore step.
- `-Output <path>` writes the merged report to a custom path. The default is `artifacts/analyzers/analyzers.sarif`.

Incremental behavior: on an unchanged tree, a run without `-Rebuild` skips every project and reports nothing. This run is fast. Use `-Rebuild` for a complete report.

Cyclomatic complexity (CA1502): the threshold is 10, set in `CodeMetricsConfig.txt` at the repository root. The analyzer reports methods whose complexity is 11 or higher. `CA1502` is a suggestion in `.editorconfig`, so it never fails the build. Each CA1502 result carries the source file, line, column, and the enclosing type (class, struct, interface, record, or enum). The script derives the file path and the type from the source code because the compiler writes CA1502 without a location.

Per-target-framework reports: `Directory.Build.targets` sets a separate `ErrorLog` file for each target framework. Parallel compilers of a multi-target project cannot corrupt a shared file. The parser also accepts several concatenated JSON documents, so combined files do not break the report.

## Distribution Verification

Coverage entry point:

- `scripts/test-coverage.py` builds the five test projects and runs their assemblies through VSTest in sequence. This keeps Coverlet collection compatible with the Microsoft.Testing.Platform SDK setting. `scripts/coverage.runsettings` configures Coverlet collection. The script excludes generated `*.g.cs` files. It writes XML and HTML output under `artifacts/coverage`.

- Maintained publish entry points:
  - `scripts/publish.sh`
  - `scripts/publish.ps1`
  - `.github/workflows/dotnet-ci.yml`
  - `.github/workflows/nuget-publish.yml`
  - `.github/workflows/npm-publish.yml`
  - `.github/workflows/release.yml`
- The `ChapterTool` NuGet package installs the `chaptertool` command. `src/ChapterTool.CommandLine/ChapterTool.CommandLine.csproj` owns its package metadata.
- `.github/workflows/dotnet-ci.yml` packs `ChapterTool.Core` and `ChapterTool` in the build job. It uploads the packages as `ChapterTool-Core-nuget` and `ChapterTool-Cli-nuget`. Each `pack-dotnet` runtime matrix job uploads one `ChapterTool-Avalonia-<runtime>` artifact.
- The Avalonia publish scripts omit symbols, documentation files, and development diagnostics. They reject duplicate top-level assemblies in single-file output. The `pack-dotnet` jobs run this validation before artifact upload.
- `.github/workflows/nuget-publish.yml` applies one release version to both NuGet packages and publishes them. It does not install the CLI package during CI.
- `.github/workflows/release.yml` creates the GitHub Release after a successful `.NET 10 CI` push to a version tag. It publishes the artifacts from that CI run. A manual `workflow_dispatch` may select a tag that already has a successful CI run.
- Use `src/ChapterTool.CommandLine/README.md` for the NuGet Tool installation and external-tool requirements.
- The legacy Windows NSIS installer inputs are retired. Future installer work should consume the `src/ChapterTool.Avalonia` publish output and derive version metadata from `Directory.Build.props`.
