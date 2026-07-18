# TDD baseline

1. Identify representative scenarios from Phase A.
2. Write failing tests against application-core contracts.
3. Implement the smallest behavior until green.
4. Add invalid state, cancellation, boundary, and security cases.
5. Add golden/snapshot assertions only when the product has a stable serialized contract.

| Concern | Project |
| --- | --- |
| Pure application behavior | `*.Core.Tests` |
| Environment adapters | `*.Infrastructure.Tests` |
| Commands and presentation state | UI unit tests later |

Do not reference Avalonia or WinForms from Core tests. Keep real external dependencies out of
default unit tests; use fakes and separate focused integration coverage.

Document intentional non-parity with explicit behavior tests.

## Forbidden test patterns

- Reading implementation/configuration files as text and asserting substrings
- Asserting only that a fixture file exists
- Launching the real desktop application from a unit test
- Screenshot-only automated acceptance

Prefer application-state assertions, adapter fakes, isolated Headless behavior, and intentional
smoke tests for executable startup.
