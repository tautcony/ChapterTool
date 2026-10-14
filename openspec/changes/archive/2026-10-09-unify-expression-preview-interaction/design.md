## Context

The user selected option A from [the interaction design](../../../docs/tasks/expression-preview-interaction-design.md). The expression editor and chapter comparison must share one independent tool surface. The desktop host uses Avalonia. The Web host uses Blazor WebAssembly.

`ChapterContentPreview` already contains `Before`, `Candidate`, `TargetIds`, `BaseToken`, and a preview identity. `ChapterContentOperationSession.ApplyAsync` can commit the candidate without evaluating Lua again. The existing difference records contain display strings. They do not provide a suitable typed presentation contract.

Avalonia currently limits its summary to eight differences and recalculates on Apply. Web uses a flat list and falls back to internal field names. Web can also add a detected frame rate during preparation. A preview must expose these persisted effects even when chapter times do not change.

Prerequisites are the implemented changes `live-expression-preview`, `fix-wasm-modal-layout`, and the unified-editing transaction series. Their artifacts are still unarchived. Main specs retain some older projection wording. Implementation must use the current candidate model. Before eventual archive, synchronize prerequisite deltas in dependency order, then synchronize this additive capability. Do not copy old projection behavior into this change or archive other changes as part of its implementation.

## Implementation Findings

- `ChapterContentOperationSession.Prepare` captures immutable `Before` and `Candidate` documents, target identities, a base token, validation errors, and a generated difference list. `ApplyAsync` submits the existing candidate through `SessionState.ExecuteAsync`; its builder does not reevaluate the expression.
- `ChapterContentCandidateBuilder.ApplyExpression` targets every chapter in its input document. The Avalonia path uses the active content session. The Web path captures `CurrentTrackIndex`, adds detected frame-rate metadata before building, evaluates only the focused track, and restores the other tracks afterward.
- `EditableChapterDocument` has document metadata, ordered tracks, chapter fields, and track segment metadata. The generic difference list does not include segment changes, so the shared projection must compare the immutable snapshots directly.
- Avalonia hosts expression tools as reusable native windows. `AvaloniaWindowService` currently disposes the tool ViewModel on native close, but the expression ViewModel has no host close port. Successful preview application also leaves that window open.
- Avalonia preview input changes use a 180 ms timer and do not track input composition. The ViewModel recalculates in the Apply command and again after conflicts. It has no explicit stale or applying state.
- Web already uses the shared modal component and restores expression and preset parameters on cancel. Its workspace recalculates after a conflict, while its dialog enables Apply using the difference list and empty input is converted to `t` during preparation.
- The workspace candidate is an immutable document snapshot. A projection can preserve frame values as strings and expose each side's rational frame-rate basis without parsing presentation strings. Frame text is not guaranteed to be numeric, so numeric frame deltas must be optional.

## Goals / Non-Goals

**Goals:**

- Give both hosts the same scope, result structure, terminology, and confirmation rules.
- Show complete chapter-level before/after values and distinguish time, frame, and property changes.
- Commit exactly the result the user reviewed as one undoable operation.
- Keep multiline scripts and long results usable on desktop and narrow Web viewports.
- Preserve localization, keyboard input, focus, themes, and current export semantics.

**Non-Goals:**

- Add selected-chapter or all-track scope controls.
- Add per-row approval, candidate editing, a timeline, or a second working document.
- Change Lua syntax, frame conversion policy, persistent document fields, or history storage.
- Implement alternative layouts B and C.
- Require syntax-highlighting or completion parity between Avalonia and Web.

## Decisions

### 1. Use option A in both hosts

The order is title, scope, preset, multiline editor, result summary, chapter comparison, expandable property changes, and footer. The footer contains Cancel followed by Apply changes. Both buttons remain present across states. The compact layout omits step headings and instructional paragraphs.

Wide layouts use columns for chapter identity, before, after, and delta. Narrow layouts use labeled entries. Avalonia reflows below 640 DIP. Web reflows below 760 CSS px. The Web editor starts at two lines and can expand. Avalonia starts with its compact editor and can expand to 160 DIP. The result remains accessible when the script is long.

Default filtering is changed chapters. An all-chapters view provides context. The default unit is time. A frame view and row details expose frame values and accuracy. Filters only affect presentation.

