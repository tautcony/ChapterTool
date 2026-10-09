## Context

The Core already builds immutable `ChapterContentPreview` candidates and applies a valid candidate through one history transaction. Avalonia uses this path after a manual Preview action. Its expression tool also has preview, apply, and cancel commands. The WASM page still executes expression transforms when the checkbox changes or the input commits. Neither expression surface refreshes a candidate while the user edits.

The main chapter grid and exports must show committed document values. Expression text is an operation draft. Preview data must remain separate from the editable document.

## Goals / Non-Goals

**Goals:**

- Refresh a read-only expression candidate preview while the user edits text or selects a preset.
- Show changed chapter values and current validation errors in Avalonia and WASM.
- Apply only the latest valid candidate through the existing atomic history transaction path.
- Keep the main chapter grid, format preview, and export on committed document values until apply succeeds.
- Make expression application explicit. Remove the persistent apply checkbox from expression flows.
- Preserve syntax highlighting, completion, presets, script loading, diagnostics, undo, and redo.

**Non-Goals:**

- Execute an expression on every chapter edit or during export.
- Add a second editable chapter document or mutate the main grid during preview.
- Change Lua evaluation policy, expression context, or Core candidate validation.
- Add a WASM syntax colorizer or completion system in this change.

## Decisions

1. **Use the shared candidate preview contract.** Avalonia will call its content-operation port to prepare expression candidates. WASM will prepare against the active content-session snapshot and use the existing candidate builder and transaction APIs. Both hosts will show differences from `Before` and `Candidate`.

2. **Refresh after a short input debounce.** Text edits and preset changes schedule a preview refresh. A newer edit cancels the prior scheduled refresh and replaces its candidate. The operation reads one immutable snapshot. It does not update rows, history, or export state.

3. **Keep the preview separate from the grid.** The preview region will list changed values and validation messages. The main grid remains the committed document. Invalid candidates show diagnostics and disable Apply. A no-change candidate reports that no values will change.

4. **Apply commits the latest candidate.** Apply uses the preview's captured base token and preview ID. If the document changed after preview, the host discards the stale candidate and prepares a new preview. A successful apply creates one undoable transaction. Undo and redo operate on the committed candidate and do not rerun Lua.

5. **Cancel discards only preview state.** Cancel clears the candidate and its displayed differences. It does not change the document or history. The expression text remains available for further editing; the next text or preset change prepares a new preview.

6. **Remove persistent expression toggles.** A non-empty expression is an unapplied draft until the user applies it. Preset selection populates the draft and starts preview. Editing preset text clears the selected preset identity while retaining the edited script.

7. **Keep export bound to committed content.** Format preview and save use the current document. They never evaluate the expression draft. Once applied, the resulting chapter values are exported as stored.

## Risks / Trade-offs

- **Lua evaluation may be expensive for large documents** → Debounce editor changes and show the most recent completed preview only.
- **A concurrent edit may stale a preview** → Check the captured base token during apply and refresh instead of committing stale data.
- **The two hosts may drift in wording or state transitions** → Cover the same edit, invalid, cancel, apply, undo, and export scenarios in Avalonia and Playwright tests.

## Migration Plan

No data migration is required. Remove the UI checkbox behavior and stop routing expression drafts through output projection. Existing expression text and presets remain available. Roll back by restoring the prior controls and handlers; Core history and document formats remain compatible.

## Open Questions

None.
