## Context

A full-source maintainability review of `src/` found that Core and Infrastructure are relatively modular, while the Avalonia shell has accumulated a **god-session** pattern:

| Hotspot | Approx. size | Problem |
|---------|--------------|---------|
| `MainWindowViewModel*.cs` | ~2095 lines | owns load/save/edit/clip/expression/settings/status/shell |
| `SettingsToolViewModel.cs` | ~878 lines | mega settings surface + live-apply into owner |
| `ExpressionEditor.axaml.cs` | ~862 lines | control + colorizer + diagnostics + completion |
| `MainWindow.axaml.cs` | ~675 lines | second orchestrator: dual commands, control scrapes |
| `ToolWindowViewModels.cs` | ~610 lines | unrelated tool VMs + text highlighter grab-bag |
| `AvaloniaWindowService.cs` | ~217 lines | string-id switch for titles/content |

Behavioral symptoms of the structural problem:

- Clip ownership uses multiple booleans/backups (`splitClipGroup`, `combinedClipOption`, `currentInfoBelongsToSelectedClip`, `IsClipCombineChecked`) instead of a typed mode.
- Save/preview/refresh scrape controls (`ReadAdvancedOptions`, `ReadFrameOptions`, `FormatBox.SelectedIndex`) because bindings are not authoritative.
- Cell edit routing matches localized headers (`"时间点"`, `"章节名"`, `"帧数"`, English equivalents).
- Almost every tool takes `MainWindowViewModel owner`.
- Preview constructs `new ChapterExportService(formatter)` outside the composition-owned save path.
- CLI has a parallel importer/export wiring path.

This change is a **behavior-preserving decomposition**. Product workflows stay the same; ownership and state models change so future work can delete complexity instead of bolting on more flags.

## Goals / Non-Goals

**Goals:**

- Make chapter session state inevitable: one workspace, typed clip mode, one projection surface, one export-preference snapshot.
- Preserve existing async anti-stale protections (operation revision, session-identity checks, late progress/result discard, cancellation) as first-class workspace contracts with mandatory regression tests.
- Make UI bindings the single source of truth for main workflow options.
- Collapse dual command surfaces and control-scrape orchestration.
- Give tools narrow ports and register tool windows by descriptor.
- Align CLI/GUI construction factories.
- Ship in ordered, independently mergeable PR slices, each with proportional full-solution verification gates.

**Non-Goals:**

- No chapter format, parser, export-content, or Lua evaluation semantics changes.
- No visual redesign of the main workflow layout or settings multi-page navigation (settings is modularized internally only).
- No DI container introduction unless a later change proves factories are insufficient.
- No move of session types into `ChapterTool.Core` in this change; workspace lives under Avalonia `Session/`.
- No deletion or folding of the dedicated Language tool in this change; it shares the preference path with Settings.
- No rewrite of `MplsPlaylistFile` or other Core format parsers in this change.
- No expansion of CLI to expression/advanced transforms.
- No combining of all slices into one PR; A→F remain designed as separate merge units (see decision 11 for the A+B delivery note).

## Target architecture

```text
AppCompositionRoot
  ├── shared factories: load registry, export, settings, localizer, expression
  ├── MainWindowViewModel  (thin shell: commands, bindable projections, status)
  │     └── ChapterWorkspace
  │           ├── SourceMetadata (path, display path)
  │           ├── ClipSession (Split | Combined)
  │           ├── EditBuffer (current ChapterSet + apply edit results)
  │           ├── ProjectionState (naming, order, expression, last good projection)
  │           └── ExportPreferences (format, xml lang, encoding, bom, save dir inputs)
  ├── IWindowService + ToolWindowRegistry
  │     └── tool factories(ports)
  └── CLI reuses same factories
```

### Clip session model (conceptual)

```csharp
// conceptual API — names can vary in implementation
abstract record ClipSession;

sealed record SplitClipSession(
    ChapterImportSource Group,
    int SelectedIndex) : ClipSession;

sealed record CombinedClipSession(
    ChapterImportSource OriginalGroup,
    ChapterImportEntry CombinedEntry) : ClipSession;
```

