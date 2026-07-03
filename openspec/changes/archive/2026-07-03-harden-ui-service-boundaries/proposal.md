## Why

`docs/todo-list.md` captures the architectural guardrails learned from the legacy implementation: UI logging must be observable and bounded, ViewModels must stay independent of `Window`, chapter loading must not update observable UI collections from background work, import routing must remain registry-based, and UI prompts must use explicit services. The current Avalonia rewrite largely follows these directions, but the contracts are not fully explicit and a few seams still depend on manual UI refreshes or snapshot-only services, which makes regressions easy as more legacy workflows are restored.

## What Changes

- Add an evented application-log contract so the log window can display history and receive newly captured log lines without polling or reconstructing the panel from snapshots.
- Keep the log buffer bounded and structured, including diagnostics from import, export, and external-tool fallback paths.
- Tighten the main-window boundary so `MainWindowViewModel` remains free of Avalonia `Window`/`StorageProvider` dependencies and routine UI state is driven by bindings and observable command state rather than manual code-behind refresh.
- Make the load pipeline contract explicit: background import work may perform IO/parsing, but observable UI collections are updated only by the ViewModel command flow on the UI thread.
- Preserve registry-based importer resolution and fallback, and document that adding formats must register importers instead of expanding ViewModel extension switches.
- Preserve explicit dialog/window/platform services for prompts and notifications; do not reintroduce static notification events or Core UI callbacks.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `supporting-ui-platform-services`: strengthen logging, dialog, window, and platform-service requirements around bounded evented logs and explicit UI notification services.
- `avalonia-ui-shell`: strengthen main-window requirements around ViewModel/window decoupling, binding-driven refresh, command state observability, and UI-thread collection updates after async loads.

## Impact

- Affected projects: `src/ChapterTool.Core`, `src/ChapterTool.Infrastructure`, `src/ChapterTool.Avalonia`.
- Affected tests: `tests/ChapterTool.Infrastructure.Tests` for log service behavior and platform services; `tests/ChapterTool.Avalonia.Tests` for ViewModel/window decoupling, command state, headless UI binding behavior, and async load/update behavior.
- No chapter file format, parser, exporter, or external-tool command-line compatibility changes are expected.
