---
name: winforms-to-avalonia
description: >
  Orchestrate a phased WinForms-to-Avalonia migration: behavior contracts, UI-independent
  application logic, platform adapters, MVVM shell, parity/cutover, evolution, and hardening.
  Use when the request explicitly involves moving a WinForms or .NET Framework desktop UI to
  Avalonia, or continuing that migration. Do not trigger for unrelated migrations, rewrites,
  ViewModel refactors, settings work, or command-line work without WinForms-to-Avalonia context.
  Load this orchestrator before one matching phase skill.
---

# WinForms -> Avalonia (generic orchestrator)

Rewrite for **behavior contracts and layered architecture**, not control-for-control ports.
WinForms handlers and Designer state are evidence of *what users can do*; Avalonia Views only
present ViewModel state.

## Suite (A–G)

| Phase | Skill | Generic outcome |
| --- | --- | --- |
| A | `wfa-capture-contracts` | Inventory, modules, command contracts, coverage matrix, capability split |
| B | `wfa-extract-domain` | UI-independent application core, boundary ports, TDD |
| C | `wfa-platform-services` | Platform and infrastructure adapters |
| D | `wfa-avalonia-ui` | Composition root, MVVM, bindings, secondary tools |
| E | `wfa-parity-verify` | Diff matrix, acceptance, packaging, trunk/cutover |
| F | `wfa-post-port-evolve` | Product evolution without reintroducing legacy coupling |
| G | `wfa-harden-decompose` | State decomposition, resilience, security review, test isolation |

Shared references (all product-agnostic):

- `references/lifecycle-full.md` — A–G journey and gates
- `references/phase-roadmap.md` — critical path and parallelism
- `references/architecture-blueprint.md` — layering and composition
- `references/anti-patterns.md` — port and long-term smells
- `references/reusable-learnings.md` — distilled rules of thumb
- `references/execution-corrections.md` — how humans correct agents mid-migration (**read this**)

## Route work

1. Locate the legacy tree, target runtime, repository guidance, and existing migration artifacts.
2. Inventory actual product capabilities before proposing abstractions or project boundaries.
3. Choose a phase from the evidence below.
4. Load one phase skill and only the references needed for that work.
5. Use the repository's specification workflow per capability slice when applicable.

| Situation | Phase |
| --- | --- |
| No contracts / file coverage map | A |
| Business rules or use-case orchestration still inside Forms/helpers | B |
| Filesystem, process, dialogs, settings, or OS APIs leak into the application core | C |
| UI event-driven / manual control refresh | D |
| Comparing old vs new / delete legacy? | E |
| Avalonia is the trunk; evolving discovered product capabilities | F |
| Oversized ViewModels, stale async results, security debt, mixed Headless host | G |

## Rules that always apply

1. **Contracts before controls** - command catalog, state transitions, external effects, and acceptance evidence.
2. **Discover before abstracting** - never assume a feature, integration, or storage mechanism exists.
3. **Layering** - the application core has no Avalonia/WinForms types; adapters own environment-specific effects.
4. **Diagnostics over UI prompts** below the presentation layer.
5. **State over controls** - bindings and application state are authoritative; do not scrape controls before commands.
6. **Consistent behavior** - one rule or option must produce the same semantics at every entry point and consumer.
7. **Security before release** - bound untrusted input, external resource access, and dynamic execution in the phase that introduces them.
8. **Durable writes** - version, validate, and atomically replace persisted state; preserve recoverable data on failure.
9. **No source-text tests** that grep implementation files; no file-existence-only unit tests.
10. **Headless UI tests** run in a separate project/process from non-UI unit tests.
11. **Sequential tests** when multiple projects share build outputs.
12. **Legacy delete** only after parity evidence and explicit decisions for every remaining gap.
13. **Half-extractions are bugs** - extracted types must own behavior or be removed.
14. **Analyze then patch** - establish the cause before changing architecture or adding fallbacks.
15. **Clean cuts over dual paths** when the target design has been chosen.
16. **Firm specification decisions** - do not leave incompatible designs simultaneously optional.
17. **Tasks need evidence** - code, behavior tests, and command output before marking done.

## Handoff after each phase

- Artifacts and paths
- Specification/change status if used
- Gaps and decisions
- Next phase skill
- Tests run

For “do the whole migration,” still checkpoint after A and after B+C before large UI work.
