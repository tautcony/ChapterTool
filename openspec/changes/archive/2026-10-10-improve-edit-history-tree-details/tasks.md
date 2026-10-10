## 1. Core node inspection

- [x] 1.1 Add typed public node-detail, summary, field-value presence, and query-outcome models. Keep internal change-set mutation types private.
- [x] 1.2 Add a cancelable `SessionState` node query over one captured immutable history version. Handle missing nodes, ended sessions, cancellation, and recoverable resource failure without publication.
- [x] 1.3 Project chapter value, addition, deletion, membership, and order changes by stable identity. Count each modified chapter once.
- [x] 1.4 Project document, track, and segment property and structural changes. Resolve required historical labels, positions, and frame context through private iterative reconstruction.
- [x] 1.5 Provide root overview and ordered read-only chapter details. Keep lightweight tree summaries separate from selected-node field details.
- [x] 1.6 Extend `SessionEditHistoryTests.cs` and add `SessionHistoryInspectionTests.cs` under `tests/ChapterTool.Core.Tests/Session/` for alternate-branch inspection, exact typed values, moves, property-only edits, root inspection, cancellation, and failure outcomes. Assert unchanged cursor, revision, preferred edges, and document through public APIs.
- [x] 1.7 Extend existing deep-history measurements to include inspection and temporary allocation. Verify retained branches and delta-sized storage without automatic pruning.

## 2. Atomic operation metadata

- [x] 2.1 Add an immutable operation kind and typed optional parameter descriptor. Preserve existing description-based Core callers.
- [x] 2.2 Capture root load time and node publication time. Publish metadata atomically with successful changes. Verify no-change, stale, failed, canceled, and retried transactions through Core tests.
- [x] 2.3 Carry captured descriptors through `ChapterContentPreview` and `ChapterContentOperationSession`. Preserve candidate script, target scope, and effective parameters when live drafts change.
- [x] 2.4 Supply descriptors from Avalonia cell edits, insert/delete, expression, template/naming/numbering, frame-shift, and frame-rate operations. Capture effective display FPS where it affects results.
- [x] 2.5 Supply descriptors for workspace merge/split, append, and other commands that publish content changes. Preserve display-only frame updates without history nodes. Preserve Wasm callers and existing transaction behavior.
- [x] 2.6 Add command-level tests in `tests/ChapterTool.Avalonia.Tests/ViewModels/MainWindowViewModelTests.cs` for metadata capture, localized operation identity, and exact undo/redo without operation re-execution.

## 3. History session port and inspection state

- [x] 3.1 Extend `IHistorySessionPort` and its main-window adapter with typed tree/detail access, pending-preview state, and observed navigation outcomes.
- [x] 3.2 Add current/selected state separation, stable hierarchical children, expansion state, and Locate Current to the history ViewModels.
- [x] 3.3 Add cancelable detail loading, retry, request-generation checks, root identity checks, and disposal. Ignore obsolete completions after selection changes or session end.
- [x] 3.4 Add explicit restore and alternate direct-child redo actions. Disable duplicate submissions, preserve selection on failure, and select/reveal the current node after success.
- [x] 3.5 Add focused non-Headless history ViewModel tests for selection without navigation, expansion and collapse, tree topology, current-path markers, stale queries, retry, restore availability, and session teardown.

## 4. Native tree and detail UI

- [x] 4.1 Replace navigation buttons with native Avalonia `TreeView` controls backed by stable hierarchical node models.
- [x] 4.2 Add localized operation titles, distinct current and selected indicators, preferred redo choices, native expansion, and full-title tooltips.
- [x] 4.3 Add a concise inspector with nonzero effect counts, meaningful parameters, typed before/after values, exact time deltas, presence labels, selectable text, and useful change filters. Hide empty headings and explanatory prose.
- [x] 4.4 Add root chapter inspection and incremental or virtualized detail realization for large batches. Add loading, query-error, and retry states.
- [x] 4.5 Add explicit Restore, navigation status, and the pending-preview discard notice. Preserve inspection-only behavior for single-click, double-click, and Enter.
- [x] 4.6 Add a resizable wide split layout and narrow History/Details pages. Preserve selection, expansion, and scroll anchors across resize and page changes.
- [x] 4.7 Update the desktop history size constraints and verify the shared view in the embedded Avalonia host. Keep footers reachable within available host bounds.
- [x] 4.8 Implement tree keyboard navigation, inspector focus entry, close focus restoration, and accessible branch/current/selected/expanded names.

## 5. Localization and workflow verification

- [x] 5.1 Add English, Chinese, and Japanese history titles, summaries, field labels, parameter labels, presence states, and status messages to the shared AXAML resources. Use generic localized fallback titles without parsing descriptions.
- [x] 5.2 Run `uv sync --project scripts` if the environment is absent. Run `uv run --project scripts scripts/axaml-to-json.py`, then the same command with `--check`. Do not edit generated Wasm JSON by hand.
- [x] 5.3 Add Headless workflows for inspect versus restore, alternate redo, pending-preview preservation and discard, failure feedback, root details, filtering, long values, and large-tree realization.
- [x] 5.4 Extend `AvaloniaWindowServiceHeadlessTests` and embedded-host coverage for modal isolation, repeated open/close, detail cancellation, content detachment, and focus return. Keep UI tests in the Headless process and collection.
- [x] 5.5 Verify default 1000x700, wide 1280x800, and narrow 520x600 history windows. Verify light/dark themes, enlarged text, and all three locales with workflow and viewport-bound assertions.
- [x] 5.6 Capture and inspect review screenshots under `artifacts/edit-history-tree-details/` for the verified layouts and branch/detail states. Fix clipping, tree interaction, and inspector visibility errors. Do not treat image creation alone as a test assertion.

## 6. Final checks and documentation

- [x] 6.1 Run `dotnet test tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj --no-restore`, then the Avalonia, Wasm, and Avalonia Headless test projects sequentially with `--no-restore`. Restore once only if dependency or generated asset changes require it.
- [x] 6.2 Run `dotnet build src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj --no-restore` if its project file changes. Run broader solution checks only when changed consumers or unresolved failures require them.
- [x] 6.3 Update `docs/code-map/avalonia.md` and `docs/code-map/testing.md` with actual query, projection, lifecycle, and primary test ownership. Review the changed documentation for direct active sentences.
- [x] 6.4 Run `openspec validate improve-edit-history-tree-details --strict`. Report primary checks and screenshot paths. Before any code push, run `python scripts/check-ci.py` with the repository prerequisites.
