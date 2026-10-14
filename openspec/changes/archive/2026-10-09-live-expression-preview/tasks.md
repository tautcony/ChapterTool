## 1. WASM candidate workflow

- [x] 1.1 Add read-only expression candidate preparation, apply, cancel, and stale-base handling to the WASM workspace without executing drafts from export or row refresh.
- [x] 1.2 Add WASM workspace tests for live candidate preparation, invalid input, cancellation, one-transaction apply, stale candidates, and no expression reapplication after row edits or export.

## 2. WASM expression experience

- [x] 2.1 Replace the expression enable checkbox with a localized live-preview region, explicit Apply and Cancel actions, and preset-driven preview refresh.
- [x] 2.2 Debounce expression edits and ensure only the latest candidate and diagnostics are displayed.
- [x] 2.3 Add Playwright coverage for input preview without mutation, invalid scripts, cancel, apply, undo, committed export, and row edits after apply.

## 3. Avalonia expression experience

- [x] 3.1 Refresh the main-window expression candidate while editing and show readable differences and diagnostics without changing committed rows.
- [x] 3.2 Refresh the expression tool candidate while editing or selecting presets, remove the persistent apply checkbox, and apply or cancel the latest preview.
- [x] 3.3 Add unit and Headless tests for live preview, invalid scripts, cancellation, stale candidates, atomic apply, undo, and export from committed content.

## 4. Verification

- [x] 4.1 Run the focused WASM unit and browser E2E checks, then the focused Avalonia unit and Headless checks sequentially.
- [x] 4.2 Validate the OpenSpec change with `openspec validate "live-expression-preview" --strict` and report any remaining mismatch.
