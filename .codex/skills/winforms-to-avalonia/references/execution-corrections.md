# Execution corrections

Patterns distilled from migration sessions. They describe how to execute the method, not which
features a product has.

## 1. Analyze before changing architecture

| Failure mode | Correction |
| --- | --- |
| Guess a root cause and patch repeatedly | Establish evidence and explain the failure path first |
| Add fallbacks that hide defects | Fix the standard path and define fallbacks only as product policy |
| Half-migrate for compatibility after a clean-cut decision | Implement the chosen target and remove the dead path |
| Hedge design decisions | State one decision, its tradeoffs, and its falsifier |

## 2. Test behavior at the owning layer

| Failure mode | Correction |
| --- | --- |
| Assert by reading source/XAML/config text | Use compiled behavior, public APIs, or runtime integration |
| Check only that a fixture exists | Assert the behavior the fixture drives |
| Start the desktop app from a unit test | Isolate explicit smoke tests from unit hosts |
| Use screenshots as sole acceptance | Use behavior tests; screenshots support manual layout review |
| Run shared-output test projects concurrently | Run them sequentially |
| Mix Headless and ordinary tests in one process | Use a dedicated Headless project/process |

## 3. Complete the behavioral path

| Failure mode | Correction |
| --- | --- |
| An option affects one surface only | Bind every affected consumer to one application rule/state transition |
| A command has different semantics from another entry point | Route both through the same use case |
| Unsupported capability appears enabled | Gate it by declared product capability |
| Extreme or invalid values propagate | Validate contracts and add negative tests |

## 4. UI quality is migration behavior

| Failure mode | Correction |
| --- | --- |
| Controls overlap or columns collapse | Use responsive constraints and verify supported window states |
| Secondary windows grow without bounds | Apply maximums, wrapping, scrolling, and consistent styles |
| Related controls are split by implementation detail | Group by user task |
| Numeric empty values jump unpredictably | Define empty/range/increment semantics |
| Window shortcuts steal focused-control keys | Scope gestures by focus and control intent |
| Accessibility is omitted from parity | Verify names, focus order, keyboard navigation, scaling, and localization |

## 5. Fixtures and boundary fidelity

| Failure mode | Correction |
| --- | --- |
| Fixtures are scattered or temporary | Use one stable repository policy |
| Fixtures are oversized | Keep the smallest representative payload with real semantics |
| Environment-backed behavior is tested only as a pure rule | Add adapter fakes and focused real integration evidence when required |
| Boundary encoding/normalization is implicit | Declare and test the external contract |
| External models are simplified beyond their specification | Preserve authoritative semantics at the adapter boundary |

## 6. Core API design

| Failure mode | Correction |
| --- | --- |
| Magic diagnostic strings | Use typed/structured diagnostic identities |
| Magic sentinel values or control indexes | Use explicit semantic types |
| Vague public names | Name by application role |
| Two models silently represent the same concept | Unify them or define complete mapping and ownership |
| UI prompts or environment calls leak into Core | Return results and use narrow ports |

## 7. Hosts and composition

| Failure mode | Correction |
| --- | --- |
| One host accidentally starts another lifetime | Detect host intent before startup |
| Host input is parsed twice | Use one parse/bind/execute path |
| A second host duplicates the service graph | Reuse compatible application services/composition definitions |
| Heavy services are constructed per click/window | Let the composition root own justified shared lifetimes |

## 8. Platform, persistence, and resources

| Failure mode | Correction |
| --- | --- |
| Dependency resolution is implicit | Define product-specific resolution policy and diagnostics |
| Process/resource cleanup covers success only | Test cancellation, timeout, failure, and disposal |
| Persisted state has multiple partial writes | Use one logical transaction and atomic replacement |
| Newer or unreadable state is replaced with defaults | Fail safely and preserve recoverable data |
| Migration promises lossless conversion without evidence | Treat it as validated best effort with rollback |
| Product defaults change silently | Require an explicit decision |

## 9. Async and state ownership

| Failure mode | Correction |
| --- | --- |
| Stale async result overwrites newer state | Commit only when revision and context still match |
| Cancellation leaves partial mutation | Stage then atomically commit, or define rollback |
| Extracted collaborator owns no real behavior | Wire ownership fully or remove it |
| ViewModel remains a multi-step state machine | Move orchestration into a workflow coordinator |

## 10. Process and evidence

| Failure mode | Correction |
| --- | --- |
| Implement without reviewing the plan | Review decisions before apply and after structural slices |
| Mark tasks done without evidence | Require code, behavior tests, and command output |
| Ask for confirmation despite autonomous instruction | Continue within authorized scope and report at the end |
| Treat compile success as parity | Run contract, target-environment, and cutover gates |

## Final self-check

1. Did repository evidence, rather than this skill, determine the feature set?
2. Does each application behavior have one owner and consistent consumers?
3. Are security, persistence, cancellation, and stale-state failure paths covered?
4. Are accessibility and supported environments part of parity evidence?
5. Are Headless tests process-isolated and project tests sequenced safely?
6. Do plan, tasks, code, tests, and command output agree?
