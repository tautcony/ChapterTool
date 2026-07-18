---
name: wfa-extract-domain
description: >
  Phase B of a WinForms-to-Avalonia migration: extract UI-independent application behavior,
  state rules, use cases, and boundary contracts with TDD and no WinForms/Avalonia references.
  Use only when an explicit migration still has business behavior in Forms or UI helpers, or
  when the orchestrator routes here. Do not build Avalonia XAML in this phase.
---

# Phase B — Extract application core

Implement Phase A contracts as a UI-independent application core. The UI may call the core;
the core never calls UI controls or framework services.

Read [core layer](references/core-layer.md) for dependency boundaries,
[boundary adapter patterns](references/boundary-adapter-patterns.md) when the application has
external inputs or effects, and [TDD baseline](references/tdd-baseline.md) for test sequencing.

## Workflow

### 1. Scaffold (if needed)

- `src/<Product>.Core` — modern TFM, no UI packages
- `tests/<Product>.Core.Tests` — representative behavior fixtures and helpers

### 2. Models and invariants

- Preserve field meanings and documented invariants
- Drop UI-only storage (control tags, display-only caches unless computed by services)
- Prefer explicit edit APIs over free-form mutation from Views

Document intentional semantic changes.

### 3. Application rules and use cases

Extract calculations, validation, state transitions, and multi-step use cases discovered in
Phase A. Replace control indexes and tags with semantic types.

### 4. Boundary contracts

1. Keep deterministic translation or validation logic in the core when it has no environment dependency.
2. Represent filesystem, process, network, device, clock, and OS dependencies as narrow ports when present.
3. Implement environment-dependent adapters in Phase C, not as hidden calls from the core.

Return structured outcomes and diagnostics. Never prompt the user from the application core.

### 5. Untrusted-input baseline

- Validate size, depth, counts, recursion, and identifier ranges before allocation or traversal.
- Disable external resource access unless the product explicitly requires and constrains it.
- Bound dynamic execution by capability, time, memory, and cancellation when it exists.
- Add negative fixtures before the first releasable Avalonia build.

### 6. TDD

Port legacy tests; goldens for representative fixtures; edge cases from contracts;
document intentional non-support with tests.

### 7. Coupling kill list

Check the core for zero WinForms/Avalonia types, UI prompts, environment-specific APIs, and
unmediated external effects.

## Corrections seen in practice

- One application rule must drive every relevant entry point and consumer.
- Validate extreme values and illegal transitions; add regression tests for rejected states.
- Prefer typed diagnostic codes over free-form strings.
- Public core types are a stable API: use role-based names, explicit invariants, and accurate documentation.
- Preserve authoritative external contracts at boundary adapters; do not simplify away required semantics.

## Deliverables

- [ ] Core builds
- [ ] Models, invariants, and use cases extracted
- [ ] Boundary ports match discovered dependencies
- [ ] Untrusted-input bounds covered before first release
- [ ] Core tests green

## Handoff

- Platform and infrastructure adapters -> `wfa-platform-services`
- UI → `wfa-avalonia-ui`
- Product evolution after ship -> `wfa-post-port-evolve`
