# Infrastructure Code Map

`src/ChapterTool.Infrastructure` owns process execution, external tool discovery, settings persistence, filesystem/platform integration, and import adapters that depend on native tools or container libraries.

## Ownership

### Media and tool-backed import adapters

- ffprobe-backed media chapters:
  - `src/ChapterTool.Infrastructure/Importing/Media/FfprobeMediaChapterReader.cs`
- ATL-backed MP4 chapters:
  - `src/ChapterTool.Infrastructure/Importing/Media/AtlMp4ChapterReader.cs`
- Matroska chapter extraction:
  - `src/ChapterTool.Infrastructure/Importing/Matroska/MatroskaChapterImporter.cs`
- BDMV / eac3to path:
  - `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvChapterImporter.cs`

### External tool discovery and process execution

- tool lookup:
  - `src/ChapterTool.Infrastructure/Tools/ExternalToolLocator.cs`
  - `src/ChapterTool.Infrastructure/Tools/ExternalToolPathResolver.cs`
  - `src/ChapterTool.Infrastructure/Tools/MkvToolNixInstallProbe.cs`
- process execution:
  - `src/ChapterTool.Infrastructure/Processes/ProcessRunner.cs`
- service contracts:
  - `src/ChapterTool.Infrastructure/Services/IExternalToolLocator.cs`
  - `src/ChapterTool.Infrastructure/Services/IProcessRunner.cs`
  - `src/ChapterTool.Infrastructure/Services/ProcessRunRequest.cs`
  - `src/ChapterTool.Infrastructure/Services/ProcessRunResult.cs`
  - `src/ChapterTool.Infrastructure/Services/ExternalToolLocation.cs`

### Settings and configuration persistence

- schema:
  - `src/ChapterTool.Infrastructure/Configuration/ChapterToolSettings.cs`
  - `src/ChapterTool.Infrastructure/Configuration/AppSettings.cs`
  - `src/ChapterTool.Infrastructure/Configuration/FontSettings.cs`
  - `src/ChapterTool.Infrastructure/Configuration/ThemeSettings.cs`
  - `src/ChapterTool.Infrastructure/Configuration/ThemePresetCatalog.cs`
- storage:
  - `src/ChapterTool.Infrastructure/Configuration/ChapterToolSettingsStore.cs`
- contracts:
  - `src/ChapterTool.Infrastructure/Services/ISettingsStore.cs`
- corrupt-file handling:
  - `src/ChapterTool.Infrastructure/Configuration/CorruptSettingsFile.cs`
  - `src/ChapterTool.Infrastructure/Configuration/CorruptSettingsFileException.cs`
  - `src/ChapterTool.Infrastructure/Configuration/UnsupportedSettingsVersionException.cs`
- JSON source generation:
  - `src/ChapterTool.Infrastructure/Configuration/AppJsonSerializerContext.cs`

`ChapterToolSettingsStore` is the only settings persistence implementation and implements `ISettingsStore<ChapterToolSettings>`. It persists schema-versioned `application`, `theme`, and `font` child content in `settings.json`, serializes atomic read-modify-write operations by canonical path, upgrades known older unified shapes, rejects future versions without overwriting them, and ignores predecessor settings files when the unified document is absent.

All runtime consumers receive the same aggregate store. `SettingsToolViewModel` loads the document once and saves all changed child content with one aggregate write; isolated changes use `UpdateAsync` for one lock-scoped read-modify-write. The store caches the normalized aggregate by file timestamp and length, so unchanged subsequent loads do not reopen or reparse JSON. New settings areas should extend `ChapterToolSettings` and add a schema upgrade when the persisted structure changes instead of adding a store or file.

`ThemeSettings` persists only a stable built-in preset id in the unified `theme` section. `ThemePresetCatalog` owns preset identity, light/dark base variants, semantic palettes, preview swatches, and default fallback; the legacy `theme-colors.json` file remains intentionally ignored.

`FontSettings` persists independent canonical UI and monospace family names in the unified `font` section. Empty values are stable category defaults; normalization never needs an Avalonia dependency or system-font lookup.

### Platform services

- shell/OS launch behavior:
  - `src/ChapterTool.Infrastructure/Platform/ShellService.cs`
- native dependency lookup:
  - `src/ChapterTool.Infrastructure/Platform/FileSystemNativeDependencyService.cs`
  - `src/ChapterTool.Infrastructure/Platform/INativeDependencyService.cs`
- app log surface:
  - `src/ChapterTool.Infrastructure/Platform/ApplicationLogPanelProvider.cs`
- test/dummy platform services:
  - `src/ChapterTool.Infrastructure/Platform/MemoryClipboardService.cs`
  - `src/ChapterTool.Infrastructure/Platform/ScriptedDialogService.cs`
  - `src/ChapterTool.Infrastructure/Platform/RecordingWindowService.cs`

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

### BDMV / eac3to issues

Start with:

- `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvChapterImporter.cs`

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
