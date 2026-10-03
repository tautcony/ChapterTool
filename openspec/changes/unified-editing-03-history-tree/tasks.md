## 1. Reversible history data

- [x] 1.1 Define domain change sets with forward and inverse operations for document values and identities.
- [x] 1.2 Add root, parent/child nodes, preferred-child tracking, cursor, and operation descriptions to the Core session.
- [x] 1.3 Store deltas and shared immutable payloads so a one-field edit does not copy the full document.

## 2. Navigation and validation

- [x] 2.1 Implement atomic undo, preferred redo, explicit branch selection, and no-change boundary outcomes.
- [x] 2.2 Add deterministic, generated-sequence, deep-history, branch, idempotency, and injected-failure Core tests.
- [x] 2.3 Verify no automatic history pruning and record representative allocation and navigation measurements.
- [x] 2.4 Run `dotnet test tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj --no-restore` and fix failures.
