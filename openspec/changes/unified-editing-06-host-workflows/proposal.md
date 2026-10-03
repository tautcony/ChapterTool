## Why

Core history and migrated operations need consistent controls in both interactive hosts. Users must be able to undo, redo, inspect branches, understand session-only history, and keep text-control drafts separate from document edits.

## What Changes

- Connect Avalonia and browser workspaces to the same Core document session and command queue.
- Add undo/redo commands with operation names, preferred redo, branch selection, and a virtualized history panel.
- Route platform shortcuts while preserving text editing undo and accessibility/focus behavior.
- Show draft, candidate, applied, failed, and session-history lifetime status distinctly.

## Capabilities

### New Capabilities
- `host-history-workflows`: User-facing history, branch navigation, keyboard scope, and host parity.

### Modified Capabilities
- None.

## Impact

Avalonia ViewModels, XAML, shortcut catalog, Wasm workspace/page/browser guards, localization, and UI tests. Depends on `unified-editing-05-clip-operations`.
