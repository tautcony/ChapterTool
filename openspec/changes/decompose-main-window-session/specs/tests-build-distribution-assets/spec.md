## ADDED Requirements

### Requirement: Workspace session decomposition is covered by unit tests
The Avalonia unit-test project SHALL cover chapter workspace/session transitions and export/projection snapshot behavior without requiring Avalonia controls.

#### Scenario: Clip combine and restore are unit-tested
- **WHEN** unit tests drive combine and restore on a multi-entry session
- **THEN** they SHALL assert typed session mode, retained original entries, current chapter set, and derived combine capability

#### Scenario: Load replacement clears previous combined mode
- **WHEN** unit tests load a combined session and then successfully load another source
- **THEN** they SHALL assert that previous combined backup/checked state does not leak into the new session

#### Scenario: Projection and export snapshots are unit-tested
- **WHEN** unit tests change expression, naming, order shift, format, or encoding preferences
- **THEN** they SHALL assert that row projection inputs and export preference snapshots remain coherent for preview and save

#### Scenario: Concurrent load and append anti-stale regressions remain mandatory
- **WHEN** Slice A or Slice B migrates load/append/session ownership
- **THEN** unit tests SHALL continue to cover overlapping loads and older-append-vs-newer-load races with deterministic controlled load services
- **AND** late progress from a superseded operation SHALL be covered so it cannot regress silently
- **AND** those scenarios SHALL be treated as merge blockers for the migrating slice, not deferred to final cleanup

### Requirement: Each mergeable slice has proportional verification gates
Every independently mergeable decomposition slice SHALL run focused tests for the changed surface plus a full-solution verification gate before merge. Maintainers SHALL NOT launch multiple external `dotnet test` commands in parallel. Avalonia Headless UI tests SHALL remain in the dedicated `ChapterTool.Avalonia.Headless.Tests` project so they keep a separate testhost from non-UI Avalonia unit tests. A single `dotnet test ChapterTool.Avalonia.slnx` invocation is an allowed full-solution gate and may include the Headless project; that is not the same as starting multiple concurrent `dotnet test` processes.

#### Scenario: Session-heavy slices run full solution tests before merge
- **WHEN** Slice A or Slice B is proposed for merge
- **THEN** verification SHALL include focused Avalonia unit tests for load/clip/edit/expression paths
- **AND** verification SHALL include `dotnet test ChapterTool.Avalonia.slnx` after any required restore/build
- **AND** if a focused Headless command is also run for that slice, it SHALL complete before the full-solution command starts
- **AND** multiple external `dotnet test` processes SHALL NOT be started in parallel

#### Scenario: UI-shell and tool-lifecycle slices include Headless and full solution gates
- **WHEN** Slice C, D, or E is proposed for merge
- **THEN** verification SHALL include the focused unit tests for that slice
- **AND** when UI shell, tool windows, settings, or expression editor presentation changed, verification SHALL include a focused Headless project run that finishes before the full-solution gate
- **AND** verification SHALL include `dotnet test ChapterTool.Avalonia.slnx` before merge
- **AND** Headless SHALL remain hosted by its dedicated test project/testhost rather than being merged into the non-UI Avalonia unit-test project

#### Scenario: Factory/CLI slice includes CLI coverage and full solution gate
- **WHEN** Slice F is proposed for merge or the overall change is completed
- **THEN** verification SHALL include CLI/composition tests for shared factories
- **AND** verification SHALL include `openspec validate "decompose-main-window-session" --strict` when OpenSpec artifacts changed
- **AND** verification SHALL include `dotnet test ChapterTool.Avalonia.slnx`
- **AND** any preceding focused `dotnet test` commands SHALL have completed before that full-solution command starts

### Requirement: Binding-authority and grid-edit routing regressions are covered
The Avalonia unit and Headless test projects SHALL cover binding-authority and stable grid-edit routing regressions introduced by the shell decomposition.

#### Scenario: Save and preview do not depend on control scrape helpers
- **WHEN** tests change bound export/projection options and invoke save or preview through the command surface
- **THEN** the resulting options SHALL reflect the bound ViewModel/workspace state without requiring a test-only imperative control scrape helper as the production path

#### Scenario: Localized grid headers still commit correctly under headless or unit coverage
- **WHEN** tests commit time, name, or frame edits while the active UI language is Simplified Chinese, English, or Japanese
- **THEN** the correct edit command path SHALL run
- **AND** coverage SHALL not rely on hard-coded bilingual header string matching as the production routing mechanism

### Requirement: Narrow tool ports are covered without full shell wiring
Secondary tool ViewModels introduced or refactored by the decomposition SHALL be unit-testable against narrow fakes/ports.

#### Scenario: Expression tool applies through an expression port fake
- **WHEN** unit tests construct the expression tool against an expression port fake
- **THEN** apply/browse/validate behavior SHALL be exercisable without constructing the full main-window ViewModel graph

#### Scenario: Settings live-apply uses a preference sink fake
- **WHEN** unit tests change a runtime-safe setting against a preference sink fake
- **THEN** the sink SHALL receive the expected preference update without requiring unrelated main-window command wiring
