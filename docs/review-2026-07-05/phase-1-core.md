# Phase 1 — Core Domain Review

> Scope: `src/ChapterTool.Core/**` changes vs `master` (committed + uncommitted).
> Reviewer: sub-agent bg_b9a2378f + main-agent verification.

## Phase Summary

The ExpressionService refactor to structured `ExpressionException(code, message, args)` is consistent across all 18 throw sites. **All 18 `InvalidExpression.*` codes have corresponding `Diagnostic.InvalidExpression.*` keys in all three resx files** — verified programmatically by the main agent (`comm -23` between thrown codes and resx keys returned empty). Importer and diagnostic-arguments changes are structurally safe. One observability gap (no innerException) and one pre-existing overflow edge case noted.

## Files Reviewed

- `src/ChapterTool.Core/Diagnostics/ChapterDiagnostic.cs`
- `src/ChapterTool.Core/Editing/ChapterEditingService.cs`
- `src/ChapterTool.Core/Exporting/ChapterOutputProjectionService.cs`
- `src/ChapterTool.Core/Importing/Text/OgmChapterImporter.cs`
- `src/ChapterTool.Core/Importing/Text/XmlChapterImporter.cs`
- `src/ChapterTool.Core/Transform/ExpressionService.cs`
- `src/ChapterTool.Core/Services/IApplicationLogService.cs`
- `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvChapterImporter.cs` (boundary)

## Sub-agent Finding Rejected (FALSE POSITIVE)

### ~~[P1] MissingOperatorBeforeFunction code missing localization key~~ — **REJECTED**

The sub-agent claimed `Diagnostic.InvalidExpression.MissingOperatorBeforeFunction` was absent from the resx. **Main-agent verification disproved this**: the key exists at line 161 in all three resx files (`en-US`, `zh-CN`, `ja-JP`). Furthermore, a programmatic `comm -23` between all 18 codes thrown by `ExpressionService.cs` and all `Diagnostic.InvalidExpression.*` keys in `Strings.en-US.resx` returned **zero missing codes**. The i18n refactor's diagnostic-code-to-resx-key coverage is complete.

## Findings

### [P2] ExpressionException lacks innerException channel
- **Location:** `src/ChapterTool.Core/Transform/ExpressionService.cs:166-171`
- **Trigger:** Any future throw site that wraps a lower-level exception (parser/format helper) into `ExpressionException` cannot carry the root cause.
- **Impact:** Reduced debuggability. Current throw sites are all direct validation errors (no wrapping needed today), so immediate functional impact is low — but the refactor established a new exception type without the standard `.innerException` ctor, violating the "no swallow of inner exceptions" contract for future maintainers.
- **Fix:** Add `ExpressionException(string code, string message, IReadOnlyDictionary<string,object?>? args = null, Exception? innerException = null)` passing to `base(message, innerException)`.
- **Confidence:** High

### [P3] Pre-existing overflow risk in double→decimal cast (not introduced by this diff)
- **Location:** `src/ChapterTool.Core/Transform/ExpressionService.cs:~505,508`
- **Trigger:** Extreme-domain inputs (very large exponents, NaN-producing trig/log) cast `double` results to `decimal`, which can throw `OverflowException` — not caught by the existing `InvalidOperationException|FormatException|KeyNotFoundException|DivideByZeroException` handlers.
- **Impact:** Unhandled exception propagates to caller; user sees a crash instead of a diagnostic.
- **Fix:** Add `OverflowException` to the catch list, or clamp NaN/Infinity before cast.
- **Confidence:** Medium (pre-existing, not a regression)

## 漏检复盘 (Missed-pattern Retrospective)

- **Unknown input default branch**: Checked `Tokenize`, `ToPostfix`, `EvaluatePostfix` — all unsupported tokens map to explicit diagnostics. Clean.
- **Downstream failure propagation**: Importers still return `ChapterImportResult.Failed(...)` on error. No silent success-on-error introduced. Clean.
- **Half-committed state**: Chapter lists only published on success paths. Clean.
- **Deferred-execution context invalidation**: Expression evaluation is eager over token stream; no captured mutable context. Clean.
- **Single-point utility assumptions**: Placeholder substitution null-safe; diagnostic argument transport null-guarded at call sites. Clean.
- **Evaluation spot-checks**: `1 + 2 * 3` → 7 ✅; `0 ? 2 : 3 + 1` → 4 ✅; `1/0` → DivideByZeroException caught ✅.
