# Acceptance checklist template

Build the concrete checklist from Phase A. Omit categories the product does not have.

## Lifecycle and context

- [ ] Empty/default startup state
- [ ] Supported startup inputs or activation modes
- [ ] Close, cancel, restart, and recovery behavior
- [ ] Invalid input leaves no stale or partially committed state

## Per capability

- [ ] Primary behavior and result match the contract
- [ ] Validation and unsupported cases are explicit
- [ ] Cancellation, retry, and reentrancy semantics hold
- [ ] Required dependencies report actionable diagnostics

## State changes

- [ ] State changes update every affected surface
- [ ] Structural actions preserve invariants
- [ ] Undo/redo or dirty-state behavior matches when present
- [ ] Overlapping async work cannot commit stale results

## External effects and persistence

- [ ] Advertised effects are observable and correct
- [ ] Writes are atomic/recoverable when required
- [ ] Compatibility and serialization rules are explicit when present
- [ ] Resource cleanup covers success, failure, cancellation, and timeout

## UI, accessibility, and environment

- [ ] Primary and secondary surfaces open/reopen with correct lifetime
- [ ] Commands, shortcuts, and focused-control behavior match decisions
- [ ] Accessible names, focus order, keyboard navigation, scaling, and localization hold
- [ ] Default, narrow, and wide layouts remain usable
- [ ] Every supported target builds, launches, and reports unsupported capabilities honestly

## Automated anchors

- Application/ViewModel tests without UI host
- Headless tests for critical interactions in an isolated project/process
- Intentional smoke launch/publish checks for supported targets
- Stable automation identifiers for interaction surfaces