Transitions:

| Action | From | To |
|--------|------|----|
| Load success | any | `SplitClipSession` (or single-entry split) |
| Select clip | Split | Split with new index + current set from entry |
| Combine success | Split (multi, combinable) | Combined retaining original |
| Restore | Combined | Split original |
| Append MPLS | Split or Combined | Combined with expanded original + new combined set |
| Edit/frame update | Split/Combined | same mode, updated entry/current set |

Derived:

- `CanCombine`, `IsCombined`, `ClipOptions`, `CurrentChapterSet`, `RelatedMedia` come from mode, not sticky booleans.

### Async revision / anti-stale model

Today’s protection (must be preserved, not rediscovered later):

- Load increments `loadOperationVersion` and ignores progress/results when the version no longer matches (`MainWindowViewModel.ImportExport.cs`).
- Append captures operation version plus session object identity (`currentGroup` / `splitClipGroup`) and discards late results if either changed.
- Existing unit tests: overlapping loads; older append vs newer load (`MainWindowViewModelTests`).

Target model:

```text
WorkspaceRevision (monotonic)
  ├── Load(op): bind progress + result commit to revision R
  ├── Append(op): bind result commit to (revision R, sessionToken S)
  └── Any newer Load/session-replacing transition advances revision
```

Rules:

1. Progress callbacks apply only if `op.Revision == workspace.CurrentRevision`.
2. Load result commits only if revision still matches; success replaces session atomically, failure updates status only for current revision.
3. Append commits only if revision matches **and** `sessionToken` still identifies the active clip session (typed token, not ad-hoc parallel flags).
4. Cancellation before commit never applies partial session mutation.
5. Stale failure after a newer commit must not clobber the newer path/rows; logging is optional.

### Shell ports (conceptual)

```text
IExpressionSessionPort   // read/apply expression + diagnostics display
IPreferenceSink          // live-apply app prefs into workspace/localizer
IExportPreferencePort    // read/write session save format etc. for preview
IChapterEditPort         // forward-shift / zones style mutations if needed
IUiLanguageController    // language tool + settings share this path
```

`MainWindowViewModel` can implement these ports directly at first (adapter on workspace). Tools depend on the port interfaces, not the concrete main VM type.

## Decisions

1. **`ChapterWorkspace` lives under Avalonia `Session/` for this entire change (closed).**

   Why: all current consumers are Avalonia shell/tools. Projection-invalid-row retention, live settings policy, status/progress revision handling, and tool ports are shell product rules today. Moving into Core early would drag UI-adjacent policy across the project boundary and force premature code-map ownership churn.

   Alternative considered: pure Core session / early Core promotion. **Rejected for this change.**

   Promotion rule (future change only): if a type becomes free of Avalonia/localization and useful to CLI, promote only that pure piece in a separate OpenSpec change.

2. **Replace multi-flag clip state with `Split | Combined` before large VM file splits.**

   Why: this is the highest-leverage judo move. File splits without state cleanup just redistribute spaghetti.

   Alternative: extract partial classes / folders first. Rejected; that already happened and did not reduce concepts.

3. **Bindings become authoritative; remove production control scrapes.**

   Why: `ReadAdvancedOptions` is the dual-source root cause for option drift.

   Implementation notes:
   - Bind `SaveFormatIndex`, `OrderShift`, `ApplyExpression`, `Expression`, naming mode, frame options, path.
   - For AvaloniaEdit-based expression editor, keep two-way `Text` binding already present; ensure command paths do not re-read the control.
   - Path box: bind to `CurrentPath`/`DisplayPath` or a dedicated bindable path property; browse/drop set the VM property.

   Alternative: keep scrapes “just before save”. Rejected; that preserves the bug class.

4. **Keep one business command surface on the ViewModel.**

   Window may wrap commands only to supply view parameters (`SelectedIndexes`, picker path). Can-execute business rules stay on VM commands.

   Alternative: CommunityToolkit/ReactiveUI command infrastructure. Out of scope; keep `UiCommand` unless a later change proves a framework switch necessary.

