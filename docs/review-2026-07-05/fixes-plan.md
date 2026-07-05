# Fixes Plan

> Execution-oriented fix plan for findings from the 2026-07-05 code review.
> Ordered by impact × effort. Each fix is independently shippable.

## Batch 1 — Pre-release Hardening (recommend before next tag)

### Fix A: Remove ambient culture mutation from XmlLanguageDisplay
- **Addresses:** P2-1
- **Files:** `src/ChapterTool.Avalonia/ViewModels/XmlLanguageDisplay.cs`
- **Complexity:** Medium (1 file, ~30 lines rewritten)
- **Change:**
  1. Delete `TemporaryCurrentUiCulture` class entirely.
  2. In `LanguageDisplayName`, call `CultureInfo.GetCultureInfo(language.Code).DisplayName` **without** swapping `CurrentCulture`/`CurrentUICulture`. For .NET, `CultureInfo.DisplayName` returns the display name in the culture's own language regardless of ambient UI culture — so the result is deterministic.
  3. For the `"und"` code, keep the existing `localizer.GetString("XmlLanguage.Undetermined")` lookup.
  4. Cache results per `localizer.CurrentCultureName` to avoid repeated `GetCultureInfo` calls.
- **Verification:** Open Settings → XML language selector; verify display names are correct in all 3 UI languages. Run `dotnet test ChapterTool.Avalonia.slnx --no-restore`.

### Fix B: publish.sh interrupt resilience + executable validation
- **Addresses:** P2-5, P2-6, P3-4
- **Files:** `scripts/publish.sh`
- **Complexity:** Low (~15 lines added)
- **Change:**
  ```bash
  # 1. Arg-value guard (line ~19, ~23):
  case "$1" in
    -Configuration) [[ $# -ge 2 ]] || { echo "ERROR: -Configuration requires a value" >&2; exit 2; }; Configuration="$2"; shift 2 ;;
    -Runtime)       [[ $# -ge 2 ]] || { echo "ERROR: -Runtime requires a value" >&2; exit 2; }; Runtime="$2"; shift 2 ;;
    ...
  esac

  # 2. Trap + stage-then-rename for macOS bundle:
  trap 'rm -rf "$app_bundle"' INT TERM
  # ... stage into "$app_bundle.tmp", then mv "$app_bundle.tmp" "$app_bundle" at the end
  trap - INT TERM

  # 3. Hard error on missing executable:
  [[ -f "$exe_path" ]] || { echo "ERROR: executable '$exe_path' not found" >&2; exit 1; }
  chmod +x "$exe_path"

  # 4. Glob guard:
  shopt -s nullglob
  items=("$output"/*)
  shopt -u nullglob
  (( ${#items[@]} > 0 )) || { echo "ERROR: no publish output to bundle" >&2; exit 1; }
  ```
- **Verification:** `./scripts/publish.sh -Runtime osx-arm64` on macOS; verify `.app` launches. Test `-Runtime` with no value → clean error.

## Batch 2 — Observability & Correctness Hardening

### Fix C: ExpressionException innerException + OverflowException catch
- **Addresses:** P2-3, P3-1
- **Files:** `src/ChapterTool.Core/Transform/ExpressionService.cs`
- **Complexity:** Low (~10 lines)
- **Change:**
  1. Add ctor: `ExpressionException(string code, string message, IReadOnlyDictionary<string,object?>? args = null, Exception? innerException = null) : base(message, innerException)`.
  2. Add `OverflowException` to the catch list at the two evaluator catch sites (`:89`, `:147`), mapping to a diagnostic (e.g., `InvalidExpression.Overflow`).
- **Verification:** `dotnet test tests/ChapterTool.Core.Tests --no-restore`. Add a test: `ExpressionService_EvaluatesOverflowExpression_ReturnsDiagnostic`.

### Fix D: ApplicationLogPanelProvider event invocation local-copy
- **Addresses:** P2-2
- **Files:** `src/ChapterTool.Infrastructure/Platform/ApplicationLogPanelProvider.cs`
- **Complexity:** Trivial (2 lines)
- **Change:**
  ```csharp
  // Line 106, replace:
  EntryAdded?.Invoke(this, entry);
  // With:
  var handler = EntryAdded;
  handler?.Invoke(this, entry);
  ```
- **Verification:** `dotnet test tests/ChapterTool.Infrastructure.Tests --no-restore`.

### Fix E: IApplicationLogService default interface implementation
- **Addresses:** P2-4
- **Files:** `src/ChapterTool.Core/Services/IApplicationLogService.cs`
- **Complexity:** Low (3 lines)
- **Change:** Add default impl to the event:
  ```csharp
  event EventHandler<ApplicationLogEntry>? EntryAdded
  {
      add { }
      remove { }
  }
  ```
  This makes the event optional for implementers. `ApplicationLogPanelProvider` overrides it with a real event.
- **Verification:** Build passes; existing tests pass.

## Batch 3 — Minor Lifecycle Hardening (can defer)

### Fix F: MainWindow unsubscribe on close
- **Addresses:** P3-2
- **Files:** `src/ChapterTool.Avalonia/Views/MainWindow.axaml.cs`
- **Complexity:** Low
- **Change:** Add `UnsubscribeViewModelCommandState()` method mirroring `SubscribeViewModelCommandState()`, call from `OnClosed` override.

### Fix G: publish.ps1 try/finally cleanup
- **Addresses:** P3-5
- **Files:** `scripts/publish.ps1`
- **Complexity:** Low

### Fix H: Diagnostic placeholder backfill
- **Addresses:** P3-3
- **Files:** `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs`
- **Complexity:** Low
- **Change:** In `LocalizeDiagnostic`, after format, strip any remaining `{token}` patterns with empty string or `[?]`.

### Fix I: TextToolView detach on DetachedFromVisualTree
- **Addresses:** P3-6
- **Files:** `src/ChapterTool.Avalonia/Views/Tools/TextToolView.axaml.cs`
- **Complexity:** Low

## Verification Commands (per AGENTS.md)

After all fixes:
```powershell
dotnet build ChapterTool.Avalonia.slnx --no-restore
dotnet test ChapterTool.Avalonia.slnx --no-restore
openspec validate --all
```

For macOS bundle fix: `./scripts/publish.sh -Runtime osx-arm64` on a macOS host, verify `.app` launches.
