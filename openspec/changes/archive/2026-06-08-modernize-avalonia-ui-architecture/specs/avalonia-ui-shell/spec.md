## ADDED Requirements

### Requirement: Observable main-window ViewModel
The Avalonia main-window ViewModel SHALL expose UI-facing scalar state and command availability as observable properties.

#### Scenario: Scalar state changes notify bindings
- **WHEN** status text, progress, selected clip index, frame rounding, frame-rate selection, save format, option flags, visibility flags, or capability flags change
- **THEN** the ViewModel SHALL raise property change notifications for the changed properties

#### Scenario: Command availability follows state
- **WHEN** load state, selected rows, selected clip, save options, or platform capability state changes
- **THEN** affected commands SHALL raise command availability changes without requiring the window to call a manual refresh method

### Requirement: Main-window state is bound from XAML
The Avalonia main window SHALL bind visible state to ViewModel properties instead of synchronizing normal UI state through imperative control assignments.

#### Scenario: Bound controls update from ViewModel
- **WHEN** ViewModel state changes after loading, editing, clip switching, option changes, or save operations
- **THEN** bound text, progress, item sources, selections, checkboxes, visibility, enabled state, and option fields SHALL update through XAML bindings

#### Scenario: Code-behind remains view-specific
- **WHEN** `MainWindow.axaml.cs` is inspected
- **THEN** it SHALL only contain view-specific event adaptation, platform UI interactions, and constructor wiring, and SHALL NOT be responsible for routine status, progress, grid, clip, option, or command state synchronization

### Requirement: Typed compiled bindings
The Avalonia shell SHALL use typed data contexts and compiled bindings for the main window and migrated typed views.

#### Scenario: Main window bindings are checked at build time
- **WHEN** the Avalonia app project is built
- **THEN** main-window bindings to ViewModel properties and commands SHALL be validated by typed or compiled Avalonia binding support

#### Scenario: Binding regressions fail tests or build
- **WHEN** a bound ViewModel property or command is renamed or removed
- **THEN** the build or focused UI/static tests SHALL fail before runtime manual testing is required

### Requirement: Hidden command shims are removed
The UI shell SHALL NOT use hidden buttons or invisible controls solely as command hosts for main-window actions.

#### Scenario: Main actions are reachable through real command surfaces
- **WHEN** save, append MPLS, combine, open media, color, expression, template, zones, forward shift, and similar actions are available
- **THEN** they SHALL be exposed through visible buttons, menu items, context menu items, key bindings, or directly testable ViewModel commands

#### Scenario: Hidden shim controls are absent
- **WHEN** the main-window XAML is inspected
- **THEN** controls whose only purpose is to hide a command binding from the visible UI SHALL NOT be present

### Requirement: Async commands are observed
The UI shell SHALL execute asynchronous commands through an abstraction or event pattern that awaits work, observes exceptions, and exposes execution state when needed.

#### Scenario: Async command failures are handled
- **WHEN** a load, save, edit, clip, combine, or auxiliary command fails asynchronously
- **THEN** the exception SHALL be observed and routed to the ViewModel or dialog/status error path instead of being lost as fire-and-forget work

#### Scenario: Event handlers await command work
- **WHEN** grid edits, shortcut handlers, clip selection, insert/delete operations, or combine actions trigger asynchronous command behavior
- **THEN** the event adaptation code SHALL await the command task or call an async command API that tracks completion