Option B was rejected because switching between complete tables weakens direct comparison. Option C was rejected because moving between steps interrupts live review. No preference toggle selects between these layouts.

### 2. Derive one typed preview projection from immutable snapshots

Add a small host-neutral projection under Core's existing session area. It must not reference Avalonia, Blazor, or localized UI resources. Both hosts consume this projection. Host adapters resolve shared resource keys and format culture-dependent values.

The projection contains these logical values; exact type names are implementation choices:

| Value | Source and purpose |
| --- | --- |
| Scope | Captured track identity/name, participating chapter identities, excluded separator count |
| Summary | Unique changed chapters, time-changed chapters, frame-information updates, unchanged times, property changes |
| Chapter comparison | Stable identity, display number/name, before/after ticks, typed frame values and accuracy, change categories |
| Property comparison | Typed before/after values with explicit document or track ownership |
| Diagnostics | Existing errors with locations only when available |

Use snapshot values, not parsing of `ToString()` output. Match chapters by identity and preserve track order. Frame accuracy counts can overlap time-change counts. The affected-chapter total must use a set union. Exclude separators from expression chapter counts even if existing `TargetIds` includes them.

Unknown or newly supported persisted changes must remain visible through a labeled details entry. Add explicit handling and tests for every field that the expression candidate currently changes. Do not silently filter a difference out because its field is unfamiliar.

Public UI text must use business labels. Unknown frame rate and uncomputed frame data are distinct from zero. Audit the existing accuracy tolerance before choosing translations for its enum values. Time precision must increase when millisecond formatting would conceal a real change. Compare frame deltas only when both values have a compatible frame-rate basis.

The alternative of separate string builders was rejected because it would preserve drift between hosts. A general-purpose diff framework is unnecessary for this scoped feature.

### 3. Capture scope and expose all side effects

Preparation captures the current track and its chapter identities. No selection means no implicit change of scope. Scope text remains visible during review.

If frame-rate detection changes document or segment metadata outside chapter rows, show its actual ownership in the property section. Do not label document-wide metadata as a change to only the current track. Preserve the existing calculation policy in this change; make its effects reviewable.

Empty input is an incomplete draft and cannot be applied. An explicit `t` is evaluated normally. It can still expose persisted frame-information changes. No-change detection uses the complete candidate, not just time deltas or a filtered list.

### 4. Own one current candidate and explicit state per tool session

States are unavailable, empty, waiting, computing, ready, unchanged, invalid, stale, applying, and failed. Success closes the tool. Each state has a localized explanation and a defined Apply eligibility.

Input or preset changes invalidate the previous candidate immediately. Use approximately 250 ms debounce in both hosts. Input-method composition must finish before scheduling evaluation. Each calculation captures a draft revision and scope. Only the latest result can enter ready state. Closing or replacing the document invalidates all outstanding results.

Old values can remain visible during computation, but must carry an old-preview label. They cannot be applied. Computation must not block normal editor interaction; use the existing host-appropriate scheduling facilities and cancellation limits. Do not add a worker architecture without measured need.

A changed document token or scope enters stale state. The user invokes Update preview, reviews the new result, and confirms again. The prior stale-preview contract's new preparation occurs through this refresh action. It does not authorize committing that new result under the earlier click.

### 5. Commit the reviewed candidate without evaluation

Apply requires a valid, changed candidate whose draft revision, scope, and session base still match. The handler captures that candidate and submits its existing identity through the transaction entry point. Remove Avalonia's Apply-time `RefreshPreviewNow` behavior.

The commit queue remains the final stale-state authority. A conflict keeps the tool open, preserves the script, explains staleness, and requires refresh. A repeated event or retry reuses the same transaction identity. Input controls and dismissal are disabled for the short applying state. Errors keep enough state to retry or correct the draft.

No valid subset can commit after a row error, timeout, or cancellation. A no-change result produces no history entry. Undo restores the complete previous snapshot. Redo restores the committed candidate without running Lua.

Automatically recalculating during Apply was rejected because the user might commit an unseen result.

### 6. Use one close and success lifecycle

