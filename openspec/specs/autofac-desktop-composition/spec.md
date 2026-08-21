# autofac-desktop-composition Specification

## Purpose
TBD - created by archiving change introduce-autofac-desktop-composition. Update Purpose after archive.
## Requirements
### Requirement: Desktop composition uses Autofac modules
The desktop Avalonia application SHALL use an Autofac container as the composition boundary for desktop services, the main shell, and Native Window auxiliary tools. Registrations SHALL be grouped in explicit modules owned by the desktop composition project.

#### Scenario: Production container is built
- **WHEN** the desktop application starts
- **THEN** `AppCompositionRoot` SHALL build an Autofac container from the production modules
- **AND** the container SHALL include the required workspace, infrastructure, localization, settings, platform, tool catalog, auxiliary-host, and main-shell registrations

#### Scenario: Shared assemblies remain container independent
- **WHEN** Core, Contracts, browser, or shared Avalonia UI projects are built
- **THEN** they SHALL not require an Autofac reference or Autofac registration API

### Requirement: Required application objects use constructor injection
Production application services, workflows, ViewModels, tool descriptors, and host implementations SHALL receive required dependencies through constructors that Autofac can resolve.

#### Scenario: Main shell is resolved
- **WHEN** the container resolves `MainWindow`
- **THEN** it SHALL resolve `MainView`, `MainWindowViewModel`, and all required shell services through constructor or explicit host-factory registrations
- **AND** the shell SHALL not use property or field injection for required dependencies

#### Scenario: Tool descriptor is resolved
- **WHEN** the auxiliary-tool host creates a registered tool
- **THEN** the descriptor SHALL receive its required session, localization, settings, appearance, and platform ports through the typed creation boundary
- **AND** the descriptor SHALL not resolve unrelated services from a global service locator

### Requirement: Registration ownership and lifetimes are explicit
The desktop modules SHALL assign an explicit lifetime to each production registration and SHALL preserve one application instance for shared stateful services.

#### Scenario: Shared service identity is preserved
- **WHEN** the main shell, importer registry, save service, and auxiliary tools resolve localization, settings, expression, export, formatter, external-tool, and catalog services
- **THEN** each consumer SHALL receive the application-owned instance for that service
- **AND** the container SHALL not create a second default instance for the same application graph

#### Scenario: Tool content has an owned lifetime
- **WHEN** a tool is opened, refreshed, closed, or the host is disposed
- **THEN** its ViewModel SHALL be created and disposed by the auxiliary-tool host lifecycle
- **AND** the application container SHALL not retain a closed tool ViewModel through an accidental singleton registration

### Requirement: Window-bound services use host factories
The desktop container SHALL not register a global `Window`, `Control`, or `TopLevel` instance solely to satisfy a service constructor. Window-bound file picker, settings picker, clipboard, and close-confirmation services SHALL receive the active window through a host-owned factory or port.

#### Scenario: Native tool opens with an active window
- **WHEN** a Native Window auxiliary tool opens
- **THEN** the host SHALL create window-bound services for that tool using the active tool window
- **AND** the root container SHALL remain independent of a global active-window lookup

#### Scenario: Main view creates a file picker
- **WHEN** the main view attaches to a desktop window and a file action runs
- **THEN** the host-supplied file-picker factory SHALL create or return the picker for that window
- **AND** the shared UI assembly SHALL not construct a desktop picker by probing global state

### Requirement: Container validation occurs before user interaction
The desktop composition SHALL validate required registrations and resolve the primary shell graph before the main window becomes visible.

#### Scenario: Required registration is missing
- **WHEN** a production or test module omits a required shell service
- **THEN** container validation SHALL fail with a dependency-resolution error before the main window is assigned to the desktop lifetime

#### Scenario: Production graph resolves
- **WHEN** all required modules are registered
- **THEN** validation SHALL resolve `MainWindow`, `MainWindowViewModel`, `IToolCatalog`, `IAuxiliaryToolHost`, and the importer registry
- **AND** validation SHALL complete without opening a user-facing window or running an external process

### Requirement: Test compositions can replace registrations
The desktop composition SHALL expose a test composition path that adds or overrides registrations before container build.

#### Scenario: Test replaces workflow services
- **WHEN** a test module registers fake load, save, importer, shell, runtime capability, catalog, or auxiliary-tool services
- **THEN** the resolved shell SHALL use the test registrations
- **AND** the production registrations SHALL not be used for those overridden services

#### Scenario: Test verifies shared identity
- **WHEN** a test resolves the same shared service through two consumers
- **THEN** both consumers SHALL reference the same instance when the service is application-scoped

### Requirement: Container ownership is disposed
The desktop application SHALL own and dispose the Autofac lifetime scope that owns the application graph.

#### Scenario: Application exits
- **WHEN** the Avalonia desktop lifetime exits
- **THEN** `AppCompositionRoot` SHALL dispose the application lifetime scope
- **AND** disposable hosts, logging providers, localization adapters, and owned services SHALL be released exactly once

#### Scenario: Composition is disposed repeatedly
- **WHEN** the composition root is disposed more than once
- **THEN** disposal SHALL be idempotent
- **AND** the container SHALL not dispose an owned service more than once

