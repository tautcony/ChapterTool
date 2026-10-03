## Context

Document values introduced in change 01 are immutable. Hosts still call editing services through several paths, so a shared commit boundary must be introduced without disabling existing actions.

## Goals / Non-Goals

**Goals:** Provide a single session mutation queue, atomic publication, typed outcomes, stale-token checks, cancellation, and retry safety.

**Non-Goals:** Add undo/redo history, migrate every content command, or change clip merge semantics.

## Decisions

- Add a Core session API that accepts a captured base token and a candidate-producing operation.
- Validate and allocate all commit metadata before swapping the current session state reference.
- Treat cancellation before the swap as cancellation and cancellation after the swap as an applied result.
- Add an identity-preserving adapter for non-structural `ChapterSet` edit results. The adapter SHALL reject row-count changes because it cannot infer stable identities for inserted or removed rows.
- Keep the existing editing commands usable. Change 04 owns routing every workspace content command through the transaction API.
- Make transaction IDs bind to a stable request fingerprint and cached result.

## Risks / Trade-offs

- [A command may bypass the new session during migration] → Keep current commands unchanged in this step. Change 04 owns complete command migration and its entry-point inventory.
- [Queue serialization may block UI work] → Keep candidate calculation outside the short atomic publication section and surface cancellation.
