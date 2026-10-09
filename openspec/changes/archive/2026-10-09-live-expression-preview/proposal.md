## Why

The unified editing model treats Lua expressions as one-time document operations. The Avalonia shell and WASM page still expose option-style controls, and the browser applies expression changes on input commit. Users need to see the candidate result while editing without changing committed chapters or history.

## What Changes

- Update expression input flows to refresh a read-only candidate preview as the user edits.
- Keep the main chapter grid, export preview, and save output on committed document values until the user applies the candidate.
- Replace persistent expression-enable behavior with explicit apply and cancel actions.
- Apply a valid expression candidate as one atomic, undoable document transaction in Avalonia and WASM.
- Keep expression diagnostics and preset selection connected to the live preview.

## Capabilities

### New Capabilities
- `wasm-expression-editing`: Defines the browser expression editor, live candidate preview, and explicit commit behavior.

### Modified Capabilities
- `avalonia-ui-shell`: Defines consistent live preview and explicit apply behavior for Avalonia expression inputs.
- `chapter-workspace-session`: Defines how expression drafts produce read-only previews and committed transactions.

## Impact

- Avalonia expression controls, main-window content-option preview, expression tool ViewModel, and Headless/unit tests.
- WASM expression controls, workspace candidate lifecycle, browser localization, and Playwright coverage.
- Existing expression presets, Lua engine, document candidate builder, transaction history, and export pipeline remain the shared policy and services.
