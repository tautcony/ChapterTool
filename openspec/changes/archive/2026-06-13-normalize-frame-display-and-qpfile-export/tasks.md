## 1. Core Frame State

- [x] 1.1 Add or expose a frame accuracy state that distinguishes accurate, inexact, and neutral/unrounded rows.
- [x] 1.2 Update frame-rate calculation so `FramesInfo` contains only numeric frame text.
- [x] 1.3 Update frame editing and expression projection paths so they preserve numeric frame text and recompute accuracy separately.
- [x] 1.4 Remove any parsing or generation of `K` and `*` suffixes from stored chapter frame data.

## 2. Export Semantics

- [x] 2.1 Keep a single QPFile export format and remove duplicate Chapter2Qpfile code, tests, UI entries, and specs.
- [x] 2.2 Update QPFile export to calculate integer frames from chapter time and selected frame rate rather than `FramesInfo`.
- [x] 2.3 Update celltimes and any other frame-number exporters to use the configured compatibility rounding policy.
- [x] 2.4 Add regression tests proving stale or marked frame display text does not affect QPFile output.

## 3. Avalonia Display

- [x] 3.1 Add row ViewModel properties for frame text and frame accuracy styling.
- [x] 3.2 Replace the frame DataGrid text column with a template that supports text outer glow styling.
- [x] 3.3 Render accurate rounded frames with green non-offset glow, inexact rounded frames with red non-offset glow, and unrounded frames with neutral black styling.
- [x] 3.4 Ensure frame cell editing still commits numeric values through the existing edit command path.
- [x] 3.5 Add a persisted frame accuracy tolerance setting and expose it in the settings panel.
- [x] 3.6 Apply the configured frame accuracy tolerance when refreshing frame display.

## 4. Verification

- [x] 4.1 Update Core tests that currently expect `K` or `*` inside `FramesInfo`.
- [x] 4.2 Update Avalonia ViewModel/UI tests for the single QPFile option and frame accuracy presentation state.
- [x] 4.3 Run `dotnet test tests\ChapterTool.Core.Tests\ChapterTool.Core.Tests.csproj --no-restore`.
- [x] 4.4 Run `dotnet test tests\ChapterTool.Avalonia.Tests\ChapterTool.Avalonia.Tests.csproj --no-restore`.
- [x] 4.5 Run `openspec validate "normalize-frame-display-and-qpfile-export" --strict`.
