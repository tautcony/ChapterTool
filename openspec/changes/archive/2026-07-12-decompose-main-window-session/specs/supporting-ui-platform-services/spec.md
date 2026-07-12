## ADDED Requirements

### Requirement: Secondary tool windows are registered by descriptor
The window service SHALL open secondary tool windows through an explicit registration/descriptor model rather than an open-coded string switch that independently encodes title, content factory, and placeholder text for each tool id.

#### Scenario: Known tool opens from a registry entry
- **WHEN** the shell requests a registered tool window such as preview, log, settings, language, expression, template names, zones, or forward shift
- **THEN** the window service SHALL resolve title and content factory from the tool registration
- **AND** a later addition of another tool SHALL require adding a registration entry rather than expanding multiple independent switch statements as the only extension path

#### Scenario: Unknown tool remains safe
- **WHEN** the shell requests an unregistered tool id
- **THEN** the window service SHALL show a localized placeholder or no-op failure path
- **AND** it SHALL NOT throw an unhandled exception that closes the main window

### Requirement: Composition root owns shared service instances
The application composition root SHALL own the shared localization manager, expression engine, export service construction, settings store, and load/import registry factories used by the GUI shell. Production construction paths SHALL NOT silently create replacement default instances of those shared services when a real dependency was expected.

#### Scenario: Window service uses the composition localizer
- **WHEN** secondary tool windows are created during normal GUI startup
- **THEN** they SHALL use the same localization manager instance owned by composition
- **AND** they SHALL NOT create a private default localizer that can diverge from the main window culture

#### Scenario: Preview and save share export construction
- **WHEN** the GUI constructs save and preview export behavior
- **THEN** both SHALL obtain export/projection services from composition-owned construction
- **AND** preview SHALL NOT silently fall back to a separately constructed default export service with different dependencies

### Requirement: CLI and GUI share load and export factories
CLI conversion workflows and the GUI shell SHALL obtain importer-registry and export-service construction through shared composition factories or equivalent shared factory methods, instead of maintaining fully divergent private wiring that can drift.

#### Scenario: CLI importer registry matches GUI construction rules
- **WHEN** CLI inspect/convert and GUI load construct importer registries
- **THEN** both SHALL use the same factory rules for formatter, tool locator, process runner, and media readers
- **AND** adding a new importer registration SHALL not require two independent private wiring lists as the only path

#### Scenario: CLI export construction is composition-aligned
- **WHEN** CLI convert constructs an exporter
- **THEN** it SHALL use the shared export factory path or an explicitly injected exporter provided by tests
- **AND** it SHALL NOT rely on a one-off default constructor path that can diverge from GUI export dependency choices

## MODIFIED Requirements

### Requirement: Application composition root
The Avalonia application SHALL centralize construction of services, ViewModels, and windows in application startup composition, including shared settings, localization, expression, export, and import factories used by GUI and CLI consumers.

#### Scenario: Main window is resolved from composition
- **WHEN** the application starts normally
- **THEN** `App` SHALL resolve `MainWindow` and its dependencies from the composition root rather than constructing the application object graph across `App`, `MainWindow`, and service constructors

#### Scenario: Services are substitutable in tests
- **WHEN** tests construct the main shell or ViewModels
- **THEN** dialog, clipboard, shell, settings, window, process, external tool, native dependency, load, save, frame-rate, editing, and importer services SHALL be replaceable through registered interfaces or factories

#### Scenario: Composition validates required registrations
- **WHEN** a composition smoke test resolves the main window, primary ViewModels, window service, and importer registry
- **THEN** missing required services SHALL be detected before user workflows are exercised manually

#### Scenario: Shared factories serve CLI and GUI
- **WHEN** CLI commands need importer registry or export construction outside the desktop main window
- **THEN** they SHALL use composition-root factory methods or an equivalent shared factory surface
- **AND** they SHALL NOT permanently maintain a second complete private service graph that silently drifts from GUI wiring

### Requirement: Secondary windows use dedicated views and ViewModels
Auxiliary UI tools SHALL be implemented as dedicated Avalonia views with ViewModels instead of large imperatively generated control trees, and tool construction SHALL use registered factories plus narrow session ports rather than requiring the full main-window ViewModel for every tool capability.

#### Scenario: Window service displays views
- **WHEN** preview, log, settings, language, expression, template, zones, or forward-shift tools are opened
- **THEN** the window service SHALL show the corresponding view and coordinate owner/result behavior without constructing that tool's internal controls inline
- **AND** title/content resolution SHALL come from the tool registration model

#### Scenario: Secondary window behavior is bindable
- **WHEN** a secondary tool displays state, accepts input, or invokes actions
- **THEN** the tool SHALL use ViewModel properties and commands that can be unit tested without generating its visual tree imperatively

#### Scenario: Converted windows keep lifecycle behavior
- **WHEN** a secondary window is opened, closed, and reopened
- **THEN** it SHALL preserve the documented reusable, modal, or result-returning behavior for that tool

#### Scenario: Secondary window view models are released
- **WHEN** a secondary window closes or the window service replaces its content
- **THEN** any disposable DataContext SHALL be disposed
- **AND** tool ViewModels that subscribe to localization or other long-lived services SHALL unsubscribe during disposal

#### Scenario: Tools depend on narrow session ports
- **WHEN** expression, language, template, forward-shift, settings live-apply, or preview-format tools are constructed
- **THEN** each SHALL depend on a narrow workspace/session or preference port for the capability it needs
- **AND** it SHALL NOT require the full main-window command surface solely to mutate one related preference or expression field
