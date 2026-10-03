## Why

Atomic commits provide reversible state boundaries, but users still cannot recover previous edits or choose an alternate future after undo. This change adds a session-owned history tree that retains every committed state for the lifetime of the document session.

## What Changes

- Store immutable root state and reversible domain change sets in an in-memory history tree.
- Add undo, redo on the preferred path, alternate branch selection, and current-state navigation.
- Preserve every committed branch without count-, time-, or memory-threshold pruning.
- Keep history attached to the document session and expose operation descriptions for hosts.

## Capabilities

### New Capabilities
- `session-edit-history`: In-memory history tree, undo, redo, branch switching, and resource-failure behavior.

### Modified Capabilities
- None.

## Impact

Core session state, change-set storage, and history tests. Depends on `unified-editing-02-atomic-transactions`.
