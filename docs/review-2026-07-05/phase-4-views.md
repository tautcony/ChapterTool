# Phase 4 — Avalonia Views, Code-Behind & Bundle Config Review

> Scope: `src/ChapterTool.Avalonia/Views/**`, `csproj`, `Assets/`, `Services/`.
> Reviewer: sub-agent bg_1c8af626 + main-agent verification.

## Phase Summary

No P0 found. The macOS `Info.plist` has all required keys with valid values; `CFBundleExecutable=ChapterTool.Avalonia` matches the default assembly name. The real risk is the **silent chmod skip** in `publish.sh` (calibrated P2). Empty-grid overlay binds correctly to `IsChapterGridEmpty` and respects AGENTS.md layout rules. Code-behind async handlers have try/catch.

## Files Reviewed

- `src/ChapterTool.Avalonia/Views/MainWindow.axaml` + `.axaml.cs`
- `src/ChapterTool.Avalonia/Views/Tools/SettingsToolView.axaml`
- `src/ChapterTool.Avalonia/Views/Tools/TextToolView.axaml` + `.axaml.cs`
- `src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj`
- `src/ChapterTool.Avalonia/Assets/MacOS/Info.plist`
- `src/ChapterTool.Avalonia/Services/{AvaloniaWindowService,RuntimeChapterLoadService,RuntimeChapterSaveService}.cs`

## Info.plist & Bundle Config Audit

All required keys present and valid:

| Key | Value | Status |
|-----|-------|--------|
| `CFBundleName` | `ChapterTool` | ✅ |
| `CFBundleDisplayName` | `ChapterTool` | ✅ |
| `CFBundleIdentifier` | `com.tautcony.chaptertool` | ✅ reverse-DNS |
| `CFBundleVersion` | `1.0.0` | ✅ numeric |
| `CFBundleShortVersionString` | `1.0.0` | ✅ numeric |
| `CFBundlePackageType` | `APPL` | ✅ |
| `CFBundleExecutable` | `ChapterTool.Avalonia` | ⚠️ matches default assembly name, but no validation in publish script |
| `CFBundleIconFile` | `app-icon.icns` | ✅ |
| `LSMinimumSystemVersion` | `10.15` | ✅ |
| `NSHighResolutionCapable` | `true` | ✅ |
| `LSApplicationCategoryType` | `public.app-category.developer-tools` | ✅ |

**csproj**: No explicit `AssemblyName` → defaults to `ChapterTool.Avalonia` (matches `CFBundleExecutable`). No `PublishSingleFile` (no rename risk). `ApplicationIcon` → `Assets\Icons\app-icon.ico` ✅.

## AGENTS.md UI Compliance

- No `Canvas`/absolute positioning in reviewed XAML ✅
- DataGrid columns have `MinWidth` (56, 105.6, 144, 76.8) ✅
- Empty-grid overlay: `HorizontalAlignment/VerticalAlignment=Center`, `IsHitTestVisible=False`, has automation id ✅
- Overlay binds `IsVisible="{Binding IsChapterGridEmpty}"` → `Rows.Count == 0` with `OnPropertyChanged` at row mutation ✅
- No visible Canvas regression ✅

## Findings

### [P2] CFBundleExecutable name consistency + silent chmod skip in publish.sh
- **Location:** `src/ChapterTool.Avalonia/Assets/MacOS/Info.plist:19-20` + `scripts/publish.sh:94-95`
- **Trigger:** `publish.sh` does `[[ -f "$exe_path" ]] && chmod +x "$exe_path"` — if the executable doesn't exist with the expected name `ChapterTool.Avalonia`, the `chmod` is **silently skipped** and the bundle ships without a valid executable. LaunchServices will refuse to launch the `.app`.
- **Impact:** macOS bundle launch failure with no diagnostic at publish time. The names match by default today (no `AssemblyName` override), but there's no assertion/validation to catch drift if assembly naming changes.
- **Fix:** Make the executable check a hard error:
  ```bash
  [[ -f "$exe_path" ]] || { echo "ERROR: expected executable '$exe_path' not found" >&2; exit 1; }
  chmod +x "$exe_path"
  ```
- **Confidence:** High (the silent-skip is real; the name-match risk is conditional on future changes)

### [P3] MainWindow subscribes commands/Rows without unsubscribe on close
- **Location:** `src/ChapterTool.Avalonia/Views/MainWindow.axaml.cs:498-506`
- **Trigger:** `SubscribeViewModelCommandState()` attaches `CanExecuteChanged` to 18 commands + `Rows.CollectionChanged`. No `OnClosed` / `Detached` unsubscribe in the file.
- **Impact:** For the **main window** (which owns its ViewModel), the subscription graph is self-contained — when the window closes, both subscriber and source are collectable together. Risk only materializes if a ViewModel outlives the window (multi-window scenario). Low practical impact for single-window desktop app.
- **Fix:** Override `OnClosed` and detach, or use weak-event pattern. Defensive but not urgent.
- **Confidence:** High (the missing unsubscribe is real; severity calibrated from sub-agent's P2 to P3)

### [P3] TextToolView control-level event handler permanently attached
- **Location:** `src/ChapterTool.Avalonia/Views/Tools/TextToolView.axaml.cs:18`
- **Trigger:** `DataContextChanged += OnDataContextChanged` attached without corresponding detach.
- **Impact:** Low in Avalonia control lifecycle. The view does call `subscribedViewModel.DetachLiveRefresh()` on DataContext change (`:40`), which handles the subscription leak the sub-agent flagged. Residual reference risk only in complex host scenarios.
- **Fix:** Add detach in `DetachedFromVisualTree` if control can outlive host transitions.
- **Confidence:** Medium

## 漏检复盘 (Missed-pattern Retrospective)

- **默认分支/非法输入**: `RuntimeChapterLoadService` has explicit failure diagnostics for invalid paths/unsupported extensions. Clean.
- **异步失败路径**: `OnDrop` has try/catch with status writeback (`:230-255`). Load/Save services propagate IO exceptions. Clean.
- **半提交状态窗口**: `RuntimeChapterSaveService` only appends "Saved" diagnostic after successful file write. Clean.
- **延迟执行上下文失效**: `ScheduleWindowCommandRefresh` uses `Dispatcher.UIThread.Post` — but no close-time unsubscribe (P3).
- **UI 布局规则**: Compliant with AGENTS.md (no Canvas, MinWidth present, overlay responsive).
