# GUI Verification Plan

## Automated Checks

- `ExecutableLaunchTests.SmokeTestArgumentBuildsAvaloniaAppAndExits` runs `ChapterTool.Avalonia.exe --smoke-test` and verifies the Avalonia app bootstrap can be built.
- `ExecutableLaunchTests.ExecutableStartsMainWindowAndDoesNotExitImmediately` launches the real executable, waits briefly, asserts that the process is still running, then closes it.
- `MainWindowViewModelTests` verify unloaded state, command availability, load/save orchestration, shortcuts, clip selection, chapter row edits, and auxiliary window entry points without relying on controls.
- `MainWindowXamlTests` statically verify the real window declares the editable chapter grid, context menu, advanced options, progress indicator, append entry point, and stable automation ids for future GUI drivers.
- `RuntimeChapterLoadServiceTests` verify the GUI runtime routes `.mkv/.mka`, `.mp4/.m4a/.m4v`, and BDMV directories into their concrete importers instead of failing as unsupported sources.

Stable automation ids currently exposed by `MainWindow.axaml`:

- `SourcePath`, `LoadButton`, `ExportFormat`, `SaveButton`, `ProgressBar`
- `ClipSelectionPanel`, `ClipSelector`, `CombineButton`, `AppendMplsButton`
- `AdvancedOptions`, `XmlLanguage`, `AutoNames`, `ExpressionText`, `ApplyExpression`, `OrderShift`, `TemplateNames`, `RefreshRows`
- `ChapterGrid`
- `PreviewWindow`, `LogWindow`, `ColorSettingsWindow`, `LanguageWindow`, `ExpressionWindow`, `TemplateNamesWindow`, `ZonesWindow`, `ForwardShiftWindow`

Run:

```powershell
dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore
```

## Manual Runtime Checks

Before functional checks, compare the main window with the legacy screenshot and `Time_Shift/Forms/Form1.Designer.cs`:

- Light compact tool-window layout, not a dark app-shell header.
- Current source path visible as a compact top label.
- Large `Load` and `Save` buttons remain the primary first-row actions.
- Frame rounding, frame-rate selection, preview, and refresh are grouped near the upper-right/top workflow zone.
- The editable chapter grid occupies most of the window.
- Save format, XML language, naming, order shift, expression, and log controls are grouped in a dense bottom options panel.
- Optional actions stay in compact buttons or context menus and do not require Windows registry access for normal use.

1. Start `src/ChapterTool.Avalonia/bin/Debug/net10.0/ChapterTool.Avalonia.exe`.
2. Confirm the `ChapterTool` window remains open.
3. Enter a fixture path, for example `Time_Shift_Test/[ogm_Sample]/00001.txt`, and click `Load`.
4. Confirm chapter rows appear and status changes to loaded.
5. Select a save format and click `Save`; confirm the exported file is written beside the source file.
6. Repeat load checks for `.vtt`, `.cue`, `.mpls`, `.ifo`, `.xpl`, `.mkv/.mka`, `.mp4/.m4a/.m4v`, and a BDMV root directory. Missing native tools should produce a visible dependency diagnostic, not an unsupported-source error.

## Future GUI Automation

The current automated GUI check validates process lifetime. Deeper interaction automation should use one of:

- Avalonia headless tests for control-level interaction once views are stabilized.
- WinAppDriver/FlaUI-style process tests for packaged Windows artifacts using the automation ids listed above.
- ViewModel command tests for fast, deterministic workflow coverage.

The preferred split is: keep parsing/export/edit behavior in Core tests, platform and dependency behavior in Infrastructure tests, and control lifetime plus command wiring in Avalonia tests.
