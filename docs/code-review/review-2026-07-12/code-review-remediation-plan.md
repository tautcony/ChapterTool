# Code Review Remediation Plan

## Purpose

This document turns the outstanding findings from `docs/code-review-src.md` and the follow-up implementation review into concrete, verifiable work. It covers only gaps that remain after the `src-code-review-remediation` change's current implementation.

The work is ordered by risk. Do not archive the OpenSpec change until every acceptance criterion below has evidence from behavior tests and the final full-solution gate.

## 1. MPLS Parent-Container Bounds

### Finding

`MplsPlayList.Read` validates the playlist's declared length only after it has allocated and parsed all declared child items. A malformed playlist can declare a header-sized container, set a large but individually permitted play-item count, and place parseable child data after the parent's declared boundary. The current final `SkipContainerRemainder` check fails eventually, but only after unnecessary work and allocation.

This violates the intended rule: a child read, skip, allocation, or iteration must not cross its parent's declared container boundary.

### Target Design

Introduce an internal bounded MPLS reader/view that represents one declared-length container:

- It stores the underlying seekable stream and an exclusive absolute end position.
- Every primitive read and skip checks that the requested bytes fit before the end position.
- Entering a nested variable-length container first verifies that its length field and minimum header fit in the parent.
- Collection counts are validated against both their semantic maximum and the parent container's remaining bytes before `List<T>` allocation.
- Completing a container requires the cursor to be at or before its end; only a non-negative remainder may be skipped.

The generic `BinaryReadExtensions` remains protocol-agnostic. `MplsParseLimits` owns semantic MPLS maxima; the bounded reader owns positional safety.

### Implementation Steps

1. Add `MplsBoundedReader` or an equivalent internal value/object under `Importing/Disc/`.
2. Make `MplsPlayList`, `MplsPlayItem`, `MplsSubPath`, `MplsSubPlayItem`, `MplsSTNTable`, mark-table, and extension-data parsers accept/use the bounded reader for their declared payloads.
3. Replace post-hoc `SkipContainerRemainder`-only enforcement with pre-read checks for mandatory headers, count-derived minima, child length fields, and padding.
4. Preserve the generic 64 MiB exact-read ceiling and existing MPLS semantic caps.

### Tests

- A playlist with `length = 6`, a count within `MaximumPlayItems`, and child bytes beyond the parent length fails before child object allocation/iteration.
- A nested STN table, subpath, and extension table each reject children that cross their direct parent boundary.
- Existing valid MPLS fixtures remain green.
- Tests use compact synthetic streams, not memory-heavy inputs or source-text assertions.

### Acceptance Criteria

- No parser reads or seeks past a declared MPLS parent container before raising `InvalidDataException`.
- Every untrusted collection count is checked against a semantic cap and a byte-budget-derived bound before allocation.
- `dotnet test tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj --no-restore` passes.

## 2. Expression Authoring Service Initial Render

### Finding

`ExpressionEditor` performs its first analysis in its constructor, before Avalonia applies the `AuthoringService` binding. It therefore uses its design/test fallback for initial completions and diagnostics. Setting the bound service later does not trigger re-analysis.

### Target Design

The injected authoring service must be authoritative whenever an editor is created through production XAML. The fallback remains only for direct design-time/test control construction without an injected property value.

### Implementation Steps

1. Handle `AuthoringServiceProperty` in `ExpressionEditor.OnPropertyChanged`.
2. When a non-null service arrives, cancel/reset any delayed diagnostic state as necessary and call `AnalyzeAndRender(renderDiagnosticsImmediately: true)`.
3. Ensure this re-analysis does not overwrite a user edit or create a text-binding loop.
4. Retain the fallback only through `EffectiveAuthoringService` when the property is null.

### Tests

Create a counting/sentinel `IExpressionAuthoringService` that returns recognizable diagnostics/completions:

- Build the main window through a composition root configured with the sentinel; locate `ExpressionBox`; assert the sentinel receives analysis on the initial rendered text.
- Open the expression tool through the normal `AvaloniaWindowService`/registry path; locate its `ExpressionEditor`; assert the same sentinel receives analysis.
- Change editor text and verify subsequent analysis still uses the sentinel.
- Keep existing highlighting, completion, and diagnostics Headless coverage unchanged.

### Acceptance Criteria

- Both XAML hosts invoke the composition-owned service for initial and subsequent analysis.
- The fallback service is not invoked on either production host.
- Avalonia unit tests and Headless tests run sequentially and pass.

## 3. MainWindow Command-Surface Completion

