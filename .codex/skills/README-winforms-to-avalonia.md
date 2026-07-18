# WinForms -> Avalonia skill suite

Reusable phased skills for migrating a WinForms desktop application to Avalonia. The suite
defines a migration method, not a product architecture: discover the application's actual
behaviors, state, integrations, and constraints before choosing concrete abstractions.

## Skills (A–G)

| Skill | Phase | Role |
| --- | --- | --- |
| [`winforms-to-avalonia`](./winforms-to-avalonia/) | Orchestrator | Route; enforce rules |
| [`wfa-capture-contracts`](./wfa-capture-contracts/) | **A** | Contracts, modules, coverage |
| [`wfa-extract-domain`](./wfa-extract-domain/) | **B** | UI-independent application core + TDD |
| [`wfa-platform-services`](./wfa-platform-services/) | **C** | Platform and infrastructure adapters |
| [`wfa-avalonia-ui`](./wfa-avalonia-ui/) | **D** | MVVM shell + early modernization |
| [`wfa-parity-verify`](./wfa-parity-verify/) | **E** | Diff, acceptance, cutover |
| [`wfa-post-port-evolve`](./wfa-post-port-evolve/) | **F** | Product evolution on the Avalonia trunk |
| [`wfa-harden-decompose`](./wfa-harden-decompose/) | **G** | Decomposition, resilience, security review |

## Lifecycle

```text
A -> B -> C -> D -> E  (first ship / trunk switch)
                    |-> F evolve
                    `-> G harden/decompose
```

Reusable rulebook:
[`winforms-to-avalonia/references/reusable-learnings.md`](./winforms-to-avalonia/references/reusable-learnings.md)

Agent correction patterns (from real multi-month sessions):
[`winforms-to-avalonia/references/execution-corrections.md`](./winforms-to-avalonia/references/execution-corrections.md)

## Core principles

1. Contracts before controls
2. Core / Infrastructure / UI dependency direction
3. Diagnostics, not UI prompts, below the presentation layer
4. Concrete adapters follow discovered dependencies; do not invent product capabilities
5. Bindings and application state, not controls, are the source of truth
6. TDD + behavior fixtures; no source-text or file-existence tests
7. Headless UI in a separate process/project
8. Sequential multi-project tests when sharing `obj/`
9. Versioned, durable settings; never overwrite newer or unreadable data
10. Untrusted-input bounds are required before first release
11. Half-extractions are bugs
12. Anti-stale coordination for overlapping asynchronous operations
13. Explicit fix / defer / retire decisions for non-parity
14. One application rule must produce consistent results at every consumer
15. Analyze with evidence before patching; use clean cuts when chosen
16. Firm design decisions; tasks need command evidence

## What these skills deliberately omit

- Preset domain models or feature catalogs
- Assumed data shapes, external integrations, or configuration sources
- Specific OpenSpec capability ids from any one repository
- One-off repository paths as universal requirements

When working inside a concrete repository, generated contracts and implementation artifacts
must use that repository's real paths, terminology, behaviors, and evidence. Only the reusable
methodology remains product-agnostic.

## Usage

1. Load `winforms-to-avalonia`.
2. Choose phase A-G from repository evidence.
3. Load one phase skill.
4. Use the repository's specification workflow for each capability slice when applicable.
