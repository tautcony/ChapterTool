## 1. Session mutation kernel

- [x] 1.1 Add `SessionState`, base tokens, typed transaction outcomes, and a serialized command queue over the document from change 01.
- [x] 1.2 Implement candidate validation, no-change detection, atomic publication, cancellation boundaries, and resource-failure rollback.
- [x] 1.3 Add transaction ID request binding and idempotent result replay.

## 2. Existing workflow adapter

- [x] 2.1 Add a stable-ID-preserving adapter for non-structural `ChapterSet` edit results. Keep current commands usable. Change 04 owns complete workspace command routing.
- [x] 2.2 Add Core behavior tests for atomic batch failure, cancellation, stale work, duplicate requests, and conflicting IDs.
- [x] 2.3 Run `dotnet test tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj --no-restore` and fix failures.