Capture the expression, preset, and source metadata when opening. Cancel, window Close, and outer Escape discard the candidate and restore that parameter baseline without a second confirmation. Backdrop clicks do not dismiss the tool. Completion popups receive Escape first.

After a successful commit, both hosts close the expression surface. Preserve the chapter-table scroll and selected identity where possible. Return focus to the opener and report a localized change summary. Retain applied parameters for another explicitly opened operation. Parameters do not remain active export rules.

Use the existing narrow auxiliary-tool/session ports. Extend a focused close callback or lifecycle port if needed. Do not make the expression ViewModel depend on the main-window ViewModel. Avalonia must prevent background content actions while this review surface is active, using its existing host facilities. Web continues to use the shared modal lifecycle.

Ctrl/Cmd+Z inside the script editor stays within the draft. Enter inserts a newline. Background document shortcuts and file drops cannot modify the document through the active modal surface. No new global Apply shortcut is introduced.

### 7. Reuse localization and test real behavior

Add labels and plural/count templates to shared locale AXAML files. Generate Web JSON with the repository script. Use existing theme resources and accessible control labels. Announce result summaries, not the whole comparison after every keystroke.

Test the typed projection through public APIs with real snapshots. Test host state transitions and actual candidate commits. Use Avalonia Headless only in its dedicated process and collection. Web browser tests must interact with rendered controls and verify cancellation, history, focus, geometry, and hit targets. Screenshots supplement assertions.

## Risks / Trade-offs

- [Pending prerequisite specs contain older behavior] → Reconcile prerequisite deltas before this change is archived. Keep this change's shared capability additive.
- [Frame-only effects look like time edits] → Separate categories and explicitly test the screenshot-shaped case.
- [Detected frame rate affects broader metadata] → Show property ownership and complete before/after values.
- [A large candidate slows rendering] → Use virtualization or paging without truncating accessible results or counts. Verify 1,000 chapters.
- [IME and asynchronous results vary by host] → Exercise composition, revision changes, close races, and stale commits in both host suites.
- [A native tool window does not currently behave modally] → Verify the actual host lifecycle before wiring close and background isolation. Preserve typed host contracts.
- [Tiny time changes appear unchanged] → Raise display precision from the typed ticks and verify sub-millisecond examples.

## Migration Plan

1. Confirm prerequisite candidate and tool-host entry points. Reuse existing transaction tests.
2. Add the shared typed projection and focused tests.
3. Implement Avalonia state, layout, and lifecycle against the new projection.
4. Implement Web state, layout, and lifecycle against the same projection.
5. Add shared locale text and regenerate Web resources.
6. Run affected suites sequentially. Capture default, wide, and narrow evidence for both hosts.
7. Update code-map ownership and finalize the design record.

No persisted settings or document schema migration is required. Rollback can revert presentation and lifecycle wiring together without rewriting user documents. Candidate data remains temporary.

## Open Questions

No product decision blocks implementation. Option A is selected. Defaults are current-track scope, time units, changed-only filtering, close on success, and baseline restoration on cancel. The exact responsive breakpoint and accuracy labels must be resolved through layout checks and the current tolerance semantics during implementation.

## Implementation and Acceptance Evidence

- Core now exposes `ExpressionPreviewProjection` from immutable before and candidate documents. It compares complete persisted values, retains track order and scope, excludes separator chapters from expression counts, records property ownership, and calculates frame deltas only when frame bases match. Projection and candidate transaction coverage passes in `ChapterTool.Core.Tests`.
- Avalonia and Web consume the shared projection. Both surfaces keep empty input unapplied, mark old results stale, require an explicit refresh after conflicts, and apply the captured candidate without evaluating Lua again. Avalonia stores applied expression settings through a settings-only port. Web reports the committed change counts.
- Avalonia uses a modal native tool window with apply-time close protection, outer Escape routing, and a success close callback. Its Headless workflow covers live review, filters, frame values, cancellation, close, and one-step undo. Web uses the shared modal, and a small JavaScript bridge delays preview scheduling until IME composition ends.
- Screenshots are available at `artifacts/expression-preview/avalonia-default.png`, `avalonia-wide.png`, `avalonia-narrow.png`, `web-default.png`, `web-wide.png`, and `web-narrow.png`. Avalonia narrow layout uses stacked before/after/delta rows. Browser checks cover 320 px, 390×844, and 844×390 viewports. Long-result scrolling is covered by M05. Existing localization and theme workflows cover English, Chinese, and light/dark palettes. Physical mobile keyboard behavior was not tested on a device; browser WebKit composition coverage passed.
- Final sequential .NET checks passed: Core 740, Avalonia 261, Avalonia Headless 104, and Wasm 44 tests. Avalonia and Wasm builds passed with zero warnings and errors. Locale JSON generation and `--check` passed. The final Chromium and WebKit modal/expression runs each passed 15/15. An earlier full browser matrix exposed obsolete expectations and the missing composition bridge; those failures were fixed before the final focused runs.
- `openspec validate "unify-expression-preview-interaction" --strict` passed. The change updates `docs/code-map/core.md`, `avalonia.md`, and `testing.md` with ownership and test paths.

