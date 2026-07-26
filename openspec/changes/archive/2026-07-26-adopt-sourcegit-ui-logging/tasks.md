## 1. SourceGit User Interface Foundation

- [x] 1.1 Import the complete SourceGit icon dictionary.
- [x] 1.2 Import the complete SourceGit light and dark theme dictionaries.
- [x] 1.3 Port all reusable SourceGit control styles to Avalonia 12.1.
- [x] 1.4 Replace SourceGit preference and converter bindings with ChapterTool resources.
- [x] 1.5 Record each excluded Git-domain selector and each compatibility adaptation.
- [x] 1.6 Add the SourceGit MIT license and upstream source notice.

## 2. Application Resource Integration

- [x] 2.1 Load the SourceGit dictionaries after the Avalonia base themes.
- [x] 2.2 Keep ChapterTool product-specific styles after the SourceGit style layer.
- [x] 2.3 Map each ChapterTool light and dark preset to all required SourceGit color tokens.
- [x] 2.4 Verify that open windows refresh after a preset change.
- [x] 2.5 Verify that the main workflow zones remain usable with the global styles.

## 3. Structured Log Window

- [x] 3.1 Implement a disposable log ViewModel with bounded history and live updates.
- [x] 3.2 Preserve localized summaries, timestamps, severity, category, event name, technical detail, exception text, and structured state.
- [x] 3.3 Implement severity filtering without deleting retained entries.
- [x] 3.4 Implement clear, copy-summary, and copy-detail commands through injected services.
- [x] 3.5 Implement a SourceGit-style virtualized master-detail log view.
- [x] 3.6 Register the dedicated log tool through `ToolWindowRegistry`.
- [x] 3.7 Verify disposal, culture refresh, clear-and-resume behavior, and bounded file logging.

## 4. Tests And Documentation

- [x] 4.1 Add resource tests for icon, theme, style, and light and dark preset coverage.
- [x] 4.2 Add ViewModel tests for filtering, localization, copying, clearing, live updates, and disposal.
- [x] 4.3 Add Headless behavior tests for log selection, copying, clearing, later entries, theme refresh, and narrow layout.
- [x] 4.4 Update `docs/code-map/avalonia.md` and `docs/code-map/testing.md`.
- [x] 4.5 Run focused infrastructure and Avalonia unit tests in sequence.
- [x] 4.6 Run Avalonia Headless tests in a separate process.
- [x] 4.7 Build the Avalonia application.
- [x] 4.8 Run the full solution tests without restore.
- [x] 4.9 Validate the OpenSpec change with strict checks.

## 5. Settings Form Refinement

- [x] 5.1 Apply the SourceGit input templates and compact form metrics to all settings tabs.
- [x] 5.2 Move path browse and clear actions into each input right-content area.
- [x] 5.3 Rebuild the footer with left status and right-aligned reset and save actions.
- [x] 5.4 Add Headless checks for input metrics, embedded actions, footer alignment, and minimum width.
- [x] 5.5 Run focused tests, the Avalonia build, and strict OpenSpec validation.
- [x] 5.6 Apply one responsive form ratio and full-width editor alignment to each settings tab.
