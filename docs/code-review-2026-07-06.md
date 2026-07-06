# Code Review - 2026-07-06

## Scope

This review inspected the current `src/` and `tests/` code for:

- fake or incomplete stub behavior exposed as real functionality
- low-value or misleading tests
- unusual compatibility or platform handling
- deviations from the repository guidance and common .NET/Avalonia practices

Four subagents reviewed Core, Infrastructure, Avalonia/UI, and cross-repository suspicious patterns in parallel. The review was read-only; no tests were run.

Existing working tree note: `scripts/publish.sh` was already modified before this review and was not touched.

## Summary

No critical issues were found. The highest-risk findings are:

- WebVTT import drops cue end times and calculates duration incorrectly.
- Windows terminal fallback in `ShellService` builds a command string from a path.
- File association support exists as a partial service surface but is not complete enough to be trustworthy.
- Preview UI is available when no chapter data exists and opens an empty window.

## High Severity

### WebVTT cue end times are parsed but discarded

- File: `src/ChapterTool.Core/Importing/Text/WebVttChapterImporter.cs:40`
- File: `src/ChapterTool.Core/Importing/Text/WebVttChapterImporter.cs:48`
- File: `src/ChapterTool.Core/Importing/Text/WebVttChapterImporter.cs:62`

The importer validates the cue end time with `TimeSpan.TryParse(parts[1], out _)`, but the parsed value is discarded. `Chapter.End` is never populated, and `ChapterInfo.Duration` is set to the last chapter start time.

Impact: imported WebVTT chapters lose end times. Any later export or duration-dependent workflow can produce wrong final segment timing.

Recommendation: keep the parsed end value, pass it as `End` when creating `Chapter`, and calculate duration from the last valid cue end.

### Windows terminal fallback is command-injection prone

- File: `src/ChapterTool.Infrastructure/Platform/ShellService.cs:62`

The Windows fallback path builds a command string:

```text
cmd /c start cmd /k "cd /d {directoryPath}"
```

Because `directoryPath` is user/path data, characters such as `&` or quotes can change the command. Exceptions around this path are also swallowed.

Impact: malformed or adversarial paths can execute unintended shell commands; failures are invisible to the user.

Recommendation: avoid command-string composition. Start `cmd.exe` directly with fixed arguments and set `ProcessStartInfo.WorkingDirectory = directoryPath`, or use a safer platform abstraction that returns a structured failure.

### File association service is incomplete and not wired to the documented command surface

- File: `src/ChapterTool.Infrastructure/Platform/WindowsFileAssociationService.cs:27`
- File: `src/ChapterTool.Infrastructure/Platform/WindowsFileAssociationService.cs:60`
- File: `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs:520`
- Related spec: `openspec/specs/avalonia-ui-shell/spec.md:60`

`WindowsFileAssociationService` writes only the ProgID description and extension default value. It does not write `shell\open\command`, icon metadata, ownership markers, or shell change notifications. `UnregisterAsync` deletes the extension key without confirming it is still owned by ChapterTool.

The spec says the main window ViewModel shall expose a file association command, but the current command list has no such command. The composition root can create the service, but no user-visible workflow appears to consume it.

Impact: registration can report success without making files open correctly, and unregister can remove another app's association. The implementation also looks like a feature but lacks an end-to-end user path.

Recommendation: either complete the service and UI workflow, or remove/hide the feature surface until it is ready. A complete implementation should write an open command, track ownership, guard unregistration, refresh shell associations, and have tests through an injectable registry abstraction.

### Preview opens an empty window with no loaded chapters

- File: `src/ChapterTool.Avalonia/Views/MainWindow.axaml:260`
- File: `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs:148`
- File: `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs:1304`

The preview command is an unconditional `WindowCommand("preview")`. With no current chapter data, `BuildPreview()` returns empty text, so the user can open a blank preview window.

Impact: this is a visible UI stub: the control appears usable but has no meaningful behavior in the empty state. Keyboard access such as `F11` can trigger the same result.

Recommendation: make `PreviewCommand.CanExecute` depend on loaded chapter data, and ensure buttons, menus, and shortcuts share that state. Alternatively, show an explicit empty-state message.

## Medium Severity

### `CueChapterImporter` has a fake injectable parser

- File: `src/ChapterTool.Core/Importing/Cue/CueChapterImporter.cs:3`
- File: `src/ChapterTool.Core/Importing/Cue/CueChapterImporter.cs:29`

The constructor accepts `CueSheetParser? parser` and stores it, but `ImportAsync` calls the static `CueSheetParser.Parse` method.

Impact: callers and tests may think parser behavior is replaceable, but the injected object is ignored.

Recommendation: remove the constructor parameter and field, or introduce a real parser interface/instance method and use it.