## Visual Review Follow-up

The user reported that the first implementation was hard to understand. Visual review found four causes. The editor and filters consumed too much space. Property changes appeared before the time comparison. The summary gave every category equal emphasis. The editor did not explain the time variable or its unit.

The first visual follow-up added numbered headings for editing and review. It explained `t` in seconds and gave the `t + 5` example. The primary summary stated how many chapter times would change. A secondary summary retained unique affected chapter counts, frame updates, and property changes. Property values followed the comparison in expandable details. The comparison emphasized candidate values. Web filters used checkboxes. The Web Apply button used the accent color.

Avalonia keeps the editor above an independent result scroll area. It reflows comparisons when the view width changes. Web uses more of the available portrait height. With the two-chapter fixture, the second chapter's changed time remains above the footer at 390×844. Both hosts retain track order and show frame-only entries. All property owners and before/after values remain available.

English and Chinese screenshots are in `artifacts/expression-preview/`. Each host has default, wide, and narrow images. Chinese filenames use the `-zh` suffix. Browser assertions verify visible changed values above the footer. Headless assertions verify before/after alignment and delta placement after resize. The Chinese rendered summary confirms that one changed chapter and two participating chapters are not reversed.

Follow-up checks passed: Avalonia 261, Avalonia Headless 105, and Wasm 44 tests. Desktop and Web builds reported zero warnings and errors. The locale generator check and browser TypeScript check passed. The modal and expression browser suite passed 46 tests across Chromium, Firefox, and WebKit. Two duplicate screenshot runs were skipped because Chromium captures the shared image set. The Firefox composition fixture now waits for listener readiness and sends a composition input event. Physical mobile keyboard behavior remains unverified.

Primary commands:

```text
dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore
dotnet test tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj --no-restore
dotnet test tests/ChapterTool.Wasm.Tests/ChapterTool.Wasm.Tests.csproj --no-restore
uv run --project scripts scripts/axaml-to-json.py --check
node node_modules/@playwright/test/cli.js test --config=playwright.config.ts expression-preview-screenshots.spec.ts unified-editing.spec.ts modal-layout.spec.ts
```

Run the browser command from `tests/ChapterTool.Wasm.E2E/` after publishing and preparing the site. After the final Apply color change, the screenshot and M02/M05 geometry checks passed seven tests against the updated CSS. Two duplicate screenshot runs were skipped.

## Compact Layout Follow-up

The user requested less space and less explanatory text. Both hosts now omit numbered headings, variable instructions, duplicate editor labels, repeated unchanged-time text, and normal footer guidance. The summaries use short count labels. Error, pending, stale, and applying states remain available. The editor keeps its accessible name.

Avalonia places scope and preset controls in one row. Its outer padding is 10×8 DIP. Chapter cards use 6 DIP padding and smaller gaps. Web uses a narrower dialog, smaller content gaps, and a footer with only the two actions during normal review. Touch targets retain their existing minimum size. Expandable details keep all property owners and before/after values.

English and Chinese default, wide, and narrow screenshots were refreshed in `artifacts/expression-preview/`. The desktop Headless suite passed 105 tests. The Avalonia suite passed 261 tests. Web publication and locale generation checks passed.

The compact browser run passed 31 tests across Chromium and WebKit. One duplicate screenshot run was skipped. The TypeScript check and strict OpenSpec validation passed. The run used the primary browser command above with --project=chromium --project=webkit.
