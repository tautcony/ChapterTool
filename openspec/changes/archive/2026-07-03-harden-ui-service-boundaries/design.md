## Context

`docs/todo-list.md` is an architectural review checklist derived from legacy WinForms patterns. The current Avalonia/Core/Infrastructure solution already follows several of its recommendations:

- `MainWindowViewModel` is constructed with `IFilePickerService`, `IWindowService`, `IApplicationLogService`, `ILogger<MainWindowViewModel>`, settings, shell, editing, import, export, and transform services; it does not expose a `SetMainWindow(Window)` pattern.
- `RuntimeChapterLoadService` resolves importers through `IChapterImporterRegistry` and fallback logic instead of putting extension switches in the ViewModel.
- `DialogRequest`, `IDialogService`, `IWindowService`, and platform service interfaces keep Core and ViewModels away from static UI notification callbacks.
- `ApplicationLogPanelProvider` is bounded, structured, and backed by `Microsoft.Extensions.Logging`.

The remaining hardening work is mostly about making these boundaries observable and regression-resistant. `ApplicationLogPanelProvider` currently exposes snapshots and formatting, but no event that lets the log window subscribe to new entries. `MainWindow.axaml.cs` still performs some routine state synchronization through `Refresh()`, direct selector assignments, and manual command-state raises. Loading is async and awaited, but the contract that importer work must not mutate observable UI collections should be explicit and tested.

## Goals / Non-Goals

**Goals:**

- Add a bounded, evented UI log surface that supports both history replay and incremental updates.
- Keep structured `ILogger` as the source of diagnostic events while giving the log window a simple event-driven subscription model.
- Reduce main-window code-behind responsibility for routine UI state by moving state, command availability, and option synchronization into observable ViewModel properties and commands.
- Make async load/update threading expectations explicit: importers may perform background IO, but `ObservableCollection` updates happen in ViewModel-controlled UI flow.
- Preserve importer registry/fallback extensibility and platform-service boundaries.
- Add focused tests that prevent regressions without static source-string assertions over implementation files.

**Non-Goals:**

- No parser, exporter, chapter model, or file format behavior changes.
- No replacement of the existing OpenSpec capabilities or broad UI redesign.
- No new external logging framework beyond the existing standard logging/Serilog setup.
- No attempt to remove every view-specific code-behind event adapter; file pickers, drag/drop, keyboard adaptation, and layout adaptation can remain view responsibilities.

## Decisions

1. Keep `IApplicationLogService` as the UI log abstraction and extend it with evented behavior.

   The log window needs three things: a snapshot for history, an event for new entries, and a clear operation. Extending the existing service keeps the surface small and aligns with `ApplicationLogPanelProvider` already implementing both `IApplicationLogService` and `ILoggerProvider`.

   Alternative considered: introduce a separate `IApplicationLogObservable`. That would make read/subscribe responsibilities explicit, but it adds a second abstraction for the same UI sink and more composition wiring. A separate interface can be introduced later if non-UI sinks need different lifecycle semantics.

2. Emit events after entries are stored and trimmed.

   Subscribers should see the same entries that `Entries` would expose after capacity enforcement. The event payload should include the newly accepted `ApplicationLogEntry`; subscribers that need full state after a clear can ask for `Entries`.

   Alternative considered: emit before adding the entry. That makes the event lower latency but creates race-prone behavior where a log window receiving the event cannot yet see the entry in the snapshot.

3. Treat localization as display-time formatting, not event-time rendering.

   Existing `ApplicationLogEntry` supports message keys, arguments, technical detail, category, severity, and exception text. The event should carry the structured entry, and the log view/model should render with the active localizer when displayed.

   Alternative considered: store only rendered text. That is simpler for append-only UI but conflicts with existing language-switch requirements and loses structured details.

4. Move routine refresh state into ViewModel-observable properties and command state.

   The current code-behind `Refresh()` updates clip selector state, combine menu checks, and command availability. These should be bound to `SelectedClipIndex`, `IsClipCombineChecked`, capability flags, and commands that raise availability changes when relevant state changes.

   Alternative considered: keep `Refresh()` and add more tests around it. That preserves current behavior but keeps state synchronization split across ViewModel and view, which is the same failure mode this change is trying to prevent.

5. Keep code-behind as an adapter for platform UI events.

   Browse/save dialogs, drag/drop file extraction, DataGrid edit event adaptation, and responsive layout rearrangement are view concerns. The boundary issue is not the presence of code-behind; it is code-behind owning business or routine state synchronization.

6. Preserve registry-based import resolution and fallback as the only runtime importer routing path.

   `RuntimeChapterImporterRegistry` can keep internal extension switches because it is the registry implementation. ViewModels and windows must call load services and must not branch on file extensions to new parser instances.

## Risks / Trade-offs

- [Risk] Log events raised on background threads can update UI-bound collections from the wrong dispatcher. -> Mitigation: the log view/model subscribes through an adapter that marshals collection mutation to the UI dispatcher; tests cover event delivery without requiring a desktop session.
- [Risk] Removing manual `Refresh()` can expose missing property notifications or command availability raises. -> Mitigation: convert in small steps and add ViewModel/headless tests for clip switching, combine toggles, save availability, and frame/advanced option changes.
- [Risk] Extending `IApplicationLogService` affects test fakes and composition. -> Mitigation: update fakes in one pass and keep the new member minimal.
- [Risk] Background importers may report progress from worker threads. -> Mitigation: ViewModel progress and collection updates remain the only observable UI mutations; progress callbacks either update scalar state safely through the command path or are marshaled before touching UI-bound properties.
- [Risk] Static source assertions would be tempting for boundary checks. -> Mitigation: use compiled references, public service seams, ViewModel construction tests, headless UI behavior, and dependency-level tests instead of reading `.cs` or `.axaml` as text.

## Migration Plan

1. Extend the log-service contract and update `ApplicationLogPanelProvider`, tests, and log window/view-model subscription behavior.
2. Add tests that prove history replay plus incremental log events work and that clear removes history without disabling future events.
3. Move main-window option state and command availability refreshes into ViewModel properties/commands where they are currently mirrored by code-behind.
4. Reduce `MainWindow.Refresh()` to view-only adaptation or remove it once bindings and command notifications cover the state.
5. Add async load/threading regression tests using deterministic fake importers that complete asynchronously and verify `Rows`, `ClipOptions`, progress, and status update through ViewModel command flow.
6. Validate focused Avalonia and Infrastructure tests, then run the full solution test command before archiving.

## Open Questions

- Should `IApplicationLogService` expose a single `EntryAdded` event, or a richer change event that also reports `Cleared`? The lean initial path is `EntryAdded` plus existing `Clear()`, with a richer event only if the log window needs clear notifications for live views.
- Should command-state notification be centralized inside `UiCommand` with observed dependencies, or manually raised from ViewModel setters? Manual raises match the current codebase and are lower risk; a dependency-aware command helper can be considered later if duplication grows.
