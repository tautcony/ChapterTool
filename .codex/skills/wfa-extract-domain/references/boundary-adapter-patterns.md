# Boundary and adapter patterns

Use this reference only when the application has external inputs or effects. The concrete
boundary may be a file, network service, process, device, operating-system capability, or
another application service; discover it from the legacy code.

## Pure boundary logic

- Keep deterministic validation, mapping, normalization, and result construction in the core.
- Preserve externally visible semantics and compatibility rules in explicit contracts.
- Return structured outcomes and diagnostics rather than showing UI or logging through globals.

## Environment-dependent adapters

- Define a narrow port for each external effect needed by an application use case.
- Put the implementation in Infrastructure or the host that owns the environment.
- Keep cancellation, timeout, retry, ownership, and cleanup semantics in the contract.
- Use a fake or controlled adapter for unit tests; reserve real integrations for focused tests.

## Multiple implementations

- Use a strategy, factory, or catalog only when the product has multiple interchangeable
  implementations or runtime selection.
- Route by semantic capability or an explicit selection policy, not by UI control indexes or
  localized labels.
- Do not create a multi-implementation selection abstraction for a single implementation.

## Partial support

- Represent unsupported or unavailable capabilities explicitly.
- Do not advertise an implementation until its behavior and failure paths are real.
- Document intentional non-parity and the user-visible recovery path.
