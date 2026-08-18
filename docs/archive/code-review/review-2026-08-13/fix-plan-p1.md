# P1 Fix Plan (review 2026-08-13)

Status: implemented.

This plan covers the next high-value medium-severity findings after the P0 batch. HOST-02, UI-02, CORE-13, and CORE-14 are included.

## Scope

| ID | File(s) | Fix |
|----|---------|-----|
| INFRA-02 | `src/ChapterTool.Infrastructure/Processes/ProcessRunner.cs` | After the child process exits, drain stdout and stderr with a bounded wait. Cancel the readers after `KillWaitTimeout`. Return captured text. Mark the result as truncated when the pipes do not reach EOF. |
| INFRA-03 | `src/ChapterTool.Infrastructure/Processes/ProcessRunner.cs` | `KillProcess` also catches `Win32Exception`, `AggregateException`, and `NotSupportedException`. The cancel and timeout paths still return a structured `ProcessRunResult`. |
| UI-03 | `src/ChapterTool.Avalonia.UI/ViewModels/MainWindowViewModel.cs`, `MainWindowViewModel.ImportExport.cs` | The chapter-name mode setter does not change state when the requested index already matches the current mode. Template load failure restores `AutoGenerateNames`. |
| UI-04 | `ExpressionToolViewModel.cs`, `LanguageToolViewModel.cs`, `ForwardShiftToolViewModel.cs`, `StandardToolCatalogFactory.cs` | Tool commands accept `ReportUnexpectedUiException`. `BrowseScriptAsync` catches IO-class failures and writes a status message. |
| HOST-01 | `src/ChapterTool.CommandLine/ChapterToolCliHost.cs` | The CLI host sets `Console.OutputEncoding` to UTF-8 without a BOM. The call is inside try/catch so unsupported consoles do not crash. |
| HOST-02 | `src/ChapterTool.Wasm/wwwroot/js/download.js`, `Pages/Home.razor`, `Services/WasmBrowserShortcutGuard.cs` | A capture-phase keydown guard calls `preventDefault` for F5/Ctrl+R always, and for Ctrl+S/O/L, F11, and F9 when the target is not an input. The Blazor handler still runs after the guard. |
| UI-02 | `src/ChapterTool.Avalonia.UI/ViewModels/SettingsToolViewModel.cs`, `StandardToolCatalogFactory.cs` | `LoadAsync` catches unexpected exceptions, marks the load as failed, writes `Status.UnexpectedError`, and reports through `ReportUnexpectedUiException`. |
| CORE-13 | `PortableInputPolicy.cs`, `PortableInputReader.cs`, CUE/TAK/IFO importers | Stream copies use the portable byte budget. Over-limit input returns `InputTooLarge`. |
| CORE-14 | `src/ChapterTool.Core/Importing/Text/XmlChapterImporter.cs` | `ParseAtom` stops at 64 nested `ChapterAtom` levels and returns `InvalidXml`. |

## Test plan

- `tests/ChapterTool.Infrastructure.Tests/ProcessRunnerTests.cs`: a parent process that exits while a grandchild keeps stdout open returns within the drain bound.
- `tests/ChapterTool.Avalonia.Tests/ViewModels/MainWindowViewModelTests.cs`: a failed template load keeps `AutoGenerateNames`; setting the current mode index is a no-op.
- `tests/ChapterTool.Avalonia.Tests/ViewModels/ToolWindowViewModelTests.cs`: a missing Lua script writes a load-failed status; tool commands keep the injected error handler.
- `tests/ChapterTool.CommandLine.Tests/Cli/ChapterToolCliApplicationTests.cs`: the host configures a UTF-8 console output encoding without a BOM.
- `tests/ChapterTool.Wasm.Tests/WasmBrowserShortcutGuardTests.cs`: reload keys always block the browser default. App shortcuts block only outside inputs.
- `tests/ChapterTool.Avalonia.Tests/ViewModels/SettingsToolViewModelTests.cs`: theme apply failure sets the failed state and invokes the error handler.
- `tests/ChapterTool.Core.Tests/Boundaries/PortableInputPolicyTests.cs`: a seekable stream over the budget is rejected without a full copy.
- `tests/ChapterTool.Core.Tests/Importing/CueImporterTests.cs` and `IfoImporterTests.cs`: stream import over the budget returns `InputTooLarge`.
- `tests/ChapterTool.Core.Tests/Importing/TextImporterTests.cs`: 65 nested `ChapterAtom` nodes fail. 64 nested nodes succeed.

## Out of scope

Remaining P2/P3 findings stay in the individual review reports.
