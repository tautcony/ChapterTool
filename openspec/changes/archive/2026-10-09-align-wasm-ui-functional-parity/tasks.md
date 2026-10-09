## 1. Establish regression fixtures and scope

- [x] 1.1 Add matching Core, Web, and Avalonia operation fixtures for ordinary chapters, separators, rational FPS, names, and numbering. Compare relevant content through public APIs. Covers D01-D06.
- [x] 1.2 Add a Web regression that applies automatic names, manually renames a chapter, and saves appearance settings. Assert the name, committed document, history cursor, and export remain unchanged. Covers D02.
- [x] 1.3 Add Web regressions for prepared naming, frame shift, and frame conversion. Assert preview and cancellation leave the document and history unchanged. Covers D01, D03, D04.

## 2. Align reviewed content operations

- [x] 2.1 Add narrow Web prepare/apply/cancel entry points over Core candidates. Retain candidate identity, session, current-track scope, draft revision, and base version. Covers D01, D03, D04.
- [x] 2.2 Separate `Home.razor` output/display/settings handlers from content-operation handlers. Remove content commits from settings and rounding changes. Preserve the active session format during settings save. Covers D02.
- [x] 2.3 Add naming, template, and numbering draft controls with an explicit review surface. Move content parameters out of advanced output preferences. Loading templates must not commit. Covers D01, D11.
- [x] 2.4 Clear active naming/numbering intent after success. Preserve template text only for deliberate reuse. Verify later preference changes do not overwrite manual edits. Covers D01, D02.
- [x] 2.5 Add frame-rate review with source/target FPS, typed differences, explicit Apply, and Cancel. Commit the exact reviewed candidate. Covers D03.
- [x] 2.6 Add signed frame-shift drafts from -1000000 to 1000000. Verify FPS basis, zero/no-change behavior, negative-time rejection, and limits through Core. Covers D04.
- [x] 2.7 Align Web and Avalonia numbering controls with 0..1000. Add rendered rejection of negative/fractional input and preserve Core normalization for API consumers. Covers D06.
- [x] 2.8 Add candidate race and repeat-submission tests. Verify stale rejection, explicit refresh, no partial commit, and one-step Undo/Redo. Covers D01, D03, D04.

## 3. Complete browser table editing

- [x] 3.1 Add identity-based Web cell drafts for time, name, and frame fields. Use Core parsing and frame-to-time conversion. Covers D05, D12.
- [x] 3.2 Implement Enter, Tab, valid blur, Escape, and local text Undo. Preserve invalid drafts with accessible errors. Prevent Enter-plus-blur duplicate commits. Covers D12.
- [x] 3.3 Preserve surviving chapter selection and usable focus after edit, history navigation, and clip changes. Reject edits whose captured identity or base is stale. Covers D12.
- [x] 3.4 Add workspace and Playwright tests for frame edits, invalid time/frame input, unchanged values, separators, and missing FPS. Compare accepted results with Avalonia. Covers D05, D12.
- [x] 3.5 Run a 1000-row browser workflow that scrolls to and edits the last row. Record interaction and rendering evidence. Add paging or virtualization only if that evidence requires it. Covers D12.

## 4. Add Lua file loading and authoring assistance

- [x] 4.1 Add `.lua` file input to the expression tool. Reuse shared script rules and bounded reads. Keep source metadata and restore entry state on Cancel or failed input. Covers D07.
- [x] 4.2 Connect the browser editor to Core authoring diagnostics and completion for the current draft and caret. Reject late results from obsolete revisions. Covers D07.
- [x] 4.3 Add theme-aware syntax highlighting and positioned diagnostics without duplicating accessible text or changing script content. Covers D07.
- [x] 4.4 Add keyboard completion, range replacement, and completion-first Escape behavior. Preserve multiline input, local Undo, IME composition, and debounce. Covers D07.
- [x] 4.5 Add script-loading, completion, diagnostic-position, rapid-input, cancellation, and composition browser tests. Retain existing exact-candidate expression and one-step Undo coverage. Covers D07.

## 5. Align settings and shortcuts

