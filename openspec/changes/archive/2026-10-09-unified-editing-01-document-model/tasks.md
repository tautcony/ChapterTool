## 1. Document contracts

- [x] 1.1 Add immutable document, track, chapter, identity, rational frame-rate, and duration value types.
- [x] 1.2 Add validation and explicit ChapterSet import/export adapters; preserve unknown duration semantics.
- [x] 1.3 Add deterministic ticks/frame conversion with midpoint-away-from-zero rounding and overflow diagnostics.

## 2. Serialization boundary

- [x] 2.1 Add snapshot-based pure serialization entry points and separate format options from content transforms.
- [x] 2.2 Add Core behavior tests for immutability, identity preservation, unknown duration, conversion boundaries, and no transform replay.
- [x] 2.3 Run `dotnet test tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj --no-restore` and fix failures.