### `IfoChapterImporter` is fake-async and ignores request content

- File: `src/ChapterTool.Core/Importing/Disc/IfoChapterImporter.cs:16`
- File: `src/ChapterTool.Core/Importing/Disc/IfoChapterImporter.cs:18`
- File: `src/ChapterTool.Core/Importing/Disc/IfoChapterImporter.cs:21`

`ImportAsync` awaits `Task.CompletedTask`, then reads only from `request.Path`. It ignores `request.Content`.

Impact: IFO behaves differently from other importers that support in-memory content. The fake await also obscures the synchronous file-only behavior.

Recommendation: parse from a seekable stream and prefer `request.Content` when present. If file-only sync parsing is intentional for now, remove the fake await and document the limitation.

### Core tests depend on Infrastructure

- File: `tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj:30`
- File: `tests/ChapterTool.Core.Tests/Importing/MediaChapterImporterTests.cs:4`

`ChapterTool.Core.Tests` references `ChapterTool.Infrastructure` and instantiates `FfprobeMediaChapterReader`.

Impact: Core tests become mixed integration tests and can fail because of Infrastructure parser behavior rather than Core importer behavior.

Recommendation: test Core's `MediaChapterImporter` using a fake `IMediaChapterReader` that returns `MediaChapterEntry` values. Keep ffprobe JSON/process parsing tests in `ChapterTool.Infrastructure.Tests`.

### External tool locator treats any file as an executable

- File: `src/ChapterTool.Infrastructure/Tools/ExternalToolLocator.cs:37`
- File: `tests/ChapterTool.Infrastructure.Tests/ExternalToolLocatorTests.cs:15`

Tool resolution uses `File.Exists` only, and tests use empty text files as successful tool candidates.

Impact: on Linux/macOS, non-executable files can be reported as found. The settings UI can show success, then runtime execution fails later.

Recommendation: add executable validation. On Unix-like systems, check execute permissions. On Windows, validate expected executable naming, and consider an optional lightweight `--version` probe for configured tools.

### Corrupt settings silently reset to defaults

- File: `src/ChapterTool.Infrastructure/Configuration/AppSettingsStore.cs:43`
- File: `src/ChapterTool.Infrastructure/Configuration/ThemeSettingsStore.cs:29`

Invalid JSON is caught and replaced with default settings without logging or surfacing a diagnostic.

Impact: user settings can appear to reset, and a later save can overwrite the corrupt file with defaults, making recovery harder.

Recommendation: log the parse failure, preserve the corrupt file as `.corrupt`, and expose a user-visible warning before defaults are saved back.

### Shell failures are silently swallowed

- File: `src/ChapterTool.Infrastructure/Platform/ShellService.cs:41`
- File: `src/ChapterTool.Infrastructure/Platform/ShellService.cs:74`
- File: `src/ChapterTool.Infrastructure/Platform/ShellService.cs:105`

Reveal/open-terminal helper paths catch broad exceptions and ignore them.

Impact: users see an action that does nothing, with no status or log entry.

Recommendation: catch expected process/platform exceptions and return or log structured failure information.

### FFmpeg directory setting actually validates ffprobe

- File: `src/ChapterTool.Avalonia/ViewModels/SettingsToolViewModel.cs:248`
- File: `src/ChapterTool.Avalonia/ViewModels/SettingsToolViewModel.cs:255`
- File: `src/ChapterTool.Avalonia/ViewModels/SettingsToolViewModel.cs:577`
- File: `src/ChapterTool.Avalonia/ViewModels/SettingsToolViewModel.cs:593`

`FfmpegPath` is presented as an FFmpeg directory setting, but validation and discovery check `ffprobe`.

Impact: users may think they configured FFmpeg, while the app actually validates ffprobe. The setting overlaps semantically with `FfprobePath`.

Recommendation: rename it to ffprobe directory if that is the intended behavior, or validate/discover `ffmpeg` if it truly represents FFmpeg.

### Native file picker strings are hard-coded English

- File: `src/ChapterTool.Avalonia/Services/AvaloniaFilePickerService.cs:11`
- File: `src/ChapterTool.Avalonia/Services/AvaloniaFilePickerService.cs:16`
- File: `src/ChapterTool.Avalonia/Services/AvaloniaFilePickerService.cs:37`
- File: `src/ChapterTool.Avalonia/Services/AvaloniaFilePickerService.cs:54`
- File: `src/ChapterTool.Avalonia/Services/AvaloniaFilePickerService.cs:71`

File picker titles and file type labels are English string literals.

Impact: localized UI still opens English system dialogs.

Recommendation: inject `IAppLocalizer` into `AvaloniaFilePickerService`, or have callers pass localized titles/filter names.

