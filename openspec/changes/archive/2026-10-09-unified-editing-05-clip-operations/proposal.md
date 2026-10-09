## Why

Clip combine and restore currently retain parallel chapter content, which conflicts with one current document and loses edits made after combining. This change makes merge, split, and append edits to the current structure and records them in the same history tree.

## What Changes

- Merge selected current tracks into one track while preserving names, chapter identities, explicit ends, source metadata, and segment layout.
- Replace restore-from-backup with splitting the current merged track at its current segment boundaries.
- Make append a candidate based on the current merged content, allocate new identities, and reject stale asynchronous results.
- Keep merge undo separate from split, with both represented as reversible transactions.

## Capabilities

### New Capabilities
- `clip-structure-editing`: Transactional merge, split-by-boundaries, and append over current document content.

### Modified Capabilities
- None.

## Impact

Core segment/session services and Avalonia/Wasm append and clip commands. Depends on `unified-editing-04-content-operations`.
