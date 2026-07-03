## ADDED Requirements

### Requirement: Application log service is evented and bounded
The application log service SHALL expose recent log history and notify subscribers when new user-visible log entries are captured, while retaining only a bounded in-memory set.

#### Scenario: Log window receives existing history then new entries
- **WHEN** a log window or log view model is opened after log entries already exist
- **THEN** it SHALL be able to read the existing bounded log history
- **AND** when a subsequent log event passes the UI sink filter it SHALL receive a notification containing the new structured log entry

#### Scenario: Log buffer remains bounded while events continue
- **WHEN** more log entries are captured than the configured UI log capacity
- **THEN** the log service SHALL retain only the most recent bounded entries
- **AND** it SHALL continue notifying subscribers for newly accepted entries

#### Scenario: Clearing history does not disable live logging
- **WHEN** the user clears the log window history
- **THEN** the in-memory log history SHALL be empty
- **AND** subsequent captured log entries SHALL still be retained and delivered to subscribers

### Requirement: UI notifications use explicit services
The application SHALL route user-facing prompts, tool windows, clipboard operations, shell operations, and platform integrations through explicit injectable services rather than static notification events or Core-to-UI callbacks.

#### Scenario: ViewModels request UI interactions through services
- **WHEN** a ViewModel needs to show a prompt, open an auxiliary window, copy text, open related media, select files, or request a platform-gated action
- **THEN** it SHALL use the corresponding service abstraction or command parameter flow
- **AND** it SHALL NOT require a static notification event or a direct Avalonia `Window` reference

#### Scenario: Core remains UI notification independent
- **WHEN** Core services import, edit, transform, convert, or export chapters
- **THEN** they SHALL return structured results or diagnostics to callers
- **AND** they SHALL NOT display UI prompts or publish static UI notification events
