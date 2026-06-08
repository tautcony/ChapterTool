# supporting-ui-platform-services Specification

## Purpose
TBD - created by archiving change rewrite-avalonia-dotnet10. Update Purpose after archive.
## Requirements
### Requirement: Auxiliary Avalonia windows
The system SHALL provide Avalonia preview, log, color settings, about, and updater windows without WinForms dependencies.

#### Scenario: Preview window is reusable
- **WHEN** preview is opened and then closed
- **THEN** it SHALL hide or be reusable on next open and SHALL display current TXT/OGM preview text

#### Scenario: Log window uses log service
- **WHEN** log entries change
- **THEN** the log window SHALL display current log content and copy selected text through clipboard service

#### Scenario: Color settings preserve six slots
- **WHEN** color settings are loaded or saved
- **THEN** the six legacy color slots SHALL retain their documented order

### Requirement: Settings and legacy migration
The system SHALL use typed cross-platform settings while reading compatible legacy configuration.

#### Scenario: Legacy chaptertool settings migrate
- **WHEN** legacy `chaptertool.json` contains saving path, language, location, mkvToolnixPath, or eac3toPath keys
- **THEN** settings SHALL load them and normalize future writes to the new settings model

#### Scenario: Legacy color config migrates
- **WHEN** legacy `color-config.json` exists in compatible locations
- **THEN** theme settings SHALL read valid six-slot color values and ignore invalid values safely

### Requirement: Platform service abstractions
The application SHALL access dialogs, clipboard, shell, process execution, settings, localization, windows, privileges, file association, and native dependencies through injectable services.

#### Scenario: Dialog and clipboard are testable
- **WHEN** a ViewModel needs confirmation, input, error display, or clipboard access
- **THEN** it SHALL call service interfaces rather than WinForms APIs

#### Scenario: Process runner captures failures
- **WHEN** an external process is executed
- **THEN** the process result SHALL include stdout, stderr, exit code, timeout, cancellation, command, and working directory

### Requirement: External dependency location
The system SHALL centralize discovery and configuration for mkvextract, eac3to, and MP4 native dependencies.

#### Scenario: Configured path wins
- **WHEN** a dependency path exists in migrated settings
- **THEN** tool locator SHALL use it before registry or installation discovery

#### Scenario: Missing tool is structured
- **WHEN** a tool cannot be found
- **THEN** callers SHALL receive a missing-dependency result rather than a UI prompt from Core

### Requirement: Windows-only isolation
Windows-specific services SHALL be hidden, degraded, or reported unsupported on non-Windows platforms.

#### Scenario: File association is platform-gated
- **WHEN** `.mpls` file association is requested off Windows
- **THEN** the system SHALL hide the action or return unsupported-platform status

#### Scenario: Elevation is explicit
- **WHEN** an operation requires administrator rights
- **THEN** the app SHALL remain asInvoker by default and request elevation only through privilege service

#### Scenario: Registry is not a default settings dependency
- **WHEN** settings such as language, save directory, color slots, and external tool paths are loaded or saved
- **THEN** the application SHALL use typed cross-platform files by default and SHALL only use registry-specific integration behind explicitly Windows-gated services

### Requirement: Localization and resources
The Avalonia application SHALL support default Chinese and `en-US` resources without WinForms `ApplyResources`.

#### Scenario: Language loads from settings
- **WHEN** settings contain blank language or `en-US`
- **THEN** localization SHALL apply the matching resource set and persist changes through settings

#### Scenario: Assets are packaged
- **WHEN** the app is built or published
- **THEN** icons and required images SHALL be available through Avalonia-compatible resource packaging

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
