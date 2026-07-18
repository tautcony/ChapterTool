# Phase roadmap

## Critical path

```text
A Contracts ──► B Application core ──► C Platform ──► D UI shell ──► E Parity/trunk
                    │                         │
                    ├ pure rules              └ environment-backed operations
                    └ boundary contracts               (needs C)

E ──► F Post-port evolution
  ──► G Harden and decompose
```

## Definition of ready

| Gate | Ready when |
| --- | --- |
| A -> B | Commands/state are contracted; files and responsibilities are owned |
| B -> C | Core is UI/environment-free; pure behavior and negative tests pass |
| C -> D | Required platform ports are injectable with explicit failure semantics |
| D -> E | Main workflows, accessibility, and responsive states have behavior evidence |
| E cutover | Gaps are fixed, explicitly deferred, retired, or waived |
| E -> F | Avalonia is trunk; remaining gaps are catalogued |
| F -> G | Growth creates structural, resilience, or security debt |
| G steady | State ownership, async context, test isolation, and code map are current |

## Anti-skip

- Do not start XAML before contracts and core behavior for that flow exist.
- Do not defer baseline input, persistence, or authorization safety to a post-port phase.
- Do not delete legacy because “main path works.”
- Do not treat compile success as parity.
