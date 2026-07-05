# Phase 3 — Avalonia ViewModels Review

> Scope: `src/ChapterTool.Avalonia/ViewModels/**` changes vs `master`.
> Reviewer: sub-agent bg_cbd33c95 + main-agent verification.

## Phase Summary

The `MainWindowViewModel` i18n refactor is coherent — `LocalizeDiagnostic` uses replace-based formatting (no `FormatException` vector), and all `ApplyEdit`/`LogDiagnostics` call sites within the file now pass localized keys. The new `XmlLanguageDisplay` has a legitimate ambient-culture-mutation design concern (P2). The sub-agent's "selector identity mismatch" finding was **rejected as a false positive** after code inspection.

## Files Reviewed

- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs` (full)
- `src/ChapterTool.Avalonia/ViewModels/SettingsToolViewModel.cs` (full)
- `src/ChapterTool.Avalonia/ViewModels/ToolWindowViewModels.cs` (full)
- `src/ChapterTool.Avalonia/ViewModels/XmlLanguageDisplay.cs` (full, untracked new file)

## Sub-agent Finding Rejected (FALSE POSITIVE)

### ~~[P1] XML language selected-item identity mismatch~~ — **REJECTED**

The sub-agent claimed `XmlLanguageDisplayOptions` "returns a new array every getter call." **Main-agent verification disproved this:**

- `XmlLanguageDisplayOptions => xmlLanguageDisplayOptions` returns the **same persistent `ObservableCollection`** every call (`MainWindowViewModel.cs:289`).
- `RefreshXmlLanguageDisplayOptions` preserves identity by design (`MainWindowViewModel.cs:1463-1487`):
  - If count unchanged: mutates items in-place via `UpdateFrom()` — item identity preserved.
  - If count changed: clears + re-adds — collection identity preserved.
- The `SelectedXmlLanguageDisplayOption` setter matches by **value** (`option.MainText`, `StringComparison.OrdinalIgnoreCase`), not by reference (`:304-305`).
- The getter indexes by `XmlLanguageIndex` integer (`:296-298`), not by reference.

This is deliberate defensive code that avoids the exact problem the sub-agent flagged.

## Findings

### [P2] Ambient culture mutation in XmlLanguageDisplay static helper
- **Location:** `src/ChapterTool.Avalonia/ViewModels/XmlLanguageDisplay.cs:27, 63-82`
- **Trigger:** `XmlLanguageDisplay.Options(localizer)` temporarily sets `CultureInfo.CurrentCulture` / `CurrentUICulture` (process-global) inside a `using var _ = new TemporaryCurrentUiCulture(...)`.
- **Impact:** During the `using` window, ALL threads in the process see the swapped culture. If a background operation (chapter save, expression eval, formatting) runs concurrently and reads `CurrentCulture`, it gets the wrong culture transiently. The `Options()` call itself is safe on the UI thread (exception-safe: `GetCultureInfo` runs before assignment in the ctor), but the side-effect scope is process-wide. In practice, severity is bounded because `Options()` is called only during culture-change refresh and tool-window construction — but it's an anti-pattern in a static helper.
- **Fix:** Remove `TemporaryCurrentUiCulture` entirely. Compute display labels via explicit resource lookup (`localizer.GetString("XmlLanguage." + code)`) with `CultureInfo.GetCultureInfo(code).DisplayName` called WITHOUT mutating ambient state — `DisplayName` respects the culture's own display name regardless of `CurrentUICulture` for neutral cultures. Cache results per UI culture.
- **Confidence:** High (the design issue is real; severity calibrated from sub-agent's P1 to P2)

### [P3] Localized diagnostic template can leave unresolved placeholders
- **Location:** `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs:1378-1393`
- **Trigger:** Localization template expects placeholders absent from `diagnostic.Arguments` (non-null dictionary missing keys).
- **Impact:** User-visible unresolved `{token}` in status/log text. No crash (replace-based formatter).
- **Fix:** Backfill missing keys from a known-defaults map, or strip unresolved tokens.
- **Confidence:** Medium

## 漏检复盘 (Missed-pattern Retrospective)

- **默认分支/未知输入**: `LocalizeDiagnostic` key-miss fallback handled; XmlLanguage index bounds checked. Clean.
- **异步失败路径**: `TextToolViewModel.OnEntryAdded` marshals to UI thread via `Dispatcher.UIThread.Post`. Clean.
- **半完成状态窗口**: `TemporaryCurrentUiCulture` constructor reads culture before assignment — no partial swap on ctor failure. But ambient-mutation design itself is the systemic risk (P2).
- **延迟执行上下文失效**: `Options()` static call lacks call-context guard — flagged as P2.
- **隐式协议/兼容前提**: `SelectorDisplayOption` reference-equality concern investigated and **rejected** — code uses `UpdateFrom` + value-matching.
- **ApplyEdit/LogDiagnostics call sites**: All updated to localized keys; no remaining raw-English callers in-file.
