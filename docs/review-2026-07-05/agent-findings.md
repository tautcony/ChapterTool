# Agent Findings — Sub-agent Dispatch Log

> Record of sub-agent dispatches, candidate findings, adoption/rejection decisions, and verification notes.

## Dispatch Summary

| Phase | Task ID | Duration | Candidate Findings | Adopted | Rejected | Downgraded |
|-------|---------|----------|--------------------|---------|----------|-------------|
| 1 Core | bg_b9a2378f | 9m21s | 2 + 1 suspect | 2 (P2,P3) | 1 (P1 FP) | — |
| 2 Infra | bg_5f185fb5 | 9m47s | 2 + 3 suspects | 2 (P2,P2) | — | 1 (P1→P2) |
| 3 VM | bg_cbd33c95 | 7m52s | 4 + 3 suspects | 2 (P2,P3) | 1 (P1 FP) | 1 (P2→P3) |
| 4 Views | bg_1c8af626 | 6m31s | 3 + 3 suspects | 3 (P2,P3,P3) | — | 1 (P1→P2), 1 (P2→P3) |
| 5 Scripts | bg_183f5248 | 4m57s | 3 + 3 suspects | 4 (P2,P2,P3,P3) | — | — |
| 6 L10n | bg_0defc2e9 | 5m47s | 0 | 0 | — | — |

## Rejected Findings (False Positives)

### Phase 1 — ~~P1: MissingOperatorBeforeFunction missing resx key~~

**Sub-agent claim:** The code `InvalidExpression.MissingOperatorBeforeFunction` thrown at `ExpressionService.cs:262` has no corresponding `Diagnostic.InvalidExpression.MissingOperatorBeforeFunction` key in the resx.

**Main-agent verification:**
```
$ grep -n "MissingOperatorBeforeFunction" Strings.en-US.resx
:161:  <data name="Diagnostic.InvalidExpression.MissingOperatorBeforeFunction" ...>

$ comm -23 <(all codes thrown) <(all keys present in resx)
(empty)
```

**Verdict: REJECTED.** Key exists at line 161 in all three resx files. All 18 `InvalidExpression.*` codes have complete coverage.

**Root cause of sub-agent error:** The sub-agent checked for key presence by reading a narrow line range (`:158-166`) and didn't find the key in that window, but it was at `:161` — likely a parsing/line-counting discrepancy in the sub-agent's resx read.

---

### Phase 3 — ~~P1: Selector identity mismatch (recomputed list + reference equality)~~

**Sub-agent claim:** `XmlLanguageDisplayOptions` returns a new array every getter call; `SelectorDisplayOption` has no value equality → UI selection desync.

**Main-agent verification:**
- `XmlLanguageDisplayOptions => xmlLanguageDisplayOptions` returns the **same persistent `ObservableCollection`** (`MainWindowViewModel.cs:289`), not a new array.
- `RefreshXmlLanguageDisplayOptions` (`:1463-1487`) preserves identity:
  - Count unchanged → `UpdateFrom()` mutates in place.
  - Count changed → clear + re-add on same collection instance.
- `SelectedXmlLanguageDisplayOption` setter matches by `MainText` value (`:304`), not reference.
- Getter indexes by `XmlLanguageIndex` integer (`:296`), not reference.

**Verdict: REJECTED.** The code explicitly handles identity preservation — this is deliberate defensive design, not a bug.

**Root cause of sub-agent error:** The sub-agent inferred "recomputed array" from the `XmlLanguageDisplay.Options()` factory method without reading the `RefreshXmlLanguageDisplayOptions` caller that bridges the factory output into a persistent collection with identity-preserving mutation.

## Downgraded Findings (Severity Calibration)

| Original | Calibrated | Rationale |
|----------|------------|-----------|
| Phase 2 P1 (event race) | **P2** | Real C# event race, but impact bounded to logging path in a desktop app. Worst case: rare NRE in `ILogger.Log`, not data corruption. |
| Phase 3 P1 (culture mutation) | **P2** | Real design anti-pattern, but `Options()` call sites are UI-thread-bound and infrequent (culture change only). Cross-thread impact requires a concurrent background operation reading `CurrentCulture` during the brief `using` window. |
| Phase 4 P1 (CFBundleExecutable) | **P2** | Names match by default (no `AssemblyName` override). The real defect is the silent chmod skip, not the name itself. |
| Phase 3 P2 (live refresh leak) | **P3** | `TextToolView.axaml.cs:40` explicitly calls `DetachLiveRefresh()` on DataContext change — the View handles the lifecycle. |
| Phase 4 P2 (no unsubscribe) | **P3** | Main window owns its ViewModel; subscription graph is self-contained and collectable together. |

## Verification Commands Run by Main Agent

1. `grep -n "MissingOperatorBeforeFunction" Strings.*.resx` — key presence in all 3 files.
2. `comm -23 <(thrown codes) <(resx keys)` — zero missing diagnostic codes.
3. `grep -rn "ILocalizationService|LocalizationService" --type cs` repo-wide — zero dangling refs.
4. `dotnet build ChapterTool.Avalonia.slnx` — 0 warnings, 0 errors.
5. Full read of `ApplicationLogPanelProvider.cs`, `Info.plist`, `publish.sh` macOS block, `ToolWindowViewModels.cs`, `RefreshXmlLanguageDisplayOptions`.
6. `grep "IsChapterGridEmpty"` — overlay binding verified.
7. `grep "DetachLiveRefresh"` — View calls detach.
8. Empty-catch / `dynamic` / unsafe-cast scan — clean.

## Assessment of Sub-agent Quality

- **Phase 6 (Localization):** Excellent — thorough, programmatic, all claims verified. No false positives.
- **Phase 5 (Scripts):** Strong — accurate shell analysis, correct severity calls.
- **Phase 4 (Views):** Good on Info.plist audit and AGENTS.md compliance; slightly over-conservative on severity calibration.
- **Phase 2 (Infra):** Correct dangling-ref audit; P1 was technically right but over-calibrated for a desktop logging path.
- **Phase 3 (VM):** Correct XmlLanguageDisplay culture analysis; **produced a false positive** on selector identity by not reading the refresh method.
- **Phase 1 (Core):** Correct ExpressionService evaluation spot-checks; **produced a false positive** on the missing key by misreading the resx.

**False-positive rate:** 2 of 13 candidate findings rejected (15%). Both were P1 claims that would have been the highest-severity items — highlighting the importance of main-agent verification for P0/P1 candidates per the skill protocol.
