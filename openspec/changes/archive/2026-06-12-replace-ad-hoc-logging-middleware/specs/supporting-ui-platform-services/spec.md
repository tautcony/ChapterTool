## ADDED Requirements

### Requirement: Application diagnostics use standard logging middleware
The application SHALL route diagnostic logging through standard .NET logging abstractions backed by a structured logging provider instead of using a hand-written in-memory list as the primary logging mechanism.

#### Scenario: Services log through injectable logging abstractions
- **WHEN** Avalonia, Infrastructure, or platform services need to record diagnostic information
- **THEN** they SHALL use injectable logging abstractions or adapters backed by `Microsoft.Extensions.Logging`

#### Scenario: Logging backend is configured in composition
- **WHEN** the Avalonia application starts normally
- **THEN** the composition root SHALL configure the logging backend, providers, minimum levels, and lifecycle ownership in one place

#### Scenario: Core remains logging-backend independent
- **WHEN** Core services are built or tested
- **THEN** they SHALL NOT depend on Serilog, Avalonia, file-system log sinks, or another concrete logging backend

### Requirement: Application logs are severity-classified structured events
Application log entries SHALL include severity level, category, timestamp, structured message state, and optional technical detail so troubleshooting can distinguish routine workflow events from warnings and failures.

#### Scenario: Routine workflow events use information level
- **WHEN** a source is loaded, chapters are saved, a source option is selected, or frame information is updated successfully
- **THEN** the event SHALL be recorded at `Information` level with concise structured context

#### Scenario: Recoverable diagnostics use warning level
- **WHEN** an import, export, external dependency, or media lookup produces a recoverable diagnostic
- **THEN** the event SHALL be recorded at `Warning` level with the diagnostic code, message, location, and relevant technical detail

#### Scenario: Failures use error level
- **WHEN** an operation fails, an exception is observed, or an external process cannot be invoked successfully
- **THEN** the event SHALL be recorded at `Error` level with exception or failure detail sufficient for debugging

#### Scenario: Verbose implementation detail is filtered
- **WHEN** an event is only useful for developer-level tracing
- **THEN** it SHALL be recorded at `Debug` or `Trace` level and SHALL NOT appear in the default user-facing log window unless configured to do so

### Requirement: Log window is backed by a bounded logging sink
The log window SHALL display recent application log events captured from the standard logging pipeline while preserving clear, copy, localization, and refresh behavior.

#### Scenario: Log window receives captured events
- **WHEN** a log event passes the configured UI sink filter
- **THEN** the log service SHALL expose it to the log window with timestamp, severity, message key or rendered message, arguments, category, and technical detail

#### Scenario: Log window clear does not disable logging
- **WHEN** the user clears the log window
- **THEN** recent in-memory entries SHALL be removed while the logging backend continues recording subsequent events

#### Scenario: In-memory entries are bounded
- **WHEN** many log events are recorded during a session
- **THEN** the UI log sink SHALL retain only a bounded recent set to avoid unbounded memory growth

### Requirement: Diagnostic logs are persisted locally
The Avalonia application SHALL write structured diagnostic logs to a local rolling file sink suitable for troubleshooting after the application exits.

#### Scenario: File logs survive application exit
- **WHEN** the application records workflow events, warnings, or errors and then exits
- **THEN** those events SHALL be available in local log files under the application's user data or settings area

#### Scenario: File logs are bounded by retention
- **WHEN** log files roll across multiple runs
- **THEN** the logging backend SHALL enforce retention or size limits so logs do not grow without bound

#### Scenario: Sensitive transport is not introduced
- **WHEN** logs are recorded
- **THEN** the application SHALL NOT upload logs or send telemetry to remote services as part of this change
