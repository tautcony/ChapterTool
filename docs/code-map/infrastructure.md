# Infrastructure Code Map

`src/ChapterTool.Infrastructure` owns process execution, external tool discovery, settings persistence, filesystem/platform integration, and import adapters that depend on native tools or container libraries. Host-neutral settings models and platform contracts live in `src/ChapterTool.Contracts`.

Use ASD-STE100 principles in this document. Keep each sentence short and direct. Keep code identifiers exact.

## Ownership

### Media and tool-backed import adapters

- ffprobe-backed media chapters:
  - `src/ChapterTool.Infrastructure/Importing/Media/FfprobeMediaChapterReader.cs`
- ATL-backed MP4 chapters:
  - `src/ChapterTool.Infrastructure/Importing/Media/AtlMp4ChapterReader.cs`
- Matroska chapter extraction:
  - `src/ChapterTool.Infrastructure/Importing/Matroska/MatroskaChapterImporter.cs`
- BDMV path:
  - `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvImporter.cs`: managed BDMV directory importer
  - `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvMetadataReader.cs`: optional disc title reader
  - `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvSourceLayout.cs`: accepted input shapes and primary/backup paths
  - `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvPlaylistScanner.cs`: bounded playlist discovery and parity filtering

`BdmvImporter` preserves global INDEX title numbers, skips prohibited titles, reports hidden titles, and consumes bounded HDMV navigation evidence.

### Runtime importer composition

- registry contract:
  - `src/ChapterTool.Infrastructure/Importing/Runtime/IChapterImporterRegistry.cs`
- runtime routing and fallback:
  - `src/ChapterTool.Infrastructure/Importing/Runtime/RuntimeChapterImporterRegistry.cs`
- shared host factories and policies:
  - `src/ChapterTool.Infrastructure/Importing/Runtime/ChapterToolRuntimeComposition.cs`

`ChapterToolRuntimeComposition` owns the default settings directory, `PATH` search directories, external-tool locator, process runner, media readers, importer registry, and CLI export service construction. Avalonia can pass existing GUI services to preserve their lifetime.

### External tool discovery and process execution

- tool lookup:
  - `src/ChapterTool.Infrastructure/Tools/ExternalToolLocator.cs`
  - `src/ChapterTool.Infrastructure/Tools/ExternalToolPathResolver.cs`
  - `src/ChapterTool.Infrastructure/Tools/ExternalToolDefaultCandidateProvider.cs`
  - `src/ChapterTool.Infrastructure/Tools/IExternalToolDefaultCandidateProvider.cs`
  - `src/ChapterTool.Infrastructure/Tools/MkvToolNixInstallProbe.cs`
- process execution:
  - `src/ChapterTool.Infrastructure/Processes/ProcessRunner.cs`
- service contracts:
  - `src/ChapterTool.Contracts/PlatformPorts/IExternalToolLocator.cs`
  - `src/ChapterTool.Infrastructure/Services/IProcessRunner.cs`
  - `src/ChapterTool.Infrastructure/Services/ProcessRunRequest.cs`
  - `src/ChapterTool.Infrastructure/Services/ProcessRunResult.cs`
  - `src/ChapterTool.Infrastructure/Services/IDialogService.cs`
  - `src/ChapterTool.Infrastructure/Services/DialogRequest.cs`
  - `src/ChapterTool.Contracts/PlatformPorts/ExternalToolLocation.cs`

`ExternalToolDefaultCandidateProvider` supplies the default executable candidate list for a tool id. `IDialogService` and `DialogRequest` define the host-neutral message dialog boundary that `ScriptedDialogService` implements for tests. `MkvToolNixInstallProbe` selects a platform-specific probe. `WindowsRegistryInstallProbe` reads the Windows registry and returns no values on other platforms.

### Settings and configuration persistence

- schema:
  - `src/ChapterTool.Contracts/Configuration/ChapterToolSettings.cs`
  - `src/ChapterTool.Contracts/Configuration/AppSettings.cs`
  - `src/ChapterTool.Contracts/Configuration/FontSettings.cs`
  - `src/ChapterTool.Contracts/Configuration/ThemeSettings.cs`
  - `src/ChapterTool.Contracts/Configuration/ThemePresetCatalog.cs`
- storage:
  - `src/ChapterTool.Infrastructure/Configuration/ChapterToolSettingsStore.cs`
- contracts:
  - `src/ChapterTool.Contracts/PlatformPorts/ISettingsStore.cs`
- corrupt-file handling:
  - `src/ChapterTool.Infrastructure/Configuration/CorruptSettingsFile.cs`
  - `src/ChapterTool.Contracts/Configuration/CorruptSettingsFileException.cs`
  - `src/ChapterTool.Infrastructure/Configuration/UnsupportedSettingsVersionException.cs`
- JSON source generation:
  - `src/ChapterTool.Infrastructure/Configuration/AppJsonSerializerContext.cs`

`ChapterToolSettingsStore` is the only settings persistence implementation. It implements `ISettingsStore<ChapterToolSettings>`. It stores schema-versioned `application`, `theme`, and `font` content in `settings.json`. It serializes atomic read-modify-write operations by canonical path. It upgrades known older unified shapes. It rejects future versions without overwriting them. It ignores predecessor settings files when the unified document is absent.

