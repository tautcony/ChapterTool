## Context

The session owns history and UI drafts, while import, append, and export can complete asynchronously. A new document must not inherit stale results or baselines from the old one.

## Goals / Non-Goals

**Goals:** Make app-initiated session loss explicit, isolate asynchronous work, release old session references, and report export state for the actual captured content.

**Non-Goals:** Persist history, prevent forced browser refresh, or add cross-process file locking.

## Decisions

- Stage import and session creation off to the side. Confirm loss only after a usable replacement candidate exists, then recheck the old session token.
- Use document identity and runtime generation tokens for every async result. Invalidate and cancel them when a session ends.
- Derive per-track exported state from canonical content digest, format options, and captured state ID.
- Allow an export to own its immutable document snapshot only; it must not retain the history tree or update a later session.
- Verify release through weak references and controlled collection, not immediate working-set reduction.

## Risks / Trade-offs

- [Close confirmation can race with edits] → Recheck revision at the final replacement boundary.
- [Digest fields may omit format-relevant data] → Define canonical serialization inputs and cover each format with public behavior tests.
