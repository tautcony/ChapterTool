## 1. Cutover

- [x] 1.1 Inventory every former direct mutation, projection, clip-backup, preview, and export consumer; map each to a session transaction and behavior test.
- [x] 1.2 Remove obsolete interactive projection state, duplicate editable chapter collections, and direct mutation paths.
- [x] 1.3 Keep and document only the stateless compatibility adapters defined in change 08.
- [x] 1.4 Update `docs/code-map/core.md`, `avalonia.md`, `contracts.md`, and `testing.md` for new ownership and test locations.
- [x] 1.5 Synchronize the changed workspace and transform/export specs. The change remains active and is not archived.

## 2. Completion evidence

- [x] 2.1 Verify generated full-state edits, deep history, branch navigation, injected resource failures, atomic publication, and stale async operation outcomes.
- [x] 2.2 Verify session release and export snapshot retention through `ChapterWorkspaceTests`.
- [x] 2.3 Record allocation, retained heap, build time, undo, and distant-branch timing for increasing document sizes and branch counts in `history-measurements.md`.
- [x] 2.4 Run the full solution, Node.js package checks, and OpenSpec validation. Record browser and environment limitations in `verification.md`.
- [x] 2.5 Capture and review default, wide, and narrow UI evidence. Record screenshot paths and review notes in `verification.md`.