5. **Stable grid column identity via `Tag` or typed column id enum.**

   Prefer `Tag="Time|Name|Frames"` or attached identity over header strings. Headless/unit tests cover all three languages.

   Alternative: binding-path inspection only. Acceptable if reliable with DataGrid; Tag is simpler and explicit.

6. **Tool window registry as data + factories.**

   ```csharp
   sealed record ToolWindowRegistration(
       string Id,
       string TitleResourceKey,
       Func<ToolWindowCreateContext, Control> CreateContent,
       double? PreferredWidth = null);
   ```

   `AvaloniaWindowService` iterates registrations instead of triple switch (title/content/placeholder).

   Alternative: full navigation framework. Overkill for a multi-window tool host.

7. **Narrow ports over full owner references.**

   Migration can be incremental:
   1. Extract interfaces implemented by `MainWindowViewModel`.
   2. Change tool constructors to take interfaces.
   3. Optionally extract interface implementations into workspace later.

   Do not invent a mini-DI container for tools; factories in window service/composition are enough.

8. **Settings modularization is ownership split only (closed). No multi-page settings UX redesign in this change.**

   Modules:
   - `SettingsAppearanceViewModel` (already exists)
   - `SettingsExternalToolsViewModel` (paths, validate, discover)
   - `SettingsOutputDefaultsViewModel` (save dir/format/xml/encoding/bom/tolerance)
   - `SettingsAboutViewModel` or simple about properties
   - `SettingsToolViewModel` becomes orchestrator: load/save/dirty/close, children

   Alternative considered: multi-page navigation redesign. **Rejected for this change.**

9. **Expression editor split is presentational and theme-aware in Slice E (closed).**

   Files:
   - `ExpressionEditor.axaml.cs` — shell/control API
   - `ExpressionCompletionPresentation.cs`
   - `ExpressionDiagnosticPresentation.cs`
   - `ExpressionColorizer.cs`
   - Theme brushes in `App.axaml` / theme resources for completion category colors

   Keep `ExpressionAuthoringService` in Core as the analysis authority. Theme-driven category/chrome colors are in scope for Slice E, not a fast-follow.

10. **CLI shares factories, not the GUI workspace.**

    CLI still does basic convert without expression session. It only reuses construction of registry/exporter/settings directory rules.

11. **PR slicing A→F is intended as independent merge units (closed; A+B delivery note).**

    Each slice is designed as an independently mergeable unit with its own verification gate. Do not land “workspace + bindings + tools + CLI” as one PR.

    **Delivery note:** Slice A (typed clip session) and Slice B (ChapterWorkspace facade) were landed together in a single implementation commit during apply (commit message “slices A–B”), with combined verification covering both concurrent anti-stale regressions and workspace facade tests. That packaging choice is recorded here so artifacts match history; remaining slices C→F stayed separate. Future work that needs attributable clip-session vs workspace-facade diffs should still prefer separate commits when practical.

12. **Language tool is retained; shared preference path only (closed).**

    The dedicated Language tool remains. Language apply/persist goes through the same preference/session path used by Settings. Folding Language into Settings is out of scope for this change.

13. **Async revision is part of the workspace contract, not an incidental ViewModel field.**

    Operation revision / session-token checks move with load and append into the workspace/shell orchestration that owns commits. Deleting multi-flag clip state must not delete anti-stale protection. Existing concurrent unit tests are merge blockers for Slices A and B.

## Migration Plan (ordered PR slices)

### Shared merge gate (every independently mergeable slice)

Before merge, every slice MUST:

1. Build the affected projects (`dotnet build` on Avalonia app and/or solution as needed after restore when assets change).
2. Run focused tests for the changed surface.
3. When UI shell, tool windows, settings views, or ExpressionEditor presentation change: run focused `ChapterTool.Avalonia.Headless.Tests` and wait for it to finish.
4. Run `dotnet test ChapterTool.Avalonia.slnx` as the full-solution gate (this single invocation may include Headless and other test projects).
5. **Never launch multiple external `dotnet test` processes in parallel.** Finish any focused Headless command before starting the full-solution gate.
6. Keep Headless in its dedicated project/testhost per repository process-isolation rules. That isolation requirement is about separate projects/testhosts, not a ban on solution-level `dotnet test`.

