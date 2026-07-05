# Phase 7 — Cross-cutting Differential Disproof Pass

> Horizontal sweep across all phases, independent of phase ordering. Goal: catch systemic patterns that individual phase reviews might miss.

## Methodology

Instead of reviewing by layer, this pass sweeps by **anti-pattern category** across the entire diff, looking for instances that might have fallen between phase boundaries.

## Sweep Results

### 1. All dispatch/command/protocol entries: default branch, param validation, failure回传

| Entry point | Default branch | Param validation | Failure propagation | Verdict |
|-------------|----------------|-------------------|---------------------|---------|
| `ExpressionService.Tokenize/ToPostfix/EvaluatePostfix` | ✅ all token types → explicit diagnostic | ✅ | ✅ `ExpressionException` | Clean |
| Importers (OGM/XML/BDMV) | ✅ unknown format → `ChapterImportResult.Failed` | ✅ extension check | ✅ diagnostics returned | Clean |
| `LocalizeDiagnostic` | ✅ key-miss → fallback to `diagnostic.Message` | ✅ null args guard | ✅ replace-based (no FormatException) | Clean |
| `XmlLanguageDisplay.LanguageDisplayName` | ✅ unknown code → `EnglishDisplayName` fallback | ✅ CultureNotFoundException catch | ✅ | Clean |
| `publish.sh` arg parser | ⚠️ missing `$2` guard | ❌ | `set -u` abort (P2) | Flagged |

### 2. All async chains: failure, cancellation, timeout, idempotency, context invalidation

| Async path | Failure handling | Cancellation | Reentrancy | Verdict |
|------------|-----------------|--------------|------------|---------|
| `MainWindow.OnDrop` (async void) | ✅ try/catch → status | N/A (no token) | ✅ command CanExecute | Clean |
| `MainWindow.OnOpened` (async void) | ✅ per Phase 4 | — | — | Clean |
| `MainWindow.OnKeyDown` (async void) | ✅ per Phase 4 | — | command-level | Clean |
| `TextToolViewModel.OnEntryAdded` | ✅ UI-thread marshalled | N/A | ✅ refresh is idempotent | Clean |
| `ColorSettingsViewModel.LoadAsync` | ✅ store-null guard | `CancellationToken.None` | ✅ | Clean |

**No new `.Result` / `.Wait()` introduced anywhere in the diff.**

### 3. All state-write chains: "half-committed state from ordering errors"

| State write | Atomicity | Rollback | Verdict |
|-------------|-----------|----------|---------|
| `RuntimeChapterSaveService` | ✅ "Saved" diagnostic only after successful write | N/A (file write) | Clean |
| `RefreshXmlLanguageDisplayOptions` | ✅ count-check → either clear+add or in-place UpdateFrom | N/A | Clean |
| `ApplicationLogPanelProvider.Capture` | ✅ entry built before lock; added under lock; capacity trim under same lock | N/A | Clean |
| `publish.sh` macOS bundle move | ❌ non-atomic multi-file `mv` loop, no trap | ❌ no rollback (P2) | Flagged |

### 4. All rebuild/batch/cleanup/migration chains: "remove-then-rebuild data window"

| Rebuild path | Window risk | Verdict |
|--------------|-------------|---------|
| `RefreshXmlLanguageDisplayOptions` clear+add (count change) | Brief empty collection during clear+add — but UI binding reads on `PropertyChanged` which fires AFTER rebuild completes | Clean |
| `LanguageToolViewModel.ReplaceLanguages` clear+add | Same pattern — `OnPropertyChanged` after rebuild | Clean |
| `publish.sh` `rm -rf "$app_bundle"` then rebuild | Window exists but acceptable for publish artifact (not runtime data) | Acceptable |

### 5. All content-rendering / rich-text / export chains: security boundary, scale

| Render point | Input source | Scale boundary | Verdict |
|--------------|-------------|----------------|---------|
| `TextToolViewModel.Format` (JSON/XML pretty-print) | User chapter export text | ✅ catches `JsonException`/`XmlException` → returns raw text | Clean |
| `HighlightJson`/`HighlightXml` | Line-level text | O(n) per line, no regex backtracking | Clean |
| `LocalizeDiagnostic` | Diagnostic args (bounded set) | Replace-based, no composite format injection | Clean |

### 6. All high-leverage utility functions: encoding, time, collision, naming, compatibility

| Utility | Risk | Verdict |
|---------|------|---------|
| `XmlLanguageDisplay` culture swap | Process-global mutation (P2) | Flagged |
| `ExpressionService` double→decimal cast | OverflowException uncaught (P3, pre-existing) | Flagged |
| `Uri.IsHexDigit` usage in color parsing | ✅ correct hex validation | Clean |
| `AppLanguage.Normalize` | ✅ culture-name normalization | Clean |
| resx placeholder formatting | ✅ named-only, verified parity | Clean |

## New Findings from This Pass

**None.** All patterns flagged in this sweep were already captured by the phase reviews. No cross-cutting issue fell between phase boundaries.

## Verdict

The phase decomposition + this horizontal sweep provide **complete coverage** of the diff's high-risk surface. The two highest-risk categories (async/concurrency and state-write atomicity) are clean except for the already-flagged `publish.sh` interrupt window and `XmlLanguageDisplay` ambient mutation.
