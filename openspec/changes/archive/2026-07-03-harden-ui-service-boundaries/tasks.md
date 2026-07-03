## 1. Evented Application Log Service

- [x] 1.1 Extend the UI log service contract with a structured new-entry notification while preserving snapshot history, formatting, and clear behavior.
- [x] 1.2 Update `ApplicationLogPanelProvider` to raise notifications only for entries accepted by the configured level filter and retained by the bounded buffer.
- [x] 1.3 Add or update Infrastructure tests for history replay, new-entry delivery, capacity trimming, severity filtering, structured state retention, and clear-then-continue behavior.
- [x] 1.4 Update log-window or tool-view behavior so live logs append from service notifications and existing logs still render when the window opens.
- [x] 1.5 Verify log entries continue to format through active localization resources and preserve technical details after language changes.

## 2. Main-Window ViewModel and Service Boundaries

- [x] 2.1 Audit `MainWindowViewModel` constructor and command paths to confirm file picking, windows, shell, clipboard, prompts, settings, and platform actions remain behind services or command parameters.
- [x] 2.2 Move routine state currently synchronized by `MainWindow.Refresh()` into observable ViewModel properties and command availability notifications.
- [x] 2.3 Bind combine checked state, clip selection, save availability, related-media availability, and auxiliary command enabled state directly to ViewModel state or commands.
- [x] 2.4 Keep code-behind only for view adaptation: file picker invocation, drag/drop path extraction, DataGrid edit event mapping, shortcut adaptation, and responsive layout adjustments.
- [x] 2.5 Add or update ViewModel and headless UI tests proving state changes update visible controls without calling a routine manual refresh method.

## 3. Async Load and Import Routing

- [x] 3.1 Add a deterministic async load test that completes after an await boundary and verifies `Rows`, `ClipOptions`, selected clip state, status text, and progress update through `MainWindowViewModel`.
- [x] 3.2 Ensure importer progress callbacks update ViewModel-owned progress/status state without direct control mutation.
- [x] 3.3 Add or update tests covering importer registry resolution for existing extensions and the fallback cases called out in the current specs.
- [x] 3.4 Verify `MainWindowViewModel` and `MainWindow` continue to call load services without constructing importers or branching on source extensions.
- [x] 3.5 Ensure fallback diagnostics for ffprobe, mkvextract, FLAC embedded CUE, and unsupported sources are returned or logged structurally.

## 4. Prompt and Notification Boundaries

- [x] 4.1 Verify Core services return structured results or diagnostics instead of displaying UI prompts or publishing static notification events.
- [x] 4.2 Verify ViewModels route user-facing prompts, tool windows, clipboard operations, related-media launch, file association, and unsupported-platform feedback through explicit services.
- [x] 4.3 Add focused tests for prompt/window service substitution where boundary behavior is not already covered.

## 5. Validation

- [x] 5.1 Run `openspec validate "harden-ui-service-boundaries" --strict`.
- [x] 5.2 Run `dotnet test tests/ChapterTool.Infrastructure.Tests/ChapterTool.Infrastructure.Tests.csproj --no-restore`.
- [x] 5.3 Run `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore`.
- [x] 5.4 Run `dotnet test ChapterTool.Avalonia.slnx --no-restore`.
