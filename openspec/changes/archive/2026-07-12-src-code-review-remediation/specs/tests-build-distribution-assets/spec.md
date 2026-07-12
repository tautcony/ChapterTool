## ADDED Requirements

### Requirement: Import parser safety is covered by automated tests
The Core and/or Infrastructure test projects SHALL cover secure XML loading and bounded binary playlist parsing without relying on source-text assertions over production `.cs` files.

#### Scenario: Hostile or DTD-dependent XML fails closed
- **WHEN** tests feed Matroska XML (and XPL where applicable) that depends on external entity resolution or prohibited DTD processing
- **THEN** import SHALL fail closed or refuse entity expansion
- **AND** a fixture of valid chapter XML without external entities SHALL still succeed

#### Scenario: Oversized binary length fails without huge allocation
- **WHEN** tests present a synthetic MPLS or binary helper input with an oversized length field
- **THEN** parsing SHALL fail with invalid-data behavior
- **AND** the test SHALL complete without requiring multi-gigabyte process memory

#### Scenario: Invalid MPLS container bounds fail before allocation or backwards seek
- **WHEN** tests present an oversized playlist/subpath count, a signed-overflow exact-read length, or a container with a declared length smaller than its consumed entries
- **THEN** parsing SHALL return invalid-data behavior before oversized collection allocation or a negative skip

### Requirement: Shell orchestration remediation is covered by automated tests
The Avalonia unit and Headless test projects SHALL preserve and extend coverage for coordinator extraction, settings module ownership (or deletion), command-surface adapter behavior, and expression-editor service injection without source-text assertions as the sole proof.

#### Scenario: Existing anti-stale and binding regressions remain green
- **WHEN** load/append orchestration moves into workflow coordinators
- **THEN** overlapping-load, older-append-vs-newer-load, and late-progress-ignored unit coverage SHALL remain green

#### Scenario: Settings ownership is verified by behavior
- **WHEN** settings modularization is completed by wiring or deletion
- **THEN** tests SHALL prove settings load/save/live-apply still works
- **AND** verification SHALL NOT consist only of reading source files for the presence of module class names
- **AND** when modules are retained, a module-owned preference edit SHALL be observed through load/save or live-apply behavior

#### Scenario: Expression authoring injection is behaviorally covered
- **WHEN** the main-window and expression-tool editors are created through their production composition paths with a sentinel injected authoring service
- **THEN** both editors SHALL invoke that service for analysis-driven completion or diagnostics
- **AND** they SHALL not use the control's design-time/test fallback

### Requirement: Per-slice verification gates for review remediation
Each mergeable slice of this change SHALL run focused tests for touched projects and then a full-solution test gate before merge, without starting multiple solution test projects as parallel external processes.

#### Scenario: Security slices gate Core tests then full solution
- **WHEN** XML/binary hardening or Lua sandbox tests change Core
- **THEN** the slice SHALL run focused Core tests and then `dotnet test ChapterTool.Avalonia.slnx` (or the documented full-solution command) as a single gate before merge

#### Scenario: Shell slices gate Avalonia unit then Headless then full solution
- **WHEN** ViewModel/coordinator or XAML shell behavior changes
- **THEN** the slice SHALL run Avalonia unit tests, then Headless tests sequentially if UI is affected, then the full-solution gate
- **AND** unit and Headless SHALL NOT be started as concurrent external `dotnet test` processes against the same solution outputs

#### Scenario: Composition identity is verified across production factories
- **WHEN** a composition test creates GUI importer, save, main-window, settings-window, and expression-editor paths from one root
- **THEN** the test SHALL verify the documented shared formatter, expression policy, and external-tool-locator identity or policy-factory contract without source-text inspection