### Finding

`MainWindow.axaml.cs` still performs template-reading and ViewModel state transitions after choosing a template path. The view should only collect UI-only input and forward it; file loading, state updates, localization, and error handling belong to the ViewModel/workflow layer.

### Target Design

Add a ViewModel method or `UiCommand` that accepts a selected template path and owns:

- reading the template text;
- updating `ChapterNameTemplateText`, `ChapterNameTemplateStatus`, and naming mode;
- diagnostic/status/logging behavior for I/O failure.

The window adapter only invokes the picker and passes the returned path to this method/command.

### Implementation Steps

1. Extract the non-picker portion of `LoadChapterNameTemplateAsync` into `MainWindowViewModel` or a naming/projection collaborator.
2. Expose a path-taking command/method that is independently unit-testable.
3. Leave keyboard and visible-button routes pointing to the same ViewModel behavior.
4. Remove direct mutation of ViewModel naming properties from the window code-behind.

### Tests

- Unit test a successful template path updates template text, mode, status, and projected rows.
- Unit test an unreadable path returns a structured status/diagnostic without corrupting the previous valid template state.
- Headless test picker adapter only supplies the selected path and observes the ViewModel outcome.

### Acceptance Criteria

- No business state transition remains in `MainWindow.axaml.cs` for template loading.
- All template-load paths use the same ViewModel workflow.

## 4. Preserve Structured Import Logging

### Finding

The coordinator extraction reduced `LogImportSummary` from summary plus per-group/per-entry structured logs to a summary only. This is a behavior regression for diagnosing imported sources, selected titles, format, duration, and frame rate.

### Target Design

`StatusDiagnosticsPresenter` remains the log owner but restores the previous structured records:

- one import summary;
- one record per source group;
- one record per import entry.

Inject or pass the shared time formatter needed to format duration; do not re-create it in the presenter.

### Implementation Steps

1. Add `IChapterTimeFormatter` to `StatusDiagnosticsPresenter`'s constructor.
2. Restore `Log.ImportGroup` and `Log.ImportEntry` emission with the original structured fields.
3. Keep the current severity and `MessageKey` convention.

### Tests

- Use a fake/logger sink with a multi-entry import result.
- Assert one summary, the expected group records, and the expected entry records with source, format, chapter count, duration, and fps fields.
- Verify failed imports still log an error-level summary and diagnostics.

### Acceptance Criteria

- The log panel and external logger retain the per-entry context available before coordinator extraction.

## 5. Composition Identity Contract

### Finding

The production root now appears to share formatter, authoring service, export service, external-tool locator, and process runner. However, there is no test that protects this lifecycle contract, so future factory changes can silently recreate divergent instances.

### Target Design

Document the production lifetime policy and test observable identity/policy consistency across:

- GUI importer registry and media importer;
- save service and export service;
- main-window ViewModel and both expression editors;
- settings window and importer registry external-tool resolution.

### Implementation Steps

1. Add narrowly scoped internal test accessors only when public factory output cannot otherwise expose the dependency identity.
2. Prefer reference-identity assertions for pure shared services.
3. For services intentionally created per call, assert they receive the same shared dependency instances instead of asserting the wrapper itself is a singleton.
4. Update `docs/code-map/avalonia.md` with the final lifetime table if it changes.

### Tests

- One composition-root test creates all relevant production factories from one root.
- It verifies shared formatter/expression policy/external locator identity or equivalent injected identity at each boundary.
- It verifies that the authoring service supplied to both editor paths is the root-owned instance.

### Acceptance Criteria

- OpenSpec task 5.3 has executable coverage rather than only implementation intent.
- No GUI production factory creates a second default formatter, expression engine, authoring service, or external-tool locator.

## 6. Documentation and Close-Out

### Documentation Changes

Update `docs/code-map/core.md` so it names the correct owners:

- `BinaryReadExtensions.cs` and `DiscBinaryReadLimits` for generic exact-read bounds;
- `MplsParseLimits.cs` for MPLS container/count/address limits.

Update `docs/code-map/avalonia.md` after the command and lifecycle work above. Cross-link `docs/code-review-src.md` from the eventual PR summary.

### Final Verification Order

1. Core parser tests.
2. Avalonia unit tests.
3. Avalonia Headless tests, after unit tests complete.
4. `dotnet test ChapterTool.Avalonia.slnx --no-restore` as one full-solution gate.
5. `openspec validate "src-code-review-remediation" --strict`.
6. Check every completed task with a concise evidence note, sync delta specs, and archive the change.
