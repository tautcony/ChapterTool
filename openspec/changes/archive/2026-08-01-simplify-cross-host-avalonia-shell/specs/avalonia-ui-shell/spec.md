## ADDED Requirements

### Requirement: Main view receives embedded presentation explicitly
The shared `MainView` SHALL receive an explicit `IEmbeddedToolPresenter` when the host supports embedded tool presentation. It SHALL not discover that capability by casting the auxiliary-tool host.

#### Scenario: Native-window host supplies no embedded content
- **WHEN** the desktop Native Window host constructs `MainView`
- **THEN** it SHALL supply a no-content presenter
- **AND** `MainView` SHALL keep the `ToolContentHost` region hidden without inspecting the concrete auxiliary-tool host type

#### Scenario: Embedded host changes current tool content
- **WHEN** an Embedded host changes the active tool content
- **THEN** the presenter SHALL notify `MainView`
- **AND** `MainView` SHALL update content and visibility without changing the main ViewModel

### Requirement: Main shell commands use stable tool identifiers
Main-shell commands that open auxiliary tools SHALL use a stable typed tool identifier and the shared auxiliary-tool host contract.

#### Scenario: Tool command opens a registered tool
- **WHEN** the user invokes Preview, Settings, Language, Expression, Log, Template Names, Zones, or Forward Shift
- **THEN** the command SHALL pass the corresponding stable tool identifier
- **AND** it SHALL not pass the concrete `MainWindowViewModel` through an `object?` parameter

#### Scenario: Unknown tool identifier is safe
- **WHEN** a host receives an unregistered tool identifier
- **THEN** it SHALL return a safe no-op or localized placeholder result
- **AND** it SHALL not terminate the main shell

### Requirement: Secondary tools keep narrow workspace dependencies
Secondary tool ViewModels SHALL receive only the narrow workspace or host ports required by their behavior. They SHALL not depend on the concrete `MainWindowViewModel` type.

#### Scenario: Preview tool changes format
- **WHEN** the preview tool changes its export format
- **THEN** it SHALL use the export preference port
- **AND** it SHALL not require unrelated main-window commands

#### Scenario: Expression tool applies a script
- **WHEN** the expression tool loads or applies a script
- **THEN** it SHALL use the expression session port and host file-picker port
- **AND** it SHALL not require the concrete main-window ViewModel

#### Scenario: Tool changes refresh the main shell
- **WHEN** the expression tool applies a script
- **THEN** the session facade SHALL refresh the main-shell expression fields, row grid, and status through its notification port
- **AND** the tool SHALL not reach the concrete main-window ViewModel for notification

### Requirement: Main shell behavior remains host-neutral
Host selection SHALL change platform effects and tool presentation without changing workspace revision, clip session, projection, export, localization, or command-state behavior.

#### Scenario: Same load workflow runs in two hosts
- **WHEN** two Avalonia hosts invoke the same typed source load command with equivalent source documents
- **THEN** both shells SHALL apply the same workspace commit and projection rules
- **AND** only their host-specific source and surface effects may differ
