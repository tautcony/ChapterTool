---
name: wfa-harden-decompose
description: >
  Phase G of a WinForms-to-Avalonia migration: decompose oversized UI state and orchestration,
  harden asynchronous workflows and external boundaries, preserve composition lifetimes, keep
  Headless tests process-isolated, and update ownership documentation. Use only when an explicit
  migration has post-port structural or resilience debt, or when the orchestrator routes here.
  Baseline input security belongs in the phase that introduces the boundary, not only here.
---

# Phase G — Harden and decompose

Keep the Avalonia trunk **secure and maintainable** after features accumulate.
Behavior-preserving structural changes preferred; ship large work as ordered PR slices.

Read [decomposition checklist](references/decomposition-checklist.md) to select the tracks
supported by code evidence; do not introduce a session/workspace abstraction by default.

## Track G1 — Typed state aggregate, when warranted

```text
Composition root
  └── Main ViewModel (thin: commands + bindable projections)
        └── State aggregate
              ├── Domain/application state
              ├── Interaction mode (typed, not flag soup)
              ├── Pending changes
              ├── Derived presentation state
              └── Operation preferences
  └── Workflow coordinators
  └── Narrow collaborator ports
```

- Mode transitions as pure functions where possible
- Bindings own options; remove scrape-before-command
- Route UI actions by stable identifiers, not localized display text
- Collaborators do not take the full main ViewModel when a narrow port suffices
- Shared application behavior across every relevant surface and host

### Anti-stale async

| Rule | Behavior |
| --- | --- |
| Revision | Monotonic; superseding work advances it |
| Progress | Apply only if revision current |
| Commit | Atomic if revision/context still match |
| Incremental update | Revision + context token match |
| Cancel | No partial mutation |
| Stale failure | Must not clobber newer state |

Require regression tests when touching this.

## Track G2 — Extract workflows

Move multi-step orchestration out of the ViewModel:

| Collaborator | Owns |
| --- | --- |
| Primary workflow coordinator | async state machine + revision |
| State transition service | selection/mode and structural changes when present |
| Presentation projection | derived view state and option snapshots |
| Status presenter | status, progress, and diagnostic formatting |

ViewModel remains INPC façade. **Half-extraction ban:** extracted modules must be wired or deleted.

Expect bridge/proxy lines to remain (bindings need VM properties).

## Track G3 — Command surface

Keep one command semantics on the ViewModel; code-behind contains only control/host adapters.

## Track G4 — Composition lifecycle

Shared long-lived services; identity tests if needed; controls re-bind when services arrive
(not only constructor fallbacks for production hosts).

## Track G5 — Headless isolation

- Dedicated Headless project (separate process)
- Serial collection inside Headless
- Never merge into unit assemblies “to simplify”
- Behavior tests; avoid long sleeps
- Sequential multi-project `dotnet test`

## Track G6 — External-boundary security review

- Recheck size, depth, count, recursion, and allocation bounds before consuming untrusted data.
- Deny external resource access unless explicitly required and constrained.
- Constrain dynamic execution by capabilities, time, memory, cancellation, and isolation.
- Use compact negative fixtures for each boundary actually present.

## Track G7 — Alignment docs

- Spec scenario ↔ implementation matrix after large moves
- Remediation: finding → design → steps → tests → acceptance
- Maintainer code map updated in the same change
- **Three-way gate:** plan item ↔ OpenSpec task ↔ code/tests/command evidence before checking off
- Prefer plan review **before** apply and **after** structural slices

## Deliverables

- [ ] Typed state aggregate if flag soup existed
- [ ] Anti-stale tests
- [ ] Real workflow ownership; dead modules gone
- [ ] Headless isolation intact
- [ ] Security bounds tested
- [ ] Ownership map updated

## Handoff

Product features → `wfa-post-port-evolve`
Legacy delete → `wfa-parity-verify`
