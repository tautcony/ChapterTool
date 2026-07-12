## Context

Source of truth for residual debt: `docs/code-review-src.md`.

Prior change `decompose-main-window-session` introduced Avalonia `Session/` (`ChapterWorkspace`, `ClipSession`, ports, tool registry, binding authority). That work is complete as an OpenSpec change but left:

| Residual | Evidence |
|----------|----------|
| God orchestration | `MainWindowViewModel*` ~2160 lines still owns load/save/edit/expression/status |
| Dead Settings modules | `SettingsExternalToolsModule` / `SettingsOutputDefaultsModule` / `SettingsAboutModule` unreferenced |
| Dual commands | `MainWindow.axaml.cs` still wraps many VM commands |
| Expression self-DI | `ExpressionEditor` does `new ExpressionAuthoringService()` |
| XML safety | `XmlDocument.Load` without secure settings |
| Binary bounds | `ReadExactBytes(int)` allocates from untrusted lengths |

Constraints:

- Behavior-preserving for chapter load/edit/export workflows.
- Do not reintroduce Headless/unit process mixing; sequential or full-solution tests only.
- Prefer deleting complexity over new indirection layers that do not own state.
- Telemetry remains enabled by default. The prior review's opt-in/PII recommendation is intentionally excluded by product decision and is not an implementation task for this change.

## Goals / Non-Goals

**Goals:**

1. Close P0 security gaps with small, testable Core/Avalonia changes.
2. Finish P1 structural debt: real workflow coordinators, real or deleted Settings modules, single command surface, shared expression services.
3. Make composition lifecycle and production dependency nullity intentional.
4. Ship as ordered merge slices with full-solution gates (same discipline as the prior decomposition).

**Non-Goals:**

- Rewriting MPLS business logic or moving workspace types into Core.
- Expanding CLI to expression/advanced transforms.
- Introducing a full DI container (lightweight composition discipline is enough unless proven insufficient).
- Visual redesign of main window or multi-page settings navigation.
- Archiving `decompose-main-window-session` (may be done separately; this change must not depend on archive order for implementation).
- Product marketing claims of “unbreakable Lua sandbox.”

## Decisions

### 1. Slice order: security first, then shell, then composition polish

| Slice | Focus | Why first/next |
|-------|--------|----------------|
| **A** | XML secure load + binary read bounds | Small blast radius, independent of UI |
| **B** | Settings modules: wire or delete | Stops half-extraction before more settings work |
| **C** | Extract load/save + projection + status coordinators | Main structural win; depends on stable workspace APIs already present |
| **D** | Command surface collapse + ExpressionEditor injection | Completes shell boundaries |
| **E** | Composition lifecycle + non-null production deps | Cleanup after extractors exist |

**Alternative considered:** Big-bang shell rewrite first. Rejected—security fixes should not wait on a multi-PR orchestration migration.

### 2. Secure XML loading via shared helper, not ad-hoc per call site

Introduce a small Core (or Core.Importing.Text) helper that loads XML with:

- `DtdProcessing.Prohibit` (or equivalent)
- `XmlResolver = null`
- no network resolution

Used by `XmlChapterImporter` and reviewed for `XplChapterImporter` (`XDocument` path). Prefer one helper over duplicating `XmlReaderSettings` in every importer.

**Alternative:** Switch everything to `XDocument` only. Acceptable if helper still centralizes secure settings; do not leave `XmlDocument` insecure “for convenience.”

### 3. Binary allocation bounds

Define explicit maximums for every MPLS-related untrusted length and count before allocation or iteration (playlist structures, play items, subpaths, stream entries, extension data blocks, and marks), consistent with realistic Blu-ray playlists. Every container parser also validates that its declared length can contain the mandatory header and consumed entries; malformed containers MUST fail rather than seeking backwards or skipping a negative remainder. `ReadExactBytes` either:

- takes a max and throws `InvalidDataException` when `length` is negative or exceeds it, or
- callers clamp/validate before calling.

Prefer **caller validation at semantic boundaries** plus a defensive max on `ReadExactBytes` so no path can pass multi-GB lengths or use an untrusted count as an unchecked collection capacity.

**Alternative:** Stream without buffering large blocks. Higher effort; only needed if legitimate playlists approach bounds—document chosen limits in code comments/tests.

### 4. Shell coordinators consume `ChapterWorkspace`; VM stays bindable façade

Target shape:

```text
MainWindowViewModel (INotify + commands + ports)
  ├── ChapterWorkspace              (existing state owner)
  ├── LoadSaveWorkflow              (load/append/save/progress commit)
  ├── ClipEditingCoordinator        (select/combine/edit cells/frame ops)
  ├── ProjectionFacade              (expression/naming/order → rows + export options)
  └── StatusDiagnosticsPresenter    (status keys, log, diagnostic localization)
```

