# Post-port evolution tracks

Select a track only after repository evidence shows that capability exists or is an approved
addition. These are classification aids, not a feature backlog.

| Track | Goal | Core rule |
| --- | --- | --- |
| F1 Deferred capability | Restore an explicitly deferred behavior | Put rules in their owning layer |
| F2 Boundary replacement | Replace an environment/integration adapter | Preserve semantics and failure policy |
| F3 Additional host | Add another entry point or runtime host | Reuse application services; keep host binding thin |
| F4 Presentation quality | Improve localization, accessibility, diagnostics, supportability | Keep Core presentation-independent |
| F5 Persisted state | Evolve product state storage | Version, validate, preserve, atomically replace |
| F6 Extension mechanism | Add/upgrade approved extension or dynamic behavior | Define trust, capabilities, budgets, lifecycle |
| F7 Workflow quality | Improve discoverability and intentional empty/disabled states | Follow command/state contracts |

## Decision template: boundary replacement

```markdown
- Current semantics and failure modes:
- Target adapter and reason:
- Supported environments:
- Fallback policy (or none):
- Resource ownership and cleanup:
- Compatibility/cutover evidence:
```

## Decision template: persisted state

```markdown
- Logical state owner:
- Schema/version policy:
- Validation and default rules:
- Predecessor and last-known-good preservation:
- Coordination and atomic replacement:
- Recovery evidence:
```

## Decision template: additional host

```markdown
- In-scope application commands:
- Out of scope:
- Host input/binding protocol:
- Lifetime and cancellation:
- Result/error protocol:
- Shared composition boundary:
```
