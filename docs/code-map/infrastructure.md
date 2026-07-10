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
  - `src/ChapterTool.Infrastructure/Configuration/AppSettings.cs`
  - `src/ChapterTool.Infrastructure/Configuration/ThemeSettings.cs`
  - `src/ChapterTool.Infrastructure/Configuration/ThemePresetCatalog.cs`
- storage:
  - `src/ChapterTool.Infrastructure/Configuration/AppSettingsStore.cs`
  - `src/ChapterTool.Infrastructure/Configuration/ThemeSettingsStore.cs`
- corrupt-file handling:
  - `src/ChapterTool.Infrastructure/Configuration/CorruptSettingsFile.cs`
  - `src/ChapterTool.Infrastructure/Configuration/CorruptSettingsFileException.cs`
- JSON source generation:
  - `src/ChapterTool.Infrastructure/Configuration/AppJsonSerializerContext.cs`

`AppSettings` stores runtime-safe output defaults including save format, XML language, UTF-8 BOM emission, and frame-accuracy tolerance; the Avalonia settings tool applies these live and persists them through `AppSettingsStore`.

`ThemeSettings` persists only a stable built-in preset id in `theme-settings.json`. `ThemePresetCatalog` owns preset identity, light/dark base variants, semantic palettes, preview swatches, and default fallback; the legacy `theme-colors.json` file is intentionally ignored.

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

- `src/ChapterTool.Infrastructure/Configuration/AppSettingsStore.cs`
- `src/ChapterTool.Infrastructure/Configuration/CorruptSettingsFile.cs`

### Shell, terminal, file reveal, and app log issues

Start with:

- `src/ChapterTool.Infrastructure/Platform/ShellService.cs`
- `src/ChapterTool.Infrastructure/Platform/ApplicationLogPanelProvider.cs`
