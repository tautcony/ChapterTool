---
name: wfa-platform-services
description: >
  Phase C of a WinForms-to-Avalonia migration: implement platform and infrastructure adapters
  for dependencies discovered in Phase A/B, such as persistence, process execution, dialogs,
  notifications, or environment capabilities. Use only in an explicit migration when such
  dependencies leak into Forms/core or when the orchestrator routes here. Do not invent
  adapters for capabilities the product does not have, and do not implement main-window XAML.
---

# Phase C — Platform / Infrastructure services

Replace legacy static helpers and environment coupling with injectable, testable adapters.

Read [platform abstraction](references/platform-abstraction.md) to define ports,
[external processes](references/external-processes.md) only when the product launches child
processes, and [settings migration](references/settings-migration.md) only when settings exist.

## Workflow

### 1. Catalog touchpoints

| Legacy pattern | Service direction |
| --- | --- |
| UI prompts hidden in helpers | host-owned interaction port |
| Static notifications or logging | structured diagnostic/logging port |
| Persisted application preferences | typed, versioned store |
| Filesystem or process calls | narrow adapter with cancellation and failure semantics |
| Environment capability checks | capability query plus supported/unsupported result |
| Legacy deployment integration | replace, isolate, or explicitly retire |

### 2. Interfaces vs implementations

- Abstractions consumable without Avalonia types where possible
- Implementations in Infrastructure, or in the UI host when they require a UI lifetime

### 3. Persisted state, when present

Evolve toward a **versioned single document** when multiple stores appear:

- `schemaVersion` + sections
- Ordered migrators
- Reject future versions (do not overwrite with defaults)
- Preserve predecessor data while migration is being validated
- Validate the complete document before commit
- Write to a temporary sibling, flush as required, then atomically replace
- Preserve the last known-good state when parsing or migration fails
- Define in-process and cross-process coordination from the actual deployment model

### 4. External processes, when present

- Argument arrays, never shell string concatenation with user-controlled values
- Timeout + cancellation
- Distinguish not-found vs started-but-failed
- Document selection and fallback policy

Unavailable dependencies return a structured unavailable result without changing product state.

### 5. Dependency-backed adapters

Implement the ports defined in Phase B and test success, failure, cancellation, timeout, and
partial-result cleanup with fakes or controlled integration fixtures.

### 6. Secondary-surface API only

Use a narrow host interaction/window service for secondary surfaces; XAML belongs in Phase D.

### 7. Logging

Structured logging; bounded UI buffer with events after append; localize at display time.

## Corrections seen in practice

- Resolve optional dependencies according to documented product policy, not a universal search order.
- Account for platform differences in process I/O, paths, permissions, and lifecycle when relevant.
- Prefer portable implementations when cross-platform support is an explicit requirement.
- Use one transaction boundary for one logical settings update.
- Treat migration as validated best effort, not a promise of lossless conversion.
- Do not silently change product defaults without an explicit decision.

## Deliverables

- [ ] Service interfaces + registration
- [ ] Persisted-state store and migration story when persistence exists
- [ ] Required platform adapters implemented; absent capabilities were not invented
- [ ] Environment-dependent adapters covered with fakes or integration tests
- [ ] Unsupported platform behavior is explicit and recoverable
- [ ] Persisted writes are versioned, validated, and durable when persistence exists
- [ ] No UI prompts or direct environment calls leak into the application core

## Handoff

- UI → `wfa-avalonia-ui`
- Post-port product evolution -> `wfa-post-port-evolve`
