## ADDED Requirements

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
