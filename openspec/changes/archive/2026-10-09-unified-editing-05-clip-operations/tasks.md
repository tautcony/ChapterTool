## 1. Clip candidates

- [x] 1.1 Replace parallel editable clip backups with track boundary and placement metadata.
- [x] 1.2 Implement merge over current document values with identity maps, preserved ends/names/source metadata, and frame-rate policy.
- [x] 1.3 Implement split-by-boundaries over current values, interval edge rules, structure-row routing, and cross-boundary diagnostics.
- [x] 1.4 Implement append candidate construction with fresh identities, stale-token checks, and atomic publication.

## 2. Host workflow and verification

- [x] 2.1 Update combine/restore affordances to merge/split semantics without removing available clip workflows.
- [x] 2.2 Add Core round-trip, edit-before-split, end-conflict, nested-boundary, identity, append, cancellation, and stale-session tests.
- [x] 2.3 Run Core and affected Avalonia ViewModel/Headless test projects sequentially with `--no-restore`.