Focused tests alone are **not** sufficient to claim a slice is independently mergeable.

### Slice A — Clip session typed model (highest leverage)

1. Add `ClipSession` (`Split`/`Combined`) and transition helpers.
2. Move combine/restore/append/select/write-back logic onto transitions **while preserving operation revision + session-token anti-stale checks**.
3. Delete sticky booleans once call sites are migrated.
4. Unit tests: combine, restore, append, load replacement, edit write-back ownership.
5. **Mandatory concurrent regressions:** overlapping loads; older append does not overwrite newer load; late progress ignored (preserve/adapt existing `MainWindowViewModelTests` coverage).
6. Update `docs/code-map/avalonia.md` for Avalonia `Session/` clip ownership.

**Exit criteria:** no independent sticky clip flags; MPLS/IFO combine/append parity; concurrent anti-stale tests green; shared merge gate green.

### Slice B — Workspace facade around session + projection + export prefs

1. Introduce `ChapterWorkspace` under Avalonia `Session/` owning source metadata, clip session, current set, projection state, export prefs, and revision commit APIs.
2. `MainWindowViewModel` delegates load/save/edit/expression refresh to workspace.
3. Ensure load/append progress and results commit only through workspace revision rules.
4. Unit tests for projection invalid retention, export snapshot coherence, cancellation/non-commit, and concurrent anti-stale scenarios still pass after the move.
5. Preview uses injected export/projection services (kill ad-hoc `new ChapterExportService` in preview path).

**Exit criteria:** session fields are not independently mutated outside workspace APIs; anti-stale contract owned by workspace/shell orchestration; shared merge gate green.

### Slice C — Binding authority + single command surface + stable grid identity

1. Bind path/format/naming/expression/order/frame options; remove production `Read*` scrapes.
2. Collapse duplicate window commands where possible; keep only parameter-adapting wrappers.
3. Unify shortcut routing so gestures hit the same VM commands.
4. Stable column identity for cell commits; remove bilingual header matching.
5. Headless/unit tests for option binding → save/preview and localized edit routing.

**Exit criteria:** `MainWindow.axaml.cs` no longer owns option business sync; grid edit works in zh/en/ja without header string tables; focused unit + Headless + full solution gate green.

### Slice D — Tool ports + window registry

1. Define ports; implement on VM/workspace.
2. Refactor tool VMs to depend on ports.
3. Replace window-service string switches with registration table.
4. Ensure composition localizer/export instances are required (no silent `new AppLocalizationManager()` on production path).
5. Unit tests construct tools against fakes; Headless covers tool open/lifecycle where UI changes.

**Exit criteria:** tools compile/test without concrete `MainWindowViewModel` dependency (except temporary adapters); tool lifecycle tests green; shared merge gate green.

### Slice E — Settings modules and ExpressionEditor presentation

1. Split settings child modules; keep dirty/save/close orchestration; **no multi-page UX redesign**.
2. Split ExpressionEditor presentation types; **theme-aware brushes in this slice**.
3. Split `ToolWindowViewModels.cs` into per-tool files; keep Language tool.
4. Focused settings/expression tests + Headless for changed surfaces.

**Exit criteria:** settings/expression ownership modularized; category colors resolve from theme/semantic resources; shared merge gate green.

### Slice F — CLI/GUI shared factories + final validation

1. Expose composition factory methods for registry/export.
2. Point CLI defaults at those factories.
3. Composition smoke tests; CLI tests still inject fakes.
4. Final docs/code-map update; openspec archive readiness.
5. Full solution gate + `openspec validate "decompose-main-window-session" --strict`.

**Exit criteria:** CLI/GUI share factories; docs updated; full validation green.

### Rollback strategy

