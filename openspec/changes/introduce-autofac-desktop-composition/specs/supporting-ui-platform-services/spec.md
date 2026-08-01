## MODIFIED Requirements

### Requirement: Application composition root
The Avalonia application SHALL centralize construction and resolution of services, ViewModels, and windows in application startup composition, including shared settings, localization, expression, export, and import factories used by GUI and CLI consumers. The desktop Avalonia composition SHALL use Autofac modules and constructor injection as its production resolution mechanism.

#### Scenario: Main window is resolved from composition
- **WHEN** the application starts normally
- **THEN** `App` SHALL resolve `MainWindow` and its dependencies from the Autofac composition root rather than constructing the application object graph across `App`, `MainWindow`, and service constructors

#### Scenario: Services are substitutable in tests
- **WHEN** tests construct the main shell or ViewModels
- **THEN** dialog, clipboard, shell, settings, window, process, external tool, native dependency, load, save, frame-rate, editing, and importer services SHALL be replaceable through Autofac registrations, test modules, or host factories

#### Scenario: Composition validates required registrations
- **WHEN** a composition smoke test resolves the main window, primary ViewModels, window service, and importer registry
- **THEN** missing required services SHALL be detected by Autofac before user workflows are exercised manually

#### Scenario: Shared factories serve CLI and GUI
- **WHEN** CLI commands need importer registry or export construction outside the desktop main window
- **THEN** they SHALL use composition-root factory methods or an equivalent shared factory surface
- **AND** they SHALL NOT permanently maintain a second complete private service graph that silently drifts from GUI wiring

#### Scenario: Desktop startup owns the container lifetime
- **WHEN** the desktop application exits
- **THEN** the composition root SHALL dispose the Autofac lifetime scope and its owned services
- **AND** repeated disposal SHALL remain safe
