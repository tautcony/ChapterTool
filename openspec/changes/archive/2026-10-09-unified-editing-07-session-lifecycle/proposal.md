## Why

History belongs to one live document session. Replacement, reload, close, asynchronous import, and export must not move old work into a new session or falsely mark new edits as exported.

## What Changes

- Build replacement sessions before confirming and swapping them into the host.
- Confirm application-initiated session loss when history, drafts, or unexported edits exist.
- Cancel old session tasks and release history, drafts, previews, subscriptions, and UI node projections.
- Track export baselines per track using captured state, options, and content digest.

## Capabilities

### New Capabilities
- `document-session-lifecycle`: Session replacement, release, user confirmation, async isolation, and export baselines.

### Modified Capabilities
- None.

## Impact

Core session lifecycle, Avalonia and Wasm load/reload/close/export orchestration, and lifecycle tests. Depends on `unified-editing-06-host-workflows`.