### Icon buttons lack accessible names

- File: `src/ChapterTool.Avalonia/Views/MainWindow.axaml:260`
- File: `src/ChapterTool.Avalonia/Views/MainWindow.axaml:267`
- File: `src/ChapterTool.Avalonia/Views/MainWindow.axaml:275`
- File: `src/ChapterTool.Avalonia/Views/MainWindow.axaml:505`
- File: `src/ChapterTool.Avalonia/Views/Tools/SettingsToolView.axaml:101`

Several icon buttons have tooltip text and automation IDs, but no localized `AutomationProperties.Name`.

Impact: screen readers may not announce a useful action name. Automation IDs are not a substitute for accessible names.

Recommendation: add localized `AutomationProperties.Name` and, where useful, `AutomationProperties.HelpText`. Headless tests should verify the accessible name, not only command binding.

### BDMV parser moves data through diagnostics and narrow regexes

- File: `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvChapterImporter.cs:160`
- File: `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvChapterImporter.cs:344`

The BDMV importer stores eac3to stdout in a diagnostic message and then parses it later with narrow text regexes.

Impact: adding another diagnostic can break assumptions, and small eac3to output/version/localization changes can stop detection.

Recommendation: return an internal structured result that carries stdout separately from diagnostics. Add fixture coverage from real eac3to outputs and handle known format variants.

## Low Severity

### Test fakes live in production Infrastructure

- File: `src/ChapterTool.Infrastructure/Platform/MemoryClipboardService.cs:5`
- File: `src/ChapterTool.Infrastructure/Platform/ScriptedDialogService.cs:5`
- File: `src/ChapterTool.Infrastructure/Platform/RecordingWindowService.cs:5`
- File: `tests/ChapterTool.Infrastructure.Tests/PlatformServiceTests.cs:63`

These are test-double style services in the production Infrastructure assembly. The test name calls them skeletons.

Impact: they can be accidentally injected into production paths and silently record operations instead of performing real platform behavior.

Recommendation: move them to test projects, or mark them internal/test-only and avoid exposing them as production services.

### `UiCommandTests` waits with a fixed delay

- File: `tests/ChapterTool.Avalonia.Tests/Commands/UiCommandTests.cs:47`

The test waits for `async void` command completion with `Task.Delay(50)`.

Impact: slow CI machines or scheduling delays can make the test flaky.

Recommendation: use a `TaskCompletionSource`, property changed event, or polling with a bounded timeout for `ExecutionError`.

### Matroska integration setup blocks on async

- File: `tests/ChapterTool.Infrastructure.Tests/Importing/MatroskaIntegrationTests.cs:27`

`IAsyncLifetime.InitializeAsync` uses `.AsTask().Result`.

Impact: this is a sync-over-async pattern with deadlock and cancellation risks.

Recommendation: make `InitializeAsync` an `async ValueTask` and `await LocateAsync(...)`.

### Screenshot tests are weak regression guards

- File: `tests/ChapterTool.Avalonia.Tests/Headless/MainWindowHeadlessTests.cs:190`
- File: `tests/ChapterTool.Avalonia.Tests/Headless/LocalizationAndLayoutHeadlessTests.cs:117`

The tests mainly capture screenshots and assert the files exist/non-empty.

Impact: a badly misaligned or semantically blank UI can still pass.

Recommendation: keep screenshots as artifacts, but add behavioral/layout assertions such as key controls visible, no bounds overflow, and meaningful non-background pixel regions.

### MKVToolNix registry DisplayIcon parsing misses quoted paths

- File: `src/ChapterTool.Infrastructure/Tools/MkvToolNixInstallProbe.cs:78`

The install probe trims a trailing comma suffix but does not normalize quoted `DisplayIcon` values.

Impact: common registry values such as `"C:\Program Files\...\mkvtoolnix-gui.exe",0` may fail to resolve to the install directory.

Recommendation: strip quotes after trimming the icon suffix, then take the directory. Add a quoted DisplayIcon test.

### eac3to export can show a console window

- File: `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvChapterImporter.cs:171`
- File: `tests/ChapterTool.Infrastructure.Tests/BdmvChapterImporterTests.cs:51`

The eac3to chapter export request explicitly sets `CreateNoWindow: false`, and the test locks in that behavior.

Impact: GUI import can flash or show a console window.

Recommendation: default to hidden process windows unless eac3to requires visibility. If visibility is required, document the reason and test that reason rather than the raw flag.

## Non-Issues / Filtered Results

The review did not find meaningful hits for:

- `NotImplementedException`
- `PlatformNotSupportedException`
- `Thread.Sleep`
- global test parallelization disablement in Avalonia tests
- tests reading source/configuration files and asserting incidental source strings

