# Phase 2 — Infrastructure Review

> Scope: `src/ChapterTool.Infrastructure/**` changes vs `master`.
> Reviewer: sub-agent bg_5f185fb5 + main-agent verification.

## Phase Summary

`ILocalizationService` / `LocalizationService` deletion leaves **zero C# dangling references** repo-wide (confirmed via `git grep`). One event-invocation race found in `ApplicationLogPanelProvider` (calibrated P2 — real but low-impact in a logging path). The new `IApplicationLogService.EntryAdded` event is a breaking interface change for out-of-tree implementers (P2).

## Files Reviewed

- `src/ChapterTool.Infrastructure/Platform/ApplicationLogPanelProvider.cs` (full read + verified)
- `src/ChapterTool.Infrastructure/Services/IApplicationLogService.cs`
- `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvChapterImporter.cs`

## Dangling-Reference Audit

```
$ git grep -n -E "ILocalizationService|LocalizationService" -- "*.cs"
(no output)
```

**Verdict: CLEAN.** Zero C# references to the deleted types remain in production or test code. Mentions in docs/OpenSpec markdown are historical narrative, not runtime risk.

## Findings

### [P2] Event invocation race in ApplicationLogPanelProvider
- **Location:** `src/ChapterTool.Infrastructure/Platform/ApplicationLogPanelProvider.cs:106`
- **Trigger:** Thread A calls `Capture()` → reaches `EntryAdded?.Invoke(this, entry)`. Thread B unsubscribes (`EntryAdded -= handler`) between the null-check and the delegate invocation.
- **Impact:** Intermittent `NullReferenceException` from the logging path. In practice, low probability (desktop app, logging is near-sequential), but it's the textbook C# event race. An unhandled NRE from `ILogger.Log` could propagate depending on the logging pipeline's exception guards.
- **Fix:** Copy delegate to local before invoking:
  ```csharp
  var handler = EntryAdded;
  handler?.Invoke(this, entry);
  ```
  Note: invocation is correctly outside the lock (avoids reentrancy deadlock) — keep it there.
- **Confidence:** High (the race is real; severity calibrated from sub-agent's P1 to P2 given low-impact logging context)

### [P2] IApplicationLogService.EntryAdded is a breaking interface change
- **Location:** `src/ChapterTool.Core/Services/IApplicationLogService.cs:7`
- **Trigger:** Any external implementer of `IApplicationLogService` compiled against the old interface.
- **Impact:** Compile-time break for out-of-tree consumers. In-repo, the single implementer (`ApplicationLogPanelProvider`) is updated.
- **Fix:** If backward-compat matters, add a default interface implementation: `event EventHandler<ApplicationLogEntry>? EntryAdded { add { } remove { } }` or document the contract change.
- **Confidence:** Medium (repo-local correctness is fine)

## 漏检复盘 (Missed-pattern Retrospective)

- **悬空引用**: 全仓 C# 搜索确认删除类型零残留。Clean.
- **接口新增成员**: 仓内实现已覆盖；对外破坏已报 P2。
- **异步阻塞**: 未引入 `.Result` / `.Wait()`。Clean.
- **类型安全**: 无 `dynamic`、危险强转、空 `catch`。Clean.
- **事件并发反路径**: 命中 `EntryAdded?.Invoke` 竞态（P2）。
