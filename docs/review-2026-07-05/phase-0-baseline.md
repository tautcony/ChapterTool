# Phase 0 — Baseline

> Code review of `feat/improve-struct` vs `master`, including uncommitted changes.
> Generated: 2026-07-05 (Asia/Shanghai)

## Branch & Change Surface

- **Branch**: `feat/improve-struct`
- **Commits ahead of master**: 5
  - `2e2f18f` feat: Add macOS app bundle support and update application icon
  - `c8522ba` test: speed up Avalonia unit test execution
  - `25e4752` fix: Correct case of 'Full' in blame-hang-dump-type for CI
  - `260c053` feat: Enhance ChapterDiagnostic with Arguments and improve error handling in expression evaluation
  - `eb42d91` feat: Update store selection process and enhance log service integration
- **Uncommitted**: 12 modified + 3 untracked files (i18n follow-ups + new `XmlLanguageDisplay` ViewModel + new `show-chapter-empty-state` OpenSpec change).
- **Combined diff vs master (committed + uncommitted)**: 73 files changed, +4,161 / −810.

## Tech Stack & Module Boundaries

- .NET 10 / Avalonia 11 desktop app for chapter editing.
- Solution: `ChapterTool.Avalonia.slnx`
  - `src/ChapterTool.Core` — domain: models, transformations (ExpressionService), importers (text/XML/OGM/cue/etc.), exporters, diagnostics, editing.
  - `src/ChapterTool.Infrastructure` — external tools, process exec, settings, infra-backed importers (BDMV/Matroska/MP4 via ffprobe).
  - `src/ChapterTool.Avalonia` — UI: ViewModels, Views (.axaml + code-behind), Services, Localization resx.
  - `tests/` — Core.Tests, Infrastructure.Tests, Avalonia.Tests (incl. headless).
- Build verified at baseline: **`dotnet build ChapterTool.Avalonia.slnx` → 0 warnings, 0 errors.**

## Intent of This Branch (per `docs/i18n-audit-and-fix-plan.md` & OpenSpec change)

1. **i18n refactor** (largest portion of the diff):
   - P0: Add ~91 new `Diagnostic.*` keys (3 languages), wire `ChapterDiagnostic.Arguments`, route `LocalizeDiagnostic` through `Localizer.Format`.
   - P1: Add `Action.*`, `EditKind.*`, `Operation.*` keys; refactor `ApplyEdit` / `LogDiagnostics` to take localized keys instead of raw English strings.
   - P2: New `ExpressionException(code, message, args)` in `ExpressionService`; 18 `InvalidExpression.*` codes.
   - P3: `MissingDependency` / external-tool messages localized at the importer boundary.
   - P4: **Delete** `ILocalizationService` + `LocalizationService` (Core/Infrastructure) — was unused dead code.
2. **macOS app bundle support**: new `Assets/MacOS/Info.plist`, app-icon assets (`icns`/`ico`/updated `svg`), `csproj` bundle config, new `scripts/publish.{ps1,sh}` helpers.
3. **Empty-grid UI feature** (uncommitted, OpenSpec change `show-chapter-empty-state`): render `chapter-empty.svg` overlay when grid is empty; new headless tests.
4. **New `XmlLanguageDisplay` ViewModel** (untracked): provides display-name localization for the XML chapter language `<lang>` selector.

## Phase Plan (parallel sub-agents)

| Phase | Layer | Scope |
|---|---|---|
| 1 | Core domain | `src/ChapterTool.Core/**` |
| 2 | Infrastructure | `src/ChapterTool.Infrastructure/**` (incl. deleted-abstraction dangling-ref audit) |
| 3 | Avalonia ViewModels | `ViewModels/**` (incl. new `XmlLanguageDisplay`) |
| 4 | Avalonia Views + bundle config | `Views/**`, `.axaml.cs`, `csproj`, `Info.plist`, `Assets/`, `Services/` |
| 5 | Build / publish | `scripts/**`, `.github/workflows/**`, `.gitignore` |
| 6 | Localization | 3× `resx`, `LocalizationTests.cs` |
| 7 (me) | Cross-cutting differential disproof | Horizontal sweep across all phases |
| 8 (me) | Aggregation | `summary.md`, `fixes-plan.md` |

## High-Risk Surface Map (preliminary — to be confirmed/denied by phases)

The following are the most likely defect clusters, listed before reading the diffs so sub-agent findings can be checked against this expectation:

1. **`XmlLanguageDisplay` global culture mutation** (Phase 3): `TemporaryCurrentUiCulture : IDisposable` mutates `CultureInfo.CurrentCulture`/`CurrentUICulture` process-wide inside a `using`. Race-prone, exception-unsafe if `GetCultureInfo` throws after assignment, and impacts any concurrent culture-sensitive code. **Pre-rated P1 pending Phase 3 evidence.**
2. **`ILocalizationService` deletion dangling references** (Phase 2): If DI registration or any test still references the deleted type → runtime ActivatorException or compile failure. (Build passed → compile is clean; DI/runtime resolution still needs evidence.)
3. **Localization key/placeholder parity** (Phase 6): 91 new keys × 3 languages = lots of room for missing keys or mismatched `{name}` placeholders → runtime `FormatException` or English fallback.
4. **macOS `Info.plist` correctness** (Phase 4): Missing `CFBundleIdentifier`/`CFBundleExecutable`/version keys → bundle won't launch on macOS.
5. **`publish.sh` shell quoting** (Phase 5): Unquoted `$VAR` expansions on paths with spaces → silent partial artifacts.
6. **`ExpressionService` evaluation regression** (Phase 1): The refactor touches the core expression evaluator; verify precedence, div-by-zero, NaN, and that all throw sites now use `ExpressionException` (no dropped error cases).

## Anti-Pattern Coverage Plan

The following high-leakage patterns will be checked by every phase AND in the final cross-cutting pass:

- Default branch on unknown input / unknown diagnostic code / unknown lang code.
- Async failure propagation (cancellation, IO failure → status bar / log panel).
- Half-committed state across persistence + cache + observable collections.
- Deferred-execution context invalidation (culture swap, captured closures, event handlers firing after window close).
- Single-point utility assumptions: encoding (resx UTF-8), time (frame-rate math), collision (lang codes), empty (null `Arguments`).
- Re-entrant UI (rapid button clicks, window close mid-async).

## Out of Scope (explicitly excluded)

- `.codex/skills/**` markdown files (tooling docs, no runtime behavior).
- `openspec/changes/**` proposal/design/tasks markdown (planning artifacts).
- Binary icon assets (`icns`/`ico`) — only their wiring (csproj `BuildAction`) is reviewed.
- Subjective translation quality — only structural/behavioral defects (missing keys, placeholder mismatches, UTF-8 mojibake).
- Style/formatting nits.

## Pre-existing Review Context

- `docs/review-2026-06-10/` exists but `summary.md` is empty; not a useful baseline.
- `docs/i18n-audit-and-fix-plan.md` documents the i18n refactor as "completed" with green tests; Phase 6 will verify that claim structurally rather than trust it.
