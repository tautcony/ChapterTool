## Context

`ChapterSet` is currently shared across import, editing, transforms, and export. The target design needs an immutable document boundary while preserving existing pure services during migration.

## Goals / Non-Goals

**Goals:** Introduce immutable document value types, stable identities, exact time metadata, and a snapshot-based serializer boundary.

**Non-Goals:** Route all UI edits through transactions or add history in this change.

## Decisions

- Keep existing `ChapterSet` services as pure adapters during migration. Convert at the boundary and do not keep it as session state.
- Store chapter times as `long` ticks and rational frame rates. Convert legacy floating-point values once when adapting input.
- Give document, track, and chapter IDs explicit value types. Preserve IDs through value-only edits; allocate IDs in candidate construction for new structures.
- Make the serializer accept a captured document-derived value and format options only. Move interactive transforms out of serialization before changing host wiring.

## Risks / Trade-offs

- [Legacy formats have ambiguous duration semantics] → Preserve an explicit unknown duration and add format-specific adapter cases.
- [Public Core contracts may be consumed by CLI or Node.js] → Keep compatibility adapters until change 08 and verify existing API behavior.
