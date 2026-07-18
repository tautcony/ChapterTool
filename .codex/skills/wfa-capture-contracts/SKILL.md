---
name: wfa-capture-contracts
description: >
  Phase A of a WinForms-to-Avalonia migration: inventory the legacy application, split
  responsibilities, capture command and state contracts, build a coverage matrix, and define
  acceptance evidence without implementing Avalonia UI. Use only in an explicit WinForms-to-
  Avalonia migration when behavior is undocumented or the orchestrator routes here. Generated
  artifacts must use the current repository's real paths, terminology, and capabilities.
---

# Phase A — Capture legacy contracts

Turn a WinForms app into **documented contracts** so the rewrite does not depend on copying
event handlers. Prefer Designer metadata, resources, handlers, helpers, services, and tests over
screenshots alone.

Read [contract templates](references/contract-templates.md) when writing behavior contracts,
[coverage matrix](references/coverage-matrix.md) when checking file ownership, and
[module split](references/module-split.md) when defining responsibility boundaries.

## Workflow

### 1. Inventory

Classify legacy files:

| Category | Typical contents |
| --- | --- |
| Entry / shell | `Program`, main Form, startup args, runtime checks |
| Domain models | Entities and value objects mutated by legacy workflows |
| Application behavior | Use cases, policies, validation, calculations |
| State and data | In-memory state, persistence, mapping, synchronization |
| External boundaries | Filesystem, network, process, device, or service integrations actually present |
| Supporting UI | Secondary windows, dialogs, notifications, settings surfaces |
| Platform | Environment-specific capabilities and lifecycle behavior |
| Build / distribution | Projects, packaging, CI, deployment assets |
| Tests / fixtures | Unit, integration, UI tests, representative scenarios |

Assign every production file a **primary module**.

### 2. Split modules

Suggested shape (rename to the product):

1. UI shell and interactions
2. Application core and state transitions
3. Boundary ports and adapters, grouped by dependency
4. Supporting UI and platform services
5. Tests, build, distribution, and assets

Each module doc: purpose, rewrite boundary, reviewed files, behaviors, UI coupling to strip,
risks/decisions. See `references/module-split.md`.

### 3. Command catalog

For every user-visible action:

- Entry point or user gesture
- Preconditions and authorization
- State transitions
- Observable outputs and external effects
- Cancellation, failure, and recovery behavior
- Platform-specific or intentionally hidden behavior

Also list semantic window state (context, selection, modes, pending changes, status) rather than
only control names.

### 4. Coverage matrix

| File | Primary module | Referenced | Notes |

Mechanically verify against a full legacy file listing.

### 5. Acceptance samples and fixture policy

For each major workflow: success, validation failure, cancellation, dependency failure, and
recovery from partial or stale state. Derive scenarios from the application, not this skill.

Plan a stable fixture/scenario layout. Prefer the smallest representative data that exercises
real behavior over inventory-only checks.

### 6. Decision log

Supported platforms, accessibility expectations, hidden gesture policy, deployment strategy,
external dependencies, compatibility obligations, and known bugs to preserve or fix.

### 7. Capability / apply order

Map modules to implementable capabilities ordered by dependency:

1. Scaffolding + core contracts
2. Platform services
3. Pure application behavior and boundary contracts
4. Infrastructure adapters required by those contracts
5. UI shell
6. Later product evolution and structural hardening

Document parallel lanes and shared-contract locks.

## Deliverables

- [ ] Rewrite index linking modules
- [ ] Module docs with boundaries
- [ ] Coverage matrix without orphans
- [ ] Command + state contracts
- [ ] Fixture/acceptance list
- [ ] Risk and decision list
- [ ] Capability apply order

## Handoff

Load `wfa-extract-domain`. Do not implement Avalonia Views here.
