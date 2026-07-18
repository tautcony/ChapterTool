---
name: wfa-post-port-evolve
description: >
  Phase F of a WinForms-to-Avalonia migration: evolve the Avalonia trunk after cutover without
  reintroducing WinForms coupling or bypassing established layers. Use only when an explicit
  migration has an active Avalonia trunk and a concrete product backlog, or when the
  orchestrator routes here. Select work from repository evidence; do not assume particular
  entry points, integrations, settings, or extension mechanisms exist.
---

# Phase F — Post-port product evolution

Improve the Avalonia line **without reintroducing WinForms coupling**. First ship (A–E) is
not the end.

Read [evolution tracks](references/evolution-tracks.md) to classify a discovered backlog item;
do not implement tracks that are absent from the product.

## Preconditions

- Avalonia is the active trunk (or dual-running with legacy frozen)
- Layering exists
- Prefer one SDD/spec change per capability

## Tracks (select only from repository evidence)

### F1. Deferred capability restoration

Restore a deferred behavior in its owning layer with contract and regression evidence. Do not
place application rules solely in a ViewModel because that is the easiest entry point.

### F2. Boundary or integration replacement

Replace an adapter only after documenting semantics, failure modes, supported environments,
resource ownership, and cutover behavior. A fallback is a product decision, not a default.

### F3. Additional host or entry point

- Define the host's explicit scope, lifecycle, input binding, result, and failure protocol.
- Reuse application services and composition definitions where their lifetimes are compatible.
- Keep host-specific parsing/transport thin and test application behavior independently.
- Ensure one host does not accidentally start another host's lifetime.

### F4. Presentation and operational quality

- Externalize user-facing text when localization is in scope.
- Preserve structured diagnostics and render them at the presentation boundary.
- Add accessibility, observability, and supportability requirements discovered after cutover.

### F5. Persisted state evolution

```text
legacy stores -> typed versioned state, when the product persists state
  validated migrators; reject unsupported future versions
  preserve predecessor and last known-good data
  coordinated atomic updates
```

Define edit/apply/cancel behavior from the product contract. Do not create a new persistence
stack for each UI surface.

### F6. Extension mechanisms

When the product supports user-extensible or dynamically evaluated behavior, define
capabilities, resource budgets, trust boundaries, lifecycle, and diagnostics before implementation.

### F7. Workflow discoverability

Make supported actions discoverable, define intentional empty/disabled states, and turn required
hidden gestures into accessible command surfaces.

## Workflow

1. Spec with clear non-goals and **firm decisions** (no “may remain…” hedges)
2. Failing tests first with the smallest representative fixtures or scenarios
3. Implement inside existing layers; **clean cut** when replacing dual models
4. Surface compile/runtime errors to UI **and** logs
5. Focused + full solution tests (no screenshot-only tasks)
6. Sync long-lived requirements

When the user says not to confirm mid-flight, apply autonomously and report at the end.

## Deliverables

- [ ] Feature on Avalonia trunk with tests
- [ ] No new WinForms statics or UI algorithms in Core
- [ ] Composition and every affected host updated if shared services changed

## Handoff

Structure/security debt → `wfa-harden-decompose`
Legacy delete → `wfa-parity-verify`
