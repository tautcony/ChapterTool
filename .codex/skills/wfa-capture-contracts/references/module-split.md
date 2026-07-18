# Responsibility split guide

## Principles

1. UI shell owns interaction and presentation, not application rules.
2. Core owns models, invariants, state transitions, and use cases testable without a UI host.
3. Boundary ports describe only external effects the product actually needs.
4. Infrastructure/host adapters own environment-specific implementation details.
5. Build/distribution owns packaging and assets, not feature semantics.

Do not create one module per category mechanically. Use the legacy call graph and change reasons
to find cohesive responsibilities.

## Module doc outline

```markdown
# Module NN: <title>
## Purpose and rewrite boundary
## Reviewed files
## User commands and state transitions
## External effects and dependencies
## Existing tests and acceptance scenarios
## Target ownership and interfaces
## Risks and open decisions
```

## Cross-cutting helpers

For a helper that mixes UI, application rules, and environment effects:

- Assign one primary owner by dominant responsibility.
- Record the mixed concerns that later phases must split.
- Let other modules reference it without duplicating ownership.

## Validation

Every legacy source file has exactly one primary module; tests map to the behavior they protect.
