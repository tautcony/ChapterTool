## Context

Change 02 provides atomic session publication. The required history is an in-memory tree, not a bounded undo stack, and it must avoid copying the complete document for each small edit.

## Goals / Non-Goals

**Goals:** Retain all branches, restore states exactly, keep revisions monotonic, and make change-set cost proportional to actual edits.

**Non-Goals:** Persist history across application or browser restarts, add history import/export, or add checkpoints as a separate subsystem.

## Decisions

- Keep an immutable root snapshot and reversible domain change sets at each edge. Store forward and inverse changes.
- Record a preferred child at each branch point. Expose other children for explicit redo selection.
- Reconstruct a target state privately, then publish it through the transaction kernel's atomic state swap.
- Use iterative traversal for deep histories. Do not prune nodes automatically.
- Increment mutation revision on undo, redo, and branch changes even when the state identity returns to an earlier value.

## Risks / Trade-offs

- [Memory grows with edits and retained branches] → Store deltas, share immutable payloads, expose current-session scope, and report recoverable allocation failure without deleting history.
- [Inverse changes can be incorrect] → Compare complete canonical document snapshots and identities in deterministic and property-based sequence tests.
