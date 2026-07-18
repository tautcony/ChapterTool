# Full lifecycle A-G

```text
A Capture contracts
B Extract application core (+ solution/test scaffolding)
C Platform / Infrastructure adapters
D Avalonia UI shell (+ early modernization)
E Parity verify / trunk switch / packaging
        │
        ▼
F Post-port evolution on the Avalonia trunk
G Harden and decompose state, resilience, security, and test boundaries
```

First ship is A-E. F and G are follow-on phases, except that security and reliability controls
belong in the phase that introduces the affected boundary; G is not a reason to defer them.

## Phase outcomes

| Phase | Done when |
| --- | --- |
| A | Modules, coverage matrix, command/state contracts, dependency order |
| B | Core builds; behavior tests green; no UI or unmediated environment effects in Core; input bounds exist |
| C | Required adapters are injectable, durable, cancellable, and explicit about unsupported environments |
| D | Main workflows run under Avalonia with bindings, accessibility, and composition in place |
| E | Parity matrix, supported-environment evidence, CI, and trunk/cutover policy exist |
| F | Product backlog work lands without reintroducing legacy coupling |
| G | Oversized state/orchestration is decomposed; async, security, and test isolation are maintained |

## Parallelism

Safe disjoint lanes after contracts stabilize:

- Scaffolding / CI / fixtures
- Core models and pure rules
- Boundary contracts and deterministic translations
- Infrastructure adapters
- UI after service interfaces exist

Serialize shared contracts, persisted-state schema changes, composition registration shapes,
and public capability maps.

## Spec-driven slice shape

1. Why / what / capabilities touched
2. Design: goals, non-goals, decisions, risks
3. Requirements with scenarios
4. Ordered tasks + verification gates
5. Apply with TDD
6. Focused tests + full solution gate
7. Sync long-lived specs; archive the change

## Verification gates

- Layer-focused tests for the changed project
- Full solution tests before merging broad slices
- No parallel multi-project test hosts that share build outputs
- No source-text assertions over implementation files
- Untrusted-input and persistence failure paths covered before first release
- Spec-code alignment review after large structural moves
