## 1. Confirm integration boundaries

- [x] 1.1 Inspect the current candidate, frame-accuracy, and transaction APIs. Confirm the prerequisite behavior from `live-expression-preview`, `fix-wasm-modal-layout`, and the unified-editing changes without archiving or rewriting them.
- [x] 1.2 Trace both expression entry points and Avalonia tool-host close/focus paths. Identify the narrow lifecycle hooks needed for one review surface and background isolation. Record any implementation-specific adjustment in `design.md`.

## 2. Shared preview data and transaction evidence

- [x] 2.1 Add a host-neutral typed preview projection from immutable before/candidate snapshots. Preserve stable chapter identity, track order, current-track scope, separator exclusions, and property ownership.
- [x] 2.2 Add complete change classification and counts. Distinguish time, frame-information, and property effects. Use complete candidate equality for no-change behavior and retain every supported changed field.
- [x] 2.3 Add public-API tests for overlapping counts, frame-only and property-only effects, missing values, rational frame rates, incompatible frame bases, sub-millisecond changes, and 1,000 accessible chapter comparisons.
- [x] 2.4 Verify exact-candidate commit, stale-token rejection, duplicate identity handling, one-step undo, and redo without expression reevaluation. Extend existing transaction tests only where these outcomes lack coverage.

## 3. Avalonia state and review surface

- [x] 3.1 Replace summary-string state in `ExpressionToolViewModel` with typed preview data and explicit readiness states. Add initial preview, empty-input handling, debounce, composition handling, revision checks, and stale refresh.
- [x] 3.2 Remove Apply-time expression reevaluation. Submit the reviewed candidate with its existing identity. Preserve the draft on conflict or failure and prevent duplicate application.
- [x] 3.3 Replace the eight-difference text block in `ExpressionToolView.axaml` with option A: scope, multiline editor, summary, property changes, before/after table, changed/all filter, time/frame switch, and frame details.
- [x] 3.4 Add narrow vertical entries and complete scrolling or paging. Keep Cancel and Apply visible. Use existing theme resources and accessible labels, and preserve readable values at supported window sizes.
- [x] 3.5 Wire cancellation, native Close, outer Escape, and success through narrow tool-host ports. Restore entry parameters on cancel, close after success, preserve table position/selection, restore focus, and isolate background content actions.
- [x] 3.6 Add Avalonia unit coverage for pending, unchanged, invalid, stale, applying, failed, cancel, and repeated-operation behavior. Prove Apply does not evaluate Lua again.
- [x] 3.7 Add isolated Headless workflow coverage for multiline/preset edits, before/after review, filters, frame-only details, keyboard routing, close behavior, and successful application followed by one Undo.

## 4. Web state and review surface

- [x] 4.1 Replace flat Web difference formatting with the shared projection. Capture current-track scope and expose complete chapter and property effects through `WasmWorkspace`.
- [x] 4.2 Update `ExpressionDialog.razor` to use multiline editing, composition-aware debounce, explicit states, stale refresh, and exact-candidate application. Remove the dialog's empty-input identity fallback behavior.
- [x] 4.3 Implement option A with the same summary, comparison, filter, units, details, and footer order as Avalonia. Reflow to vertical entries on narrow screens and keep all rows accessible.
- [x] 4.4 Preserve the shared modal lifecycle. Verify cancel baseline restoration, late-result suppression, applying-state dismissal rules, background isolation, focus restoration, and accurate success feedback.
- [x] 4.5 Extend Web workspace tests for complete no-change detection, frame/property-only candidates, stale refresh, exact-candidate commit, cancellation, and one-step undo/redo.
- [x] 4.6 Extend browser E2E tests for rendered comparisons, multiline presets, input composition, fast edits, invalid candidates, cancel/close/Escape, repeated Apply events, focus, and post-apply export values.

## 5. Localization and cross-host acceptance

- [x] 5.1 Add shared locale labels and count templates for scope, states, change categories, units, details, missing values, and success. Verify accuracy wording against its actual tolerance semantics and keep internal enum/object strings out of normal presentation.
- [x] 5.2 Run `uv run --project scripts scripts/axaml-to-json.py` and `uv run --project scripts scripts/axaml-to-json.py --check`. Run `uv sync --project scripts` first only if the scripts environment is absent.
- [x] 5.3 Run affected Core, Avalonia, isolated Headless, and Wasm test projects sequentially with `dotnet test <test-project.csproj> --no-restore`. Use `docs/code-map/testing.md` to select the exact projects. Restore only if dependency assets change. Build the desktop host if its project files change.
- [x] 5.4 Run the browser regression commands documented in `docs/testing/wasm-browser-e2e-plan.md`, including the relevant Chromium and WebKit expression/modal cases. Assert geometry and actual hit targets in default, wide, 320px-wide, 390×844, and 844×390 layouts.
- [x] 5.5 Capture Avalonia and Web default, wide, and narrow screenshots under `artifacts/`. Check long scripts/names/errors, enlarged text, Chinese and English, light/dark themes, and mobile-keyboard accessibility. Record evidence paths and any real-device limitation.
- [x] 5.6 Update affected code-map ownership, entry points, and test references. Update the selected design document with the implemented outcome and verification evidence without changing unrelated guidance.
- [x] 5.7 Review every scenario in `specs/expression-preview-interaction/spec.md` against the implementation and test evidence. Run `openspec validate unify-expression-preview-interaction --strict` and record remaining blockers before declaring implementation complete.

## 6. Improve review clarity after visual feedback

- [x] 6.1 Explain expression input and units. Give editing and review clear headings in both hosts.
- [x] 6.2 Put time comparisons before expandable property details. Keep all persisted effects visible in the summary.
- [x] 6.3 Reduce editor and filter space. Verify that the first comparison is visible at default and narrow portrait sizes.
- [x] 6.4 Reflow desktop comparisons at narrow widths. Preserve keyboard controls and persistent actions.
- [x] 6.5 Generate shared locales. Run affected UI checks and capture updated screenshots.
- [x] 6.6 Record visual findings and verification in the design.

## 7. Compact the review surface

- [x] 7.1 Remove step headings, instructional paragraphs, footer guidance, and repeated labels. Reduce spacing and shorten localized summaries in both hosts.
- [x] 7.2 Verify rendered editing, comparisons, details, and footer actions. Refresh English and Chinese screenshots at default, wide, and narrow sizes.
- [x] 7.3 Record the compact layout and check results in the design.
