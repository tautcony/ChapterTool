# Avalonia Headless Test Performance

This document records the reusable diagnosis completed for Headless tests that were fast in isolation but slow when run together.

The final implementation passes the Headless and unit test suites. No fixed `Task.Delay`, polling loop, `Thread.Sleep`, or `SpinWait` remains in the Headless test project. The important result is the causal model below, not a wall-clock value from one machine.

## Symptom And Diagnosis

Avalonia Headless uses one process-wide UI session for this test assembly. The Headless collection serializes its tests, but serialization does not release windows, visual trees, Popup roots, event subscriptions, or Dispatcher work between tests.

The characteristic symptom was:

1. A test class completed quickly in a fresh test process.
2. The same class became progressively slower after other Headless classes ran in the same process.
3. Assertions and domain work did not account for the extra time.
4. Explicitly releasing the preceding class's UI-owned state removed the extra time.

The primary cause was `MainWindowHeadlessTestHost.Dispose()` closing its Window without detaching the Window's `Content` visual tree. The test host owned and retained the Window, and the still-reachable tree continued to increase shared-session layout and resource work. Closing the Window, clearing `Content`, and draining queued UI jobs removed this cross-test cost.

Secondary lifecycle leaks amplified the same behavior:

- Directly constructed `SettingsToolViewModel` instances were not disposed, leaving `CultureChanged` and `Appearance.Changed` subscriptions active.
- ComboBox Popup and ContextMenu surfaces opened by tests were not always closed in `finally`.
- Two settings-window tests intentionally cancelled Close or kept a live window open, but did not perform a final accepted Close after their assertions.
- Tool-view tests created a complete `MainWindowHeadlessTestHost` when they only needed an `AppLocalizationManager`.
- The shared layout helper repeated `ExecuteInitialLayoutPass()` for the same Window.

These are test ownership defects. They are not failures of the production workflows being asserted.

## Causal Evidence

Use relative comparisons rather than absolute timing values:

- Run `MainWindowInteractionHeadlessTests` alone. It is fast in a fresh process.
- Run `SettingsToolHeadlessTests` followed by `MainWindowInteractionHeadlessTests` in one process. The later class becomes disproportionately slower.
- Dispose the directly constructed settings ViewModels. The combined run returns close to the sum of the isolated runs.
- Run `MainWindowHeadlessTests` followed by `ToolViewsHeadlessTests`. The tool-view tests become slow as a group.
- Detach the disposed main window's `Content` tree. The tool-view group returns close to its isolated behavior.

The cases that appeared slowest in the contaminated run were `Bound_options_and_stable_column_tags_work_across_ui_languages` for `zh-CN` and `ja-JP`, and `Context_menu_items_respect_capability_flags`. They were victim tests: the same tests were ordinary rendering tests after predecessor cleanup. This distinction prevents optimizing the victim assertion or adding waits to it.

## Removed Waits

- Removed the fixed delay from `Expression_edit_delays_invalid_result_but_applies_valid_result_immediately`; valid state is asserted after deterministic layout/Dispatcher processing.
- Removed the fixed delay from the expression-editor changing-state test.
- Replaced the font startup test's bounded polling loop with `AppCompositionRoot.AppearanceSettingsInitialization`, the actual asynchronous completion contract.

`Dispatcher.UIThread.RunJobs()` and `Task.Yield()` remain where they drain or hand control back to the Avalonia UI dispatcher. They do not impose a fixed wall-clock delay.

## Reproduction And Triage

Run one class in a fresh process:

```bash
dotnet test tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj \
  --no-restore \
  --filter 'FullyQualifiedName~MainWindowInteractionHeadlessTests' \
  --logger 'console;verbosity=detailed'
```

Run suspected producer and victim classes in the same process:

```bash
dotnet test tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj \
  --no-restore \
  --filter 'FullyQualifiedName~SettingsToolHeadlessTests|FullyQualifiedName~MainWindowInteractionHeadlessTests' \
  --logger 'console;verbosity=detailed'
```

When the combined duration is materially greater than the sum of isolated durations, inspect the earlier class for retained Windows, content trees, Popup/ContextMenu roots, DispatcherTimer work, and event subscriptions. The absolute duration is environment-dependent; the non-additive relationship is the signal. Do not start by increasing a delay or deleting the later slow test.

## Prevention Rules

- Use `MainWindowHeadlessTestHost` for main-window tests. Its disposal must close the Window, detach `Content`, and drain queued UI jobs.
- Close every opened ComboBox, Popup, and ContextMenu in `finally`.
- Dispose every directly constructed `IDisposable` ViewModel or DataContext. Closing a manually created Window does not invoke `AvaloniaWindowService` DataContext disposal.
- Do not create a complete main-window host when a test only needs a localizer or one control.
- Execute the initial layout pass only once per Window; use normal layout passes afterward.
- Test asynchronous completion through a Task/event/state transition owned by the production lifecycle. Do not infer completion from elapsed time.
- Compare a class alone, a producer/victim pair, and the full Headless project when investigating order-dependent performance.
- Keep Headless tests in their separate test project. Collection serialization inside a mixed unit/UI assembly is not process isolation.

## Implementation Map

- Shared Window/layout/transient UI cleanup: `tests/ChapterTool.Avalonia.Headless.Tests/Headless/MainWindowHeadlessTestHost.cs`
- Settings Window final-close coverage: `tests/ChapterTool.Avalonia.Headless.Tests/Headless/AvaloniaWindowServiceHeadlessTests.cs`
- Direct settings ViewModel disposal and font Popup cleanup: `tests/ChapterTool.Avalonia.Headless.Tests/Headless/SettingsToolHeadlessTests.cs`
- ComboBox Popup cleanup: `tests/ChapterTool.Avalonia.Headless.Tests/Headless/MainWindowHeadlessTests.cs`
- Lightweight expression-editor hosts and removed fixed waits: `tests/ChapterTool.Avalonia.Headless.Tests/Headless/ToolViewsHeadlessTests.cs`
- Startup completion contract: `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs`
- Completion-contract coverage: `tests/ChapterTool.Avalonia.Headless.Tests/Composition/AppCompositionRootFontTests.cs`
