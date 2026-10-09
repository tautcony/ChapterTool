## Approved shared corrections

The continuation request approves corrections found by matching host fixtures.
Both hosts must derive frame text without committing display preferences.
Frame edits and signed shifts must use the displayed FPS.
Expression preparation may supply detected FPS to the selected track when source FPS is absent.
It must preserve other tracks.
Frame conversion must quantize each absolute segment boundary through the chapter conversion calculation.
The converted duration must equal the converted end minus the converted start.
Source frame-rate metadata must remain unchanged.

## Context

`ChapterTool.Wasm` uses Blazor components. `ChapterTool.Avalonia.UI` uses Avalonia views and ViewModels. Both hosts use the Core `ChapterWorkspace` and content history. Web already references Core and Contracts. It does not reference Avalonia.UI or Infrastructure.

The current Web expression dialog already reviews a candidate before application. Other Web content operations do not use that interaction. `Home.razor` calls `WasmWorkspace.ApplyOptionsAndRefresh` for naming changes, rounding changes, advanced options, and settings application. That method commits naming and numbering candidates. Its `applyExpression` argument is false. It does not rerun Lua.

Web frame-rate conversion and frame shift also commit immediately. The frame column is read-only. Web uses fixed shortcut routing and a basic retained log list. Its settings modal discards changes on close.

The completed changes `unified-editing-04-content-operations`, `unified-editing-06-host-workflows`, `unified-editing-07-session-lifecycle`, `fix-wasm-modal-layout`, and `unify-expression-preview-interaction` remain unarchived. This design uses their implemented contracts. It must not edit their artifacts or assume their specs are already present in `openspec/specs/`.

### Difference and acceptance checklist

| ID | Priority | Current difference | Planned outcome | Primary evidence |
| --- | --- | --- | --- | --- |
| D01 | P0 | Web naming and numbering commit from option changes | Draft, reviewed candidate, explicit apply/cancel | Workspace and browser workflow tests |
| D02 | P0 | Settings can reapply content drafts | Preferences never commit chapter content | Rename a generated chapter, save appearance, compare history and export |
| D03 | P0 | Web frame-rate conversion commits immediately | Review source/target rates and candidate before commit | Cross-host conversion, cancel, stale candidate, undo |
| D04 | P1 | Web shift permits only 1..100000 | Signed -1000000..1000000 draft with validation and review | Valid negative shift, invalid negative time, positive limit |
| D05 | P1 | Web frames are read-only | Frame edit changes time through Core | Time/frame/export equality and one-step undo |
| D06 | P1 | Avalonia accepts ineffective negative numbering input | Both hosts expose 0..1000 offsets | Core result plus rendered bounds and validation |
| D07 | P1 | Web lacks Lua file and editor assistance | Script load, highlighting, completion, positioned diagnostics | Actual editor input and script-loading workflows |
| D08 | P1 | Web shortcuts are fixed and incomplete | Catalog-based settings and routing | Customized mapping, conflicts, row actions, clip navigation |
| D09 | P1 | Web settings close discards drafts | Save, explicit discard, and keep-editing lifecycle | Close button, Escape, validation, persistence failure |
| D10 | P2 | Web logs expose only summaries and clear | Search, severity, explicit inspector, copy, filtered export | Membership and rendered inspection workflows |
| D11 | P2 | Web advanced options use codes and obscure content intent | Localized labels and separate preference/content surfaces | Locale changes, enabled states, accessible labels |
| D12 | P2 | Web input lifecycle and table behavior are incomplete | Explicit commit/cancel, stable identity, reachable large tables | Enter/Tab/Escape, focus, selection, 1000-row workflow |

## Goals / Non-Goals

**Goals:**

- Match committed editing outcomes and explicit confirmation behavior across hosts.
- Preserve one document owner, one history tree, and Core validation.
- Make settings, output preferences, display preferences, and content operations distinct.
- Close the portable editor, shortcut, and log gaps.
- Preserve existing Web modal focus, IME, and responsive behavior.

**Non-Goals:**

- Resolve native tools, media import, BDMV directories, local save paths, shell access, system fonts, or telemetry.
- Change browser download acknowledgement, document export baselines, or page-leave handling.
- Replace the Web UI framework or require identical layouts.
- Add a persistent chapter history, a new script language, or negative numbering semantics.
- Implement speculative grid virtualization before a measured workflow requires it.

## Decisions

### 1. Reuse the Core candidate boundary for every reviewed operation

Core keeps ownership of naming, numbering, frame shifts, frame conversion, validation, and transactions. Web must expose narrow prepare/apply/cancel operations over the existing candidate APIs. It must retain the exact reviewed candidate and its base identity. Application must not recompute a candidate.

