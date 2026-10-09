## 1. Replacement lifecycle

- [x] 1.1 Add session-end token invalidation, cancellation, event unsubscription, and host reference release.
- [x] 1.2 Stage imports and replacement documents before confirmation and recheck revision before swapping sessions.
- [x] 1.3 Add localized close/reload/replace confirmation based on active history, drafts, and unexported edits.

## 2. Export baseline and lifecycle evidence

- [x] 2.1 Capture export snapshots and update per-track digest/state/format baseline only after successful output.
- [x] 2.2 Add tests for canceled/failed replacement, stale load/append/export, new-session isolation, draft preservation, and collectible ended sessions.
- [x] 2.3 Add tests proving export retains only a content snapshot and never clears history.
- [x] 2.4 Run affected Core, Avalonia non-Headless, Headless, and Wasm tests sequentially with `--no-restore`.
