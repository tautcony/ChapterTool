## Why

The interactive application currently carries editable chapters, clip backups, projection rules, and exported output through separate state paths. This change establishes one immutable document snapshot as the source for current content and gives later transactions a stable, precise contract.

## What Changes

- Add an immutable document model for ordered tracks, chapters, metadata, duration, frame rate, numbering, and segment boundaries.
- Add stable document, track, and chapter identities and deterministic ticks/frame conversion rules.
- Adapt imported chapter sets into a document without retaining a second editable copy.
- Make a captured document snapshot the input to pure format serialization.

## Capabilities

### New Capabilities
- `editable-chapter-document`: Immutable document snapshots, stable identities, time semantics, and serialization input.

## Impact

Core models, import adapters, export services, and their public tests. Host editing remains on its current path until subsequent changes migrate commands.
