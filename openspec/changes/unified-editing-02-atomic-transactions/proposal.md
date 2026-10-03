## Why

The immutable document needs one commit boundary before user operations can be safely unified. This change introduces session identity, revision checks, cancellation, idempotency, and all-or-nothing candidate application while keeping the current edit commands usable.

## What Changes

- Add one Core document session state with current document, state identity, mutation revision, and command queue.
- Add atomic candidate validation and commit results for success, no change, conflict, cancellation, and resource failure.
- Deduplicate retries by transaction identity and reject reuse with a different request.
- Add a stable-ID-preserving adapter for non-structural results from the existing `ChapterSet` editing API. Keep existing workflows usable. Route every workspace content command in change 04.

## Capabilities

### New Capabilities
- `atomic-edit-transactions`: Serialized, atomic document mutations and stale-operation rejection.

### Modified Capabilities
- None.

## Impact

Core session and editing adapters, and Core/session tests. Depends on `unified-editing-01-document-model`.
