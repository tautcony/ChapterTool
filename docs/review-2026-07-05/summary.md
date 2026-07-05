# Code Review Summary — `feat/improve-struct` vs `master`

> Review date: 2026-07-05
> Scope: all changes (committed + uncommitted) on `feat/improve-struct` vs `master`
> 73 files changed, +4,161 / −810 lines

## Verdict

**No P0 (critical/blocker) defects found.** The branch is structurally sound and the build passes (0 warnings / 0 errors). The i18n refactor — the largest portion of the diff — is **structurally complete**: all 234 keys × 3 languages are in parity, all 62 placeholder keys match across languages, and all 18 `InvalidExpression.*` diagnostic codes have resx coverage. The `ILocalizationService` deletion leaves zero dangling references.

The 12 confirmed findings are all P2/P3 and cluster around three themes: (1) a culture-mutation anti-pattern in a new helper, (2) interrupt-resilience gaps in publish scripts, and (3) minor lifecycle/observability hardening.

## Finding Count by Severity

| Severity | Count | Theme |
|----------|-------|-------|
| **P0** | 0 | — |
| **P1** | 0 | — |
| **P2** | 6 | Culture mutation, event race, interface break, arg guard, interrupt cleanup, bundle validation |
| **P3** | 6 | Overflow edge case, unsub lifecycle, unresolved placeholders, glob edge case, ps1 rollback, view detach |
| **Rejected FPs** | 2 | Sub-agent false positives caught by main-agent verification |

## Findings (P2 → P3)

### P2-1: Ambient culture mutation in `XmlLanguageDisplay`
- **File:** `src/ChapterTool.Avalonia/ViewModels/XmlLanguageDisplay.cs:27, 63-82`
- `TemporaryCurrentUiCulture` mutates `CultureInfo.CurrentCulture`/`CurrentUICulture` process-wide inside a `using`. Concurrent background operations reading `CurrentCulture` during this window get the wrong culture.
- **Fix:** Remove ambient mutation; use explicit resource lookup + `CultureInfo.DisplayName` without swapping ambient state.

### P2-2: Event invocation race in `ApplicationLogPanelProvider`
- **File:** `src/ChapterTool.Infrastructure/Platform/ApplicationLogPanelProvider.cs:106`
- `EntryAdded?.Invoke(this, entry)` without local-copy pattern — standard C# event race on concurrent unsubscribe.
- **Fix:** `var handler = EntryAdded; handler?.Invoke(this, entry);`

### P2-3: `ExpressionException` lacks `innerException` channel
- **File:** `src/ChapterTool.Core/Transform/ExpressionService.cs:166-171`
- New exception type has no `innerException` constructor — future wrapping sites can't carry root cause.
- **Fix:** Add `Exception? innerException = null` ctor overload.

### P2-4: Breaking interface change on `IApplicationLogService`
- **File:** `src/ChapterTool.Core/Services/IApplicationLogService.cs:7`
- New `EntryAdded` event breaks out-of-tree implementers. In-repo implementer updated.
- **Fix:** Default interface implementation or version the contract if external consumers exist.

### P2-5: `publish.sh` missing arg-value guard
- **File:** `scripts/publish.sh:19, 23`
- `-Runtime` without value → `set -u` abort with cryptic error.
- **Fix:** `[[ $# -ge 2 ]] || { echo "ERROR: $1 requires a value" >&2; exit 2; }`

### P2-6: No interrupt cleanup during macOS bundle restructuring + silent chmod skip
- **File:** `scripts/publish.sh:75-83, 94-95`
- Non-atomic multi-file `mv` loop with no `trap`; `chmod +x` silently skipped if executable missing.
- **Fix:** Add `trap` cleanup or stage-then-rename; make exe-existence a hard error.

### P3-1: Pre-existing `OverflowException` uncaught in ExpressionService
- **File:** `src/ChapterTool.Core/Transform/ExpressionService.cs:~505`
- `double`→`decimal` cast on extreme-domain inputs; not in catch list. Pre-existing, not a regression.

### P3-2: MainWindow command subscriptions not detached on close
- **File:** `src/ChapterTool.Avalonia/Views/MainWindow.axaml.cs:498-506`
- Self-contained for single-window app; risk only if ViewModel outlives window.

### P3-3: Localized diagnostic template can leave unresolved placeholders
- **File:** `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs:1378-1393`
- Non-null but incomplete `Arguments` dict → unresolved `{token}` in UI text. No crash.

### P3-4: `publish.sh` glob-empty literal pattern
- **File:** `scripts/publish.sh:79-83`
- Empty output dir → `mv` tries literal `*`.

### P3-5: `publish.ps1` no rollback on interrupt
- **File:** `scripts/publish.ps1:36-38`

### P3-6: TextToolView permanent DataContextChanged handler
- **File:** `src/ChapterTool.Avalonia/Views/Tools/TextToolView.axaml.cs:18`

## Rejected False Positives

Two sub-agent P1 candidates were **rejected after main-agent verification**:

1. ~~"MissingOperatorBeforeFunction resx key missing"~~ — key exists at line 161 in all 3 resx files; all 18 `InvalidExpression.*` codes have complete coverage.
2. ~~"Selector identity mismatch from rebuilt list"~~ — code deliberately preserves identity via `UpdateFrom()` in-place mutation + value-based selection matching.

See `agent-findings.md` for full disproof evidence.

## Cross-module Systemic Issues

None identified. The branch's changes are well-contained within their layers. The i18n refactor touches Core (diagnostics), Infrastructure (deleted abstraction), and Avalonia (ViewModels + resx) coherently — the boundary contracts (diagnostic codes → resx keys → UI formatting) are intact end-to-end.

## Patterns Checked and Confirmed Clean (Cross-cutting Pass)

- All dispatch entries have default branches and param validation (except `publish.sh` arg guard).
- No new `.Result` / `.Wait()` / `async void` outside event handlers.
- No empty catches, no `dynamic`, no unsafe casts.
- No half-committed state in runtime data paths (chapter save, log capture, option refresh).
- resx key/placeholder parity verified programmatically (234 keys × 3 languages, 0 mismatches).
- Zero dangling references to deleted `ILocalizationService`/`LocalizationService` (repo-wide).
- Build: 0 warnings, 0 errors.

## Coverage

- **Fully reviewed:** Core (ExpressionService, diagnostics, importers, editing), Infrastructure (log provider, deleted abstraction), Avalonia ViewModels (all 4), Views (MainWindow + tool views), Info.plist + csproj, publish scripts, CI workflow, 3× resx, localization tests.
- **Out of scope:** `.codex/skills/**` markdown, `openspec/changes/**` planning docs, binary icon assets (only wiring reviewed).

## Recommended Merge Posture

**Mergeable as-is for functional correctness.** The P2 items are quality/hardening improvements that don't block the branch's stated goals (i18n completion + macOS bundle support). Recommend addressing P2-1 (culture mutation) and P2-6 (publish interrupt resilience) before the next release tag, as they affect runtime correctness and release-artifact integrity respectively. All other items can be follow-up.
