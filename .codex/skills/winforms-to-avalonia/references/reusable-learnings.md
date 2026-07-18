# Reusable learnings

These rules apply across WinForms-to-Avalonia migrations. Concrete feature names, storage
mechanisms, integrations, and acceptance scenarios must come from the target repository.

## Architecture

| Learning | Why it matters |
| --- | --- |
| Split Core / Infrastructure / UI before porting screens | Prevents event handlers from becoming the new business layer |
| Freeze shared contracts before parallel work | Avoids churn on state, results, and error semantics |
| Put environment effects behind narrow ports | Tests and supported environments stay deterministic |
| Use a composition root early | Lifetimes and platform substitutions remain explicit |
| Prefer semantic types over control indexes/tags | Controls are presentation state, not domain state |

## Capture and planning

| Learning | Why it matters |
| --- | --- |
| Capture user commands, state transitions, external effects, and acceptance scenarios | Specs without observable behavior are not verifiable |
| Give every legacy file one primary responsibility owner | Stops silent orphans and duplicated ports |
| Split capabilities by dependency profile | Pure, environment-backed, and UI work can progress safely |
| Record intentional non-parity as a decision | “Cleaner” rewrites otherwise break users silently |
| Generate repository-specific artifacts from generic templates | A migration plan must name real code and behavior |

## Application core

| Learning | Why it matters |
| --- | --- |
| Application operations return structured results and diagnostics | Every host can present the same outcome |
| Representative scenarios precede implementation | Behavior preservation needs evidence |
| Compatibility and normalization rules stay explicit | “Obvious cleanup” can change user-visible behavior |
| Advanced rules belong in Core services, not ViewModels | Parity work stays testable |
| Bound untrusted input in the phase that introduces it | Security cannot wait for post-port cleanup |

## Platform and persistence

| Learning | Why it matters |
| --- | --- |
| Environment-specific capabilities return Unsupported rather than crash | Cross-platform hosts still have platform edges |
| Dependency resolution follows a documented product policy | Behavior stays consistent across supported environments |
| Persisted state is typed, versioned, validated, and atomically replaced | Partial writes and silent downgrade lose data |
| Preserve predecessor and last-known-good state | Users need recovery and rollback |
| Resource ownership, cancellation, timeout, and cleanup are part of adapter contracts | Happy-path wrappers leak processes, handles, and partial effects |

## UI and MVVM

| Learning | Why it matters |
| --- | --- |
| First Avalonia ports often remain WinForms-shaped | Plan an explicit modernization pass |
| Observable state replaces pervasive manual Refresh | Duplicate state is a major post-port bug source |
| Real command surfaces replace hidden controls | Accessibility and tests should not depend on shims |
| Await async commands and surface busy/error state | Fire-and-forget loses exceptions and ordering |
| Secondary windows have Views/ViewModels and narrow ports | Imperative trees and full-VM coupling do not scale |
| UI actions use stable identifiers, not localized text | Localization must not break routing |
| Verify accessible names, focus, scaling, narrow/wide states, and localization | Layout and access are user-facing behavior |

## State and async

| Learning | Why it matters |
| --- | --- |
| Collapse mutually exclusive flags into a typed state model | Invalid combinations become unrepresentable |
| Use monotonic revision plus context identity for overlapping operations | Stale results must not clobber newer state |
| Bindings and application state are the source of truth | Scraping controls recreates WinForms coupling |
| One rule drives every affected surface and host | Partial wiring produces contradictory behavior |

## Testing and cutover

| Learning | Why it matters |
| --- | --- |
| Test pyramid: Core -> adapter fakes -> ViewModel -> Headless -> smoke | Fast feedback without desktop automation everywhere |
| Headless UI tests need a separate process/project | Process-wide UI state can deadlock mixed test hosts |
| Prefer behavior tests over control-exists or source-text assertions | Runtime contracts survive refactors |
| Run shared-output test projects sequentially | Avoids file-lock flakiness |
| Verify every supported target environment before cutover | A successful build on one machine is not product readiness |
| Switch development trunk early; delete legacy late | Rare paths and reference behavior remain available |
| Waive gaps explicitly | Silent “done” is the enemy of cutover |

## Security

| Learning | Why it matters |
| --- | --- |
| Bound size, depth, counts, recursion, and allocation for untrusted data | Malformed input must not become denial of service |
| Deny unintended external resource resolution | Data input must not escape its trust boundary |
| Sandbox dynamic execution by capabilities and budgets | User-authored logic must not compromise the host |
| Treat persistence validation and atomic replacement as safety requirements | Corrupted state must not be silently destroyed |

## Process discipline

| Learning | Why it matters |
| --- | --- |
| One change has proposal, design, scenarios, tasks, and evidence | Large rewrites need reviewable slices |
| Designs state firm decisions and non-goals | Agents otherwise implement both sides halfway |
| Large decompositions ship as ordered slices with full gates | Big-bang refactors are hard to review and bisect |
| Update a code map when ownership moves | Maintainers need the new entry points |
| Check plan, tasks, code, tests, and command evidence together | Prevents checkbox-only completion |
