## Context

Changes 01-08 establish the current document, atomic edit kernel, complete history tree, operation and clip migration, both hosts, session lifecycle, and stateless compatibility boundaries. Old projection and direct mutation paths must now be retired.

## Goals / Non-Goals

**Goals:** Complete the cutover, remove obsolete interactive state, update ownership documentation, and establish final behavior/resource evidence.

**Non-Goals:** Introduce new product capabilities or make history persistent.

## Decisions

- Remove a legacy path only after searching its runtime consumers and proving every active workflow reaches the Core session.
- Preserve stateless Core/CLI/Node compatibility adapters from change 08, but keep them outside interactive workspace state.
- Update current code maps and specs in the same change as ownership and public behavior cutover.
- Validate full-solution .NET behavior, Node/browser workflows, large-history behavior, session release, and responsive UI evidence.

## Risks / Trade-offs

- [A dynamic consumer may still use a legacy API] → Inventory public API consumers and add behavior coverage before deletion.
- [Large-history tests cannot prove infinite physical memory] → Report scale and environment, verify no pruning policy, and test recoverable failure injection.