- [x] 5.1 Add an optional canonical shortcuts child to `WasmSettings`. Preserve the current storage key and version-one application/theme/font values. Verify missing and malformed overrides load usable defaults without rewriting on load. Covers D08.
- [x] 5.2 Add a Shortcuts settings page with record, clear, per-action reset, and shared conflict validation. Show localized invalid or unsupported gesture errors. Covers D08.
- [x] 5.3 Derive Web dispatch, displayed hints, and application default suppression from one active mapping. Preserve editable-target and modal input isolation. Covers D08.
- [x] 5.4 Route existing Insert/Delete and previous/next clip actions. Verify command prerequisites, range boundaries, and fixed Undo/Redo aliases. Covers D08.
- [x] 5.5 Add normalized settings draft comparison. Validate and persist once before activating saved runtime settings. Preserve the previous active snapshot and draft after failure. Covers D09.
- [x] 5.6 Add changed-draft Close/Escape confirmation with Discard and Keep editing. Preserve intentional Cancel and draft-only Reset. Verify nested focus restoration. Covers D09.
- [x] 5.7 Add browser workflows for shortcut recording, save/reopen, conflict rejection, text-input isolation, and modal isolation. Covers D08.
- [x] 5.8 Add settings workflows for unchanged close, keep editing, discard, reset/cancel, and injected storage failure. Assert no content history changes. Covers D02, D09.

## 6. Complete the browser log tool

- [x] 6.1 Add stable log-entry identity and a filtered projection over the existing bounded source. Add case-insensitive search, severity filters, counts, and detail-match indication. Covers D10.
- [x] 6.2 Add list-first presentation and an explicit inspector. Preserve surviving selection and scroll state. Resolve filtered or evicted entries without selecting replacements. Covers D10.
- [x] 6.3 Add selected-entry copy and clear workflows with recoverable failure feedback. Preserve filters and avoid stale inspector data. Covers D10.
- [x] 6.4 Add secondary JSON/CSV export of a captured filtered snapshot through the existing download adapter. Verify UTF-8, quoting, multiline fields, deterministic ordering, and chapter-baseline isolation. Covers D10.
- [x] 6.5 Add wide side-by-side and narrow replacement inspector layouts with keyboard details/back actions. Covers D10.
- [x] 6.6 Add behavior tests for hidden-detail search, filters, live append, retention eviction, explicit inspection, copy/clear, filtered export, and export failure. Covers D10.

## 7. Localize and verify cross-host parity

- [x] 7.1 Add localized labels, scope/status text, errors, shortcut hints, log actions, and XML language display names in shared locale AXAML files. Preserve stable values and codes. Covers D01-D12.
- [x] 7.2 Generate Web locale JSON with `uv run --project scripts scripts/axaml-to-json.py`. Run the same command with `--check`. Install the scripts environment once if absent. Covers D11.
- [x] 7.3 Compare matching operation sequences in Web and Avalonia through public APIs. Verify committed fields, selected scope, transaction counts, Undo/Redo, and serialized bytes for matching preferences. Covers D01-D07.
- [x] 7.4 Extend Playwright workflows at 1280x800, 1920x1080, 390x844, 390x640, and 844x390. Verify action reachability, focus restoration, modal isolation, and lack of outer horizontal overflow. Covers D01-D12.
- [x] 7.5 Verify English, Chinese, and Japanese labels and accessible names through resource or rendered behavior. Preserve Unicode chapter text and existing expression regressions. Covers D07-D11.
- [x] 7.6 Capture default, wide, and narrow Avalonia evidence for the numbering control change under `artifacts/`. Record browser screenshots for changed surfaces. Use behavior checks alongside screenshots. Covers D06, D11, D12.

## 8. Complete checks and documentation

- [x] 8.1 Run affected Core, Web, Avalonia unit, and Avalonia Headless test projects sequentially with `--no-restore`. Restore once only if dependencies or generated assets require it. Keep Headless in its own process.
- [x] 8.2 Run `dotnet test ChapterTool.slnx --no-restore` after shared Core or Contracts changes. Fix failures caused by this change.
- [x] 8.3 Run the Web build and browser typecheck/E2E commands selected from `scripts/check-ci.py`. Use supported browser engines. Run visual comparison only in its matching Linux baseline environment.
- [x] 8.4 Update `docs/code-map/program-form-capability-map.md`, `avalonia.md`, and `testing.md` for changed owners, entry points, behavior, and primary tests. Update the Web README for newly supported portable features and retained platform boundaries.
- [x] 8.5 Review D01-D12 against implementation evidence. Run `openspec validate align-wasm-ui-functional-parity --strict` and `git diff --check`. Report unsupported-host checks and screenshot paths. Leave deferred platform items and existing changes untouched.
