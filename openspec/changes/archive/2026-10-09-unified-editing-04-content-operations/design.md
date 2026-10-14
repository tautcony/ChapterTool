## Context

The current tool surfaces include table edits, templates, regular expressions, frame operations, and expressions. Some currently produce projected output rather than current content.

## Goals / Non-Goals

**Goals:** Make every non-clip content operation an atomic, reversible document edit with preview and explicit target scope.

**Non-Goals:** Redesign merge, split, and append semantics; build the history panel; or add disk-backed history.

## Decisions

- Introduce typed candidate results per operation and adapt existing pure services into candidate builders.
- Capture target IDs, document identity, and mutation revision before computation. Reject stale output rather than relocating targets.
- Keep tool parameters as drafts/preferences only. Commit their calculated document values once.
- Route Avalonia content operations through the active `ChapterWorkspace.ContentSession`. Batch operations expose a typed preview, apply, and cancel flow. Single-cell edits commit on the editor's existing Enter, Tab, or valid focus-loss path.
- Keep WASM host wiring and the full history panel/branch chooser in change 06. Core candidate APIs in this change remain host-callable and testable.
- Preserve separate draft undo behavior in text controls and document history behavior outside active text editing.
- Return structured validation diagnostics. Never convert invalid input or partial batch output into a successful candidate.

## Risks / Trade-offs

- [Existing tools have different error policies] → Normalize them at candidate adapters and add one failure scenario per operation family.
- [Migration can leave direct writes] → Track each existing operation in task inventory and require one history round-trip workflow per operation family.