Reuse the expression dialog state rules for unavailable, draft, computing, invalid, unchanged, ready, stale, applying, and failed states. Extract only presentation that has a real second consumer. Naming/numbering and timing operations can use a common review component. Operation-specific parameters and summaries must remain explicit.

The default scope is the current track. A combined clip uses its current combined track. Separator rows do not participate as ordinary chapters. Scope and excluded rows must be visible. A scope or document change invalidates the old candidate. A rejected application must preserve the draft.

Alternative: Retain immediate commits and rely on Undo. This does not provide the desktop review/cancel workflow or prevent settings from committing content.

### 2. Separate preference changes from one-shot content drafts

`Home.razor` and `WasmWorkspace` must stop calling content operations from settings and display handlers. Format, XML language, encoding, and BOM update export preferences. Rounding, frame precision, and tolerance update derived display. Language, theme, fonts, and shortcuts update presentation or input preferences.

Move naming, templates, and numbering to an explicit content operation surface. The compact naming control can remain as a draft selector. It must not imply a persistent export rule. Advanced export options must contain output preferences. A template picker can remain near naming controls, but loading a template must only prepare a draft.

After a successful naming/numbering application, clear the active one-shot intent. Retained template text can remain available for deliberate reuse. Saving preferences must not replay it. Expression parameters keep their existing deliberate-reapply behavior.

Startup applies saved default format. Saving settings during an active session must preserve the selected session format, as required by `chapter-workspace-session`. Setting changes can alter derived frames or serialized encoding without changing document content or its history cursor.

Alternative: Add guards to each existing `ApplyOptionsAndRefresh` caller. This leaves a method that combines unrelated effects and makes future accidental commits likely.

### 3. Use existing Core numeric semantics

Both hosts must expose numbering offsets from 0 to 1000. Zero is the neutral draft value. This change does not add negative chapter numbering. Avalonia must reject ineffective negative input instead of advertising it as supported. Core callers keep the existing normalization policy.

Frame shift accepts signed integers from -1000000 to 1000000. Zero produces an unchanged preview. Core validates each result, including negative times and overflow. UI range checks do not replace Core validation. Both hosts use the same FPS basis and current-track scope.

Alternative: Support negative numbering because Avalonia accepts it today. Core clamps it to zero and the current model requires valid chapter numbers. That would require a separate semantic change.

### 4. Make browser cell edits identity-based drafts

Capture chapter ID, field, original value, and base version at edit start. Reuse Core `EditCell` with `ChapterCellField.Frame` for frame input. Reuse the same parsing and timing conversion as Avalonia. Enter, Tab, and valid focus loss commit once. Escape restores the original draft without a history node. Invalid input stays visible with an accessible error.

Text-control Undo must remain local to the draft. Document Undo uses committed content when the table owns focus. Commit handlers must not use a row index after history or clip changes. Preserve selected chapter identities and focus where those chapters still exist.

Use a 1000-row rendered workflow to assess scrolling and edit reachability. Add paging or virtualization only if the workflow fails or measured rendering is unusable. Do not add desktop column resizing as a prerequisite for functional parity.

### 5. Reuse Core Lua authoring services through browser presentation

Use `LuaExpressionScriptService` for script rules and `ExpressionAuthoringService` for diagnostics and completion. Read a selected `.lua` file through the existing bounded browser file-input pattern. A failed read or parse must preserve the current script, preset, source metadata, document, and history.

Add a browser editor adapter for caret, selection, completion, diagnostic ranges, and highlighting. Keep editor state inside the expression component. Preserve multiline text, accessibility, composition handling, debounce, and local text Undo. Completion consumes its keyboard events before the dialog does.

Use a small browser presentation over existing authoring results. A new large editor framework is not required by this plan. If implementation proves one necessary, record its size and accessibility impact before adding it.

Alternative: Implement a separate browser Lua parser and completion catalog. This duplicates Core semantics and creates different diagnostics across hosts.

### 6. Extend browser settings with shared shortcut data

Reuse `ShortcutCatalog`, `ShortcutSettings`, `ShortcutGestureText`, and `ShortcutConflictValidator`. Keep canonical action IDs and gesture text in Contracts. A browser adapter maps keyboard events to those values. Web must not depend on Avalonia `KeyGesture`.

Add a `shortcuts` child to the browser settings model. Treat missing shortcut content in existing version-one documents as defaults. Preserve existing application, theme, and font content. Do not introduce another storage key or write settings during load. Invalid stored overrides must retain usable defaults. Invalid drafts must remain visible and block Save.