Rules:

- Coordinators own **orchestration**, not duplicate session state.
- Anti-stale revision/session-token rules stay on workspace; workflows only call workspace commit APIs.
- Ports (`IExpressionSessionPort`, etc.) may be implemented by the thin VM **or** by a small façade that forwards to coordinators—do not force tools to take four coordinator types unless needed.

**Alternative:** Only more partial files. Rejected by review: partials do not delete concepts.

**Size gate:** After Slice C, `MainWindowViewModel.cs` primary file SHOULD stay under ~1000 lines; total VM+coordinator surface may still be large, but no single orchestration monologue.

### 5. Settings modules: binary decision, no middle ground

**Preferred path (if time allows in Slice B):** move path/status/dirty fields for tools and output defaults into real modules used by `SettingsToolViewModel`, with snapshot/save aggregation remaining on the tool VM.

**Acceptable alternate:** delete the three unused module types and document that appearance already has `SettingsAppearanceViewModel`; defer further modularization.

**Not acceptable:** leave unreferenced module classes as documentation-only “ownership.”

### 6. ExpressionEditor gets authoring service from outside

- `AppCompositionRoot` owns a single `IExpressionAuthoringService` wrapping its expression engine policy.
- Because production editors are created by XAML, expose the authoring service as an Avalonia property and bind it from both the main-window and expression-tool ViewModels. Pass the service through `ToolWindowCreateContext` for the expression-tool path.
- A design-time/test fallback may construct a service, but neither production XAML path may use that fallback.
- A Headless sentinel-service test MUST prove that both production editor hosts invoke the composition-provided service.
- Control keeps presentation (colorizer, completion chrome); analysis stays in Core authoring service.

### 7. Composition lifecycle (Slice E)

Document and implement:

| Service | Lifetime |
|---------|----------|
| `ChapterTimeFormatter` | single shared instance per root |
| `IChapterExpressionEngine` / authoring | single shared |
| `ChapterExportService` | single shared (or factory that reuses engine/formatter) |
| `ExternalToolLocator` | single shared per settings store |
| `IProcessRunner` | stateless; one instance fine |
| `IShellService`, `ISettingsStore` | required non-null on production `MainWindowViewModel` / settings tool |

Tests may still inject fakes; production constructors should not treat shell/settings as optional if the app always provides them. Composition tests MUST assert reference identity (or a documented equivalent policy factory) across the GUI importer, save, main-window, settings-window, and expression-editor paths.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Secure XML rejects previously “working” malformed DTD docs | Treat as intentional; cover with tests that malicious/DTD payloads fail closed; normal Matroska chapter XML has no DTD need |
| Binary max too low breaks rare playlists | Choose generous but finite limits; tests with edge sizes; diagnostics on failure |
| Coordinator extraction breaks subtle UI state | Preserve existing unit/Headless anti-stale and binding tests as merge blockers; no behavior changes in product scenarios |
| Deleting Settings modules looks like “regression” vs prior plan | Prior plan required modularization; dead modules fail that requirement—delete or wire both satisfy honesty |
| Parallel `dotnet test` file locks | Enforce sequential / full-solution gates per AGENTS.md |

## Migration Plan

1. **Slice A** merge independently (Core + tests).
2. **Slice B** Settings cleanup (small Avalonia PR).
3. **Slice C** largest PR: extract coordinators; keep public VM API stable for XAML/tests where possible.
4. **Slice D** command + expression wiring.
5. **Slice E** composition polish.
6. After all slices: `openspec validate "src-code-review-remediation" --strict`, then archive when implemented (sync deltas into main specs).

Rollback: each slice is revertible; security slices do not depend on shell slices.

## Open Questions

1. **Exact binary length caps** — finalize numbers during Slice A using known large MPLS fixtures in tests (start proposal: e.g. single `ReadExactBytes` hard ceiling of 64 MiB and tighter semantic caps per MPLS field).
2. **Port implementation location after coordinator extract** — VM implements ports by forwarding vs dedicated adapter type; choose in Slice C for minimal test churn.
3. **Archive order with `decompose-main-window-session`** — optional; not blocked by this change’s implementation if code already matches that change’s intent.

## Verification discipline

Per slice:

- Focused tests for touched projects.
- Slice merge gate: run `dotnet test ChapterTool.Avalonia.slnx` (or Core-only slices: full solution after Core tests) as one full-solution command/gate—never parallel multi-project external test hosts. The unit and Headless projects remain separate testhost processes.
- No source-text assertions on `.cs`/`.axaml` for behavior; use runtime/API tests.
- Update `docs/code-map/` when ownership moves.
