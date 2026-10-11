## Why

The browser UI still exposes an older workflow than the current Avalonia UI. Main-window options, candidate review, history inspection, and live settings behave differently even though both hosts share Core.

## What Changes

- Use current Avalonia behavior as the reference for portable workflows.
- Expose a discoverable load menu and the XML language, numbering, and template controls in the browser main window.
- Align compact inline candidate review and keep display-only changes separate from committed content.
- Align history inspection, operation details, filtering, and retained redo branch choices.
- Apply valid settings drafts live while keeping Save, Discard, and browser storage outcomes distinct.
- Align portable tool presentation and verify default, wide, narrow, short, and landscape layouts.
- Preserve browser-specific file input, downloads, clipboard, editor, and native dialog boundaries.

## Capabilities

### New Capabilities

- `wasm-main-window-parity`: Current Avalonia workflow parity in the browser toolbar, main options, chapter grid, and candidate review.
- `wasm-history-inspection`: Browser history inspection, detail filtering, alternate redo choices, and responsive navigation.

### Modified Capabilities

- `wasm-settings-shortcuts`: Valid settings drafts apply live without persistence; discard restores the entry runtime snapshot.

## Impact

Browser Razor components, workspace adapters, CSS, WASM behavior tests, browser E2E tests, and current code-map documentation change. Core remains the authority for candidates and history. No new runtime dependency or native browser capability is required.
