## 1. Headless Test Harness

- [x] 1.1 Extend `MainWindowHeadlessTestHost` with reusable fake service observability for load, save, window, file picker, shell, settings, logging, and localization scenarios.
- [x] 1.2 Add helpers for named control lookup, rendered text lookup, forced layout passes, default/wide/narrow sizing, DataGrid row realization, focused keyboard input, context-menu opening, and screenshot artifact capture.
- [x] 1.3 Split or organize headless tests into workflow-focused files so main-window state, command routing, grid command surfaces, localization, layout, and tool windows fail independently.

## 2. Main Window Rendered Workflow Coverage

- [x] 2.1 Add headless tests for the initial no-source main-window state, including visible workflow zones, hidden clip selector, collapsed advanced panel, disabled save action, default TXT save type, and default status/progress.
- [x] 2.2 Add headless tests that load deterministic chapter sources through the visible load command surface and assert rendered path, status/progress, chapter rows, save availability, frame-rate selection, and option control state.
- [x] 2.3 Preserve and consolidate XML, IFO, and MPLS multi-option headless tests so selected labels and selected chapter names render without stale or blank grid values.
- [x] 2.4 Add headless tests for save/output options changed through rendered controls, including save format, XML language enablement, naming/template options, order shift, expression, frame rate, and save-directory routing to the fake save service.

## 3. Grid, Keyboard, and Menu Interaction Coverage

- [x] 3.1 Add headless tests for rendered DataGrid command surfaces without duplicating ViewModel-level time, name, and frame edit behavior.
- [x] 3.2 Add headless tests for row selection plus insert, delete, combine, zones, and forward-shift availability from visible command surfaces or context menus.
- [x] 3.3 Add headless tests that send documented shortcuts to the focused window and verify command effects for load, save, save-directory, reload/refresh, log, fullscreen, and clip selection.
- [x] 3.4 Add headless tests for load, clip, and row context menus under enabled and disabled capability states.

## 4. Secondary Tools and Localization Coverage

- [x] 4.1 Add headless tests that invoke preview, log, settings, color, language, expression, template names, zones, forward shift, and related auxiliary commands and assert the expected window identifiers or rendered tool views.
- [x] 4.2 Add representative rendered tests for settings, color settings, language, expression, template names, text/log/preview, and forward-shift tool views with deterministic ViewModels.
- [x] 4.3 Add headless tests for runtime language switching across Simplified Chinese, English, and Japanese for representative main-window and tool-window text.
- [x] 4.4 Add fallback localization coverage for blank or unsupported language settings, verifying no visible localization keys or mojibake strings render.

## 5. Responsive Layout and Determinism

- [x] 5.1 Add default, wide, and narrow rendered layout tests for the main window and representative secondary tools, with screenshot artifacts written under `artifacts/`.
- [x] 5.2 Assert responsive usability signals for numeric controls, DataGrid minimum widths, centered button content, bottom options, and workflow-zone visibility without exact pixel approval matching.
- [x] 5.3 Ensure all new headless UI tests use fake services or deterministic in-process fixtures and do not depend on external media tools, desktop sessions, registry state, shell launches, native dialogs, network, wall-clock timing, or machine-specific paths.
- [x] 5.4 Remove or avoid any source-text assertions over `.cs`, `.axaml`, `.csproj`, scripts, CI YAML, README, or docs when validating UI behavior.

## 6. Verification

- [x] 6.1 Run `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore`.
- [x] 6.2 Run `dotnet test ChapterTool.Avalonia.slnx --no-restore`.
- [x] 6.3 Run `openspec validate expand-headless-ui-tests --strict`.
- [x] 6.4 Document generated screenshot artifact paths in the implementation summary.