Use one active mapping for dispatch, visible hints, and browser-default suppression. Preserve the existing fixed aliases. Route Insert/Delete and PageUp/PageDown through their existing catalog actions. A modal or editable target prevents background commands. Browser-reserved and undeliverable gestures need localized validation. Existing supported browser-intercepted defaults remain compatible. This work does not change native browser APIs or page reload policy.

Alternative: Extend the current independent C# and JavaScript hard-coded key lists. They can diverge from saved mappings and displayed hints.

### 7. Define settings draft close and save behavior

Compare normalized draft values with the entry settings snapshot. Save validates all settings, persists once, then activates the saved snapshot and closes. Failure keeps the dialog, draft, and previous valid runtime settings. Intentional Cancel discards and closes. Close or Escape with changed values asks whether to discard or keep editing. Reset changes the draft only.

Shortcut edits and output defaults must use this lifecycle. A nested discard confirmation must restore focus to the settings surface. Settings actions must not create document history entries.

Alternative: Automatically save or silently discard all close paths. Neither matches a reviewable settings draft lifecycle.

### 8. Extend the Web log projection without importing desktop infrastructure

Keep the existing bounded browser log source. Add stable entry identity and a browser-owned filtered projection. Search must include every field the browser entry actually contains, including details. The inspector must not invent desktop exception or structured fields that the browser did not record.

Use list-first presentation, explicit details, severity filters, copy, and a secondary JSON/CSV export action. Export captures the visible filtered membership before reporting success or failure. Use UTF-8 and the existing browser download adapter. CSV must quote separators, quotes, and newlines correctly. This action does not change the chapter export baseline or desktop log archives.

Keep the inspector beside the list at wide widths. Replace the list with a back action at narrow widths. Eviction or filtering must not silently select another entry. Clear closes an unavailable inspector and empties the retained source.

Alternative: Reference Infrastructure or Avalonia log ViewModels from Web. That adds host dependencies without sharing the browser boundary.

### 9. Verify behavior across hosts and keep layout adaptations

Use the same fixtures and expected content for naming, numbering, frame edit, signed shift, frame conversion, and expression application. Verify full relevant document fields and history outcomes through public APIs. Compare exported bytes for the same preferences. Do not test implementation source text.

Use Web workspace tests for state, Playwright for browser interaction, Avalonia unit tests for reference outcomes, and Headless tests for the numbering control and existing reference workflows. Run test projects sequentially. Headless remains in its own process.

Keep history in a Web dialog and content review in modal surfaces. Use shared locale AXAML sources and generated Web JSON. XML language selectors must expose localized names while storing stable language codes. Default, wide, narrow, narrow-short, and landscape browser checks must verify actual reachable controls. Screenshots supplement behavior checks.

## Risks / Trade-offs

- Existing users expect naming options to commit immediately. Mitigation: Label the draft action and show a clear preview/apply path.
- New cell handlers can commit twice on Enter followed by blur. Mitigation: Bind one edit identity and test exactly one history node.
- An operation can become stale during review. Mitigation: Check session, track, draft revision, and base version at application.
- Shortcut capture can interfere with text editing or browser defaults. Mitigation: Use one mapping and test editable and modal contexts in each supported engine.
- Lua assistance can interfere with IME or Escape. Mitigation: Preserve composition tests and give completion first ownership of Escape.
- Browser storage can reject a settings save. Mitigation: Keep the last valid runtime snapshot and offer retry without content changes.
- Negative numbering looked supported on desktop. Mitigation: Document the existing Core policy and verify explicit UI rejection.
- Log retention can remove an inspected entry. Mitigation: Use stable identity and a deterministic unavailable-entry transition.
- Completed upstream specs are not synchronized yet. Mitigation: Do not duplicate or rewrite them; validate the new change independently and preserve their behavior tests.

## Migration Plan

1. Add behavior regressions for preference isolation and candidate review.
2. Implement content-operation parity before exposing more editing actions.
3. Add table edits and Lua authoring without changing the expression review contract.
4. Add shortcut settings and draft lifecycle with additive version-one compatibility.
5. Add the browser log tool and localized option presentation.
6. Run the affected checks and capture supported layouts under `artifacts/`.
7. Update code-map owners and capability boundaries. Review the completed implementation before any spec sync or archive.

Rollback can revert the implementation without migrating chapter documents. Older browser versions ignore the new shortcut child. Existing version-one output, theme, and font settings remain readable. This change does not archive itself or any prerequisite change.

## Open Questions

No product-scope decision blocks implementation. The design uses the existing Core nonnegative numbering policy. Platform differences are deferred by the user. Implementation must measure the browser table and editor before choosing additional rendering dependencies.
