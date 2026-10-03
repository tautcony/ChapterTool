## Why

The document and history kernel only helps users when normal editing actions create reversible commits. This change routes cell edits and content tools through candidate computation and one atomic history transaction.

## What Changes

- Migrate cell editing, insert/delete, move/order, numbering, metadata, template naming, regular-expression replacement, offsets, frame-rate actions, and expressions.
- Capture explicit target IDs and a base token before asynchronous computation.
- Validate the full candidate and show before/after differences before applying one transaction.
- Make tools one-shot operations; retained tool parameters do not remain active output rules.

## Capabilities

### New Capabilities
- `unified-content-editing`: Reversible edits, explicit candidate previews, and atomic application of all non-clip content operations.

### Modified Capabilities
- None.

## Impact

Core editing/transform adapters, Avalonia and Wasm command wiring as needed for applying candidates, and behavior tests. Depends on `unified-editing-03-history-tree`.