All runtime consumers receive the same aggregate store. `SettingsToolViewModel` loads the document once. It saves changed child content with one aggregate write. Isolated changes use `UpdateAsync` for one lock-scoped read-modify-write. The store caches the normalized aggregate by file timestamp and length. Unchanged loads do not reopen or reparse JSON. Extend `ChapterToolSettings` for a new settings area. Add a schema upgrade when the persisted structure changes. Do not add another store or file.

`ThemeSettings` persists only a stable built-in preset id in the unified `theme` section. `ThemePresetCatalog` owns preset identity, light/dark base variants, semantic palettes, preview swatches, and default fallback; the legacy `theme-colors.json` file remains intentionally ignored.

`FontSettings` persists independent canonical UI and monospace family names in the unified `font` section. Empty values are stable category defaults; normalization never needs an Avalonia dependency or system-font lookup.

`AppSettings.OutputTextEncoding` persists the lowercase output encoding id (`utf8`, `utf16le`, `utf16be`, `utf32le`, or `utf32be`); UTF-8 is the default.

`AppSettings` also persists the editing preferences: `DeleteRowsTimingMode` (`preserve` or `normalize`), `FrameDisplayMode` (`round` or `decimal-places`), and `FrameDecimalPlaces` (one to six).

### Platform services

- shell/OS launch behavior:
  - `src/ChapterTool.Infrastructure/Platform/ShellService.cs`
- native dependency lookup:
  - `src/ChapterTool.Infrastructure/Platform/FileSystemNativeDependencyService.cs`
  - `src/ChapterTool.Infrastructure/Platform/INativeDependencyService.cs`
  - `src/ChapterTool.Infrastructure/Platform/NativeDependencyLocation.cs`
- app log surface:
  - `src/ChapterTool.Infrastructure/Platform/ApplicationLogPanelProvider.cs`
- manual log export:
  - `src/ChapterTool.Infrastructure/Platform/ApplicationLogFileExporter.cs`
- test/dummy platform services:
  - `src/ChapterTool.Infrastructure/Platform/MemoryClipboardService.cs`
  - `src/ChapterTool.Infrastructure/Platform/ScriptedDialogService.cs`

`ApplicationLogPanelProvider` implements `IApplicationLogService` and `ILoggerProvider`. It captures entries at or above its configured minimum level. It keeps a bounded in-memory snapshot in append order. `Entries` returns a copy of the snapshot. `EntryAdded` fires after an entry is retained. `Cleared` fires after the snapshot is empty. The provider has no grouping, timestamp index, or older-page cursor.

`ApplicationLogFileExporter` implements the optional `IApplicationLogExporter` contract. It creates `settings/logs/` below the host settings directory. It writes the requested entries as UTF-8 JSON or RFC 4180 CSV. It orders exported rows by timestamp and returns filesystem failures as `ApplicationLogExportResult` values.

The desktop `LoggingModule` registers the provider and exporter as application singletons. It also keeps the Serilog rolling archive separate from the in-memory panel. The archive rolls daily, keeps up to 14 files, and rolls at 10 MB.

## Feature Lookup

### ffprobe import issues

Start with:

- `src/ChapterTool.Infrastructure/Importing/Media/FfprobeMediaChapterReader.cs`

Then inspect:

- `src/ChapterTool.Infrastructure/Tools/ExternalToolLocator.cs`
- `src/ChapterTool.Infrastructure/Processes/ProcessRunner.cs`

### MP4 embedded chapter issues

Start with:

- `src/ChapterTool.Infrastructure/Importing/Media/AtlMp4ChapterReader.cs`

### MKV / mkvextract issues

Start with:

- `src/ChapterTool.Infrastructure/Importing/Matroska/MatroskaChapterImporter.cs`

Then inspect:

- `src/ChapterTool.Infrastructure/Tools/ExternalToolLocator.cs`
- `src/ChapterTool.Infrastructure/Tools/MkvToolNixInstallProbe.cs`

### BDMV import issues

Start with:

- `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvImporter.cs`

Use `BdmvSourceLayout.cs` for input-shape or backup-directory issues. Use `BdmvPlaylistScanner.cs` for candidate order, duplicate filtering, or no-chapter diagnostics. Use `BdmvImporter.cs` for managed navigation and playlist composition.

### External tool path resolution

Start with:

- `src/ChapterTool.Infrastructure/Tools/ExternalToolLocator.cs`

Use `ExternalToolPathResolver.cs` when path expansion, executable name, or default candidate rules are involved.

### Settings persistence and corruption handling

Start with:

- `src/ChapterTool.Infrastructure/Configuration/ChapterToolSettingsStore.cs`
- `src/ChapterTool.Infrastructure/Configuration/CorruptSettingsFile.cs`

### Shell, terminal, file reveal, and app log issues

Start with:

- `src/ChapterTool.Infrastructure/Platform/ShellService.cs`
- `src/ChapterTool.Infrastructure/Platform/ApplicationLogPanelProvider.cs`
- `src/ChapterTool.Infrastructure/Platform/ApplicationLogFileExporter.cs`
