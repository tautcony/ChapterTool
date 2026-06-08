## ADDED Requirements

### Requirement: Application composition root
The Avalonia application SHALL centralize construction of services, ViewModels, and windows in application startup composition.

#### Scenario: Main window is resolved from composition
- **WHEN** the application starts normally
- **THEN** `App` SHALL resolve `MainWindow` and its dependencies from the composition root rather than constructing the application object graph across `App`, `MainWindow`, and service constructors

#### Scenario: Services are substitutable in tests
- **WHEN** tests construct the main shell or ViewModels
- **THEN** dialog, clipboard, shell, settings, window, process, external tool, native dependency, load, save, frame-rate, editing, and importer services SHALL be replaceable through registered interfaces or factories

#### Scenario: Composition validates required registrations
- **WHEN** a composition smoke test resolves the main window, primary ViewModels, window service, and importer registry
- **THEN** missing required services SHALL be detected before user workflows are exercised manually

### Requirement: Secondary windows use dedicated views and ViewModels
Auxiliary UI tools SHALL be implemented as dedicated Avalonia views with ViewModels instead of large imperatively generated control trees.

#### Scenario: Window service displays views
- **WHEN** preview, log, color, language, expression, template, zones, or forward-shift tools are opened
- **THEN** the window service SHALL show the corresponding view and coordinate owner/result behavior without constructing that tool's internal controls inline

#### Scenario: Secondary window behavior is bindable
- **WHEN** a secondary tool displays state, accepts input, or invokes actions
- **THEN** the tool SHALL use ViewModel properties and commands that can be unit tested without generating its visual tree imperatively

#### Scenario: Converted windows keep lifecycle behavior
- **WHEN** a secondary window is opened, closed, and reopened
- **THEN** it SHALL preserve the documented reusable, modal, or result-returning behavior for that tool
