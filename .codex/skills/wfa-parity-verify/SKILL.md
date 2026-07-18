---
name: wfa-parity-verify
description: >
  Phase E of a WinForms-to-Avalonia migration: verify behavioral parity against Phase A
  contracts, record intentional differences, validate supported environments and packaging,
  and make evidence-based legacy cutover decisions. Use only for an explicit migration when
  comparing the legacy and Avalonia implementations or when the orchestrator routes here.
  Do not delete legacy without the cutover checklist and explicit authorization.
---

# Phase E — Parity verification and cutover

Prove the Avalonia line is the **maintenance trunk**, document gaps, then freeze or delete
legacy only with evidence.

Read [acceptance](references/acceptance.md) to build repository-specific scenarios,
[legacy diff](references/legacy-diff.md) to record evidence, and
[cutover checklist](references/cutover-checklist.md) before freezing or deleting legacy code.

## Workflow

### 1. Capability matrix

| Feature | New status | Diff / gap | Impact | Decision (fix/defer/retire) |

Populate the matrix from Phase A. Cover every user command, state transition, external effect,
failure/recovery path, accessibility contract, supported environment, and deployment path that
the product actually has.

### 2. Automated verification

Run solution and layer-focused tests; record commands. Sequential multi-project runs if needed.

For large multi-module parity audits, use **independent read-only sub-agents per module** so conclusions do not pollute one context.

### 3. Acceptance checklist

Run the Phase A acceptance scenarios against both implementations where possible. Include
startup/lifecycle, primary and secondary workflows, invalid input, cancellation, dependency
failure, stale-state prevention, accessibility, and recovery.

### 4. Modernization pass

If still WinForms-shaped (Refresh, no composition root), file and fix high-severity items.

### 5. Packaging

For each supported target, verify build, publish, install/launch, upgrade or replacement policy,
runtime dependencies, assets, licenses, and rollback/recovery expectations.

### 6. Cutover stages

1. Trunk switch — all new work on Avalonia/`src`
2. Legacy freeze — reference only
3. Gap burn-down or waivers
4. Physical delete only after checklist

“Main path works” is not enough when any required capability or environment remains unverified.

### 7. Spec verification method

Per capability: implementation status plus automated/manual evidence. Do not mark done when
only empty entry points exist. Treat missing untrusted-input bounds, durable persistence, or
supported-environment smoke evidence as cutover blockers unless explicitly waived.

## Deliverables

- [ ] Parity/diff report with decisions
- [ ] Test results summary
- [ ] Acceptance notes
- [ ] Packaging note
- [ ] Cutover checklist or dated deferrals

## After E

First migration done ≠ product complete. Continue with:

- `wfa-post-port-evolve` for product upgrades
- `wfa-harden-decompose` for structure/security debt
