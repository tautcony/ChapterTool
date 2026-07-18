# Architecture blueprint

## Project boundaries

```text
*.Core
  Domain models, application rules, state transitions, boundary contracts, diagnostics
  NO: Avalonia, WinForms, UI prompts, environment handles, unmediated external effects

*.Infrastructure
  Persistence, filesystem, processes, network/devices, environment capabilities,
  platform-specific implementations behind interfaces

*.Avalonia (or another UI host)
  App, composition root, Views/ViewModels, localization, host-specific adapters
  NO: direct WinForms references

Tests
  Core | Infrastructure | Avalonia unit | Avalonia Headless
  Headless UI in a separate project/process
```

## Dependency direction

```text
UI ──► Infrastructure ──► Core
UI ──► Core
Tests ──► SUT (+ fakes)
```

Never: Core -> UI, Core -> WinForms, Infrastructure -> UI control types.

## Composition root

- One startup graph for services, ViewModels, windows, and host lifetime.
- Share a service only when identity, resource ownership, or consistency requires it.
- Additional hosts reuse application services and compatible composition definitions.
- Avoid constructing the graph inside MainWindow code-behind.

## State and commands

Map each legacy user action to a named command or bindable handler. Use semantic state types
instead of control indexes/tags. Return structured results (status, progress, diagnostics);
Views render them.

## State aggregate and workflows

```text
Main ViewModel (thin: commands + bindable projections)
  └── State aggregate (context, mode, pending changes, derived state, options)
  └── Workflows (primary operations, transitions, status)
  └── Narrow ports for secondary surfaces and external effects
```

Overlapping async work:

- Monotonic revision and, where needed, a context/session token.
- Commit progress/results only if the revision and context still match.
- Cancellation before commit must not leave partial mutation.

## Testing pyramid

1. Core behavior and security-boundary fixtures
2. Infrastructure with fake environment adapters
3. ViewModel/host unit tests
4. Headless UI behavior in an isolated process
5. Smoke launch/publish checks on every supported target

No source-text tests. Run multi-project tests sequentially when they share build outputs.