- Each slice is independently revertable and must stay merge-green on its own.
- No data migration (settings schema unchanged).
- If a slice regresses UX, revert that slice; workspace types can remain if unused without behavior change.

## Risks / Trade-offs

- **[Risk] Behavior drift in combine/append edge cases.**  
  → Mitigation: port existing Avalonia unit tests first; add pure session tests before deleting flags; keep golden diagnostics assertions.

- **[Risk] Losing async anti-stale protection during refactor.**  
  → Mitigation: formal workspace revision contract; mandatory concurrent load/append/late-progress tests as Slice A/B merge blockers; append keeps session-token check, not only a version integer.

- **[Risk] Claiming “independent slice” while only running focused tests.**  
  → Mitigation: every mergeable slice runs focused tests plus `dotnet test ChapterTool.Avalonia.slnx`; UI slices also run a focused Headless command first, then the full-solution gate, without concurrent external `dotnet test` processes.

- **[Risk] Binding changes break headless option→save coverage.**  
  → Mitigation: update headless tests in the same slice as binding authority; do not leave scrape helpers as production fallback.

- **[Risk] Port explosion / over-abstracting tools.**  
  → Mitigation: start with few ports mapped to real tool needs; no generic mediator bus.

- **[Risk] Large PR fatigue.**  
  → Mitigation: enforce separate A→F slices; do not merge incomplete half-migrations of clip flags.

- **[Risk] Theme-aware expression colors look worse initially.**  
  → Mitigation: map categories to semantic brushes with light/dark pairs from preset palettes; visual check in headless screenshots optional.

- **[Risk] Temporary dual model during migration.**  
  → Mitigation: keep adapter methods on workspace; forbid new call sites from touching old fields; delete old fields at end of slice A/B.

## File ownership sketch (after full migration)

```text
src/ChapterTool.Avalonia/
  Session/
    ChapterWorkspace.cs
    ClipSession.cs
    ProjectionState.cs
    ExportPreferences.cs
    Ports/*.cs
  ViewModels/
    MainWindowViewModel*.cs          # thinner shell
    Settings/
      SettingsToolViewModel.cs
      SettingsAppearanceViewModel.cs
      SettingsExternalToolsViewModel.cs
      SettingsOutputDefaultsViewModel.cs
    Tools/
      ExpressionToolViewModel.cs
      LanguageToolViewModel.cs
      ...
  Services/
    ToolWindowRegistry.cs
    AvaloniaWindowService.cs
  Views/Controls/Expression/
    ExpressionEditor.*
    ExpressionColorizer.cs
    ...
  Composition/AppCompositionRoot.cs  # shared factories for CLI too
```

Folder names under Avalonia may be refined slightly, but **workspace ownership stays in Avalonia `Session/`** for this change (see Closed Decisions).

## Closed Decisions (architecture choices finalized)

These were previously open questions; they are now formal decisions for this change. Implementation MUST NOT re-open them without an OpenSpec amendment.

| # | Decision | Choice |
|---|----------|--------|
| 1 | Language tool | **Retain** dedicated Language tool; share preference/session path with Settings. Do not fold into Settings in this change. |
| 2 | Workspace location | **Avalonia `Session/`** for the entire change. No Core move in this change. Update `docs/code-map/avalonia.md` accordingly. |
| 3 | Slice packaging | **Separate merge units A→F**. Do not big-bang. Do not casually merge A+B. |
| 4 | Settings UX | **Internal modularization only**. No multi-page settings navigation redesign. |
| 5 | Expression theming | **Theme-aware category/chrome colors in Slice E** together with the file split. Not a fast-follow. |

Additional closed product/engineering decisions:

| Topic | Choice |
|-------|--------|
| Async anti-stale | Formal workspace revision + session-token contract; mandatory concurrent regressions on A/B |
| Slice verification | Focused tests + full `ChapterTool.Avalonia.slnx` gate every mergeable slice; no parallel external `dotnet test`; focused Headless completes before solution gate when used; Headless stays dedicated testhost |
| DI container | Not introduced |
| CLI scope | Shared factories only; no expression expansion |
