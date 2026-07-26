## ADDED Requirements

### Requirement: Log window presents actionable structured events
The log window SHALL present each captured structured event with enough context for a user to identify the operation and investigate a failure.

#### Scenario: Log entry shows workflow context
- **WHEN** the log window displays an entry
- **THEN** it SHALL show the timestamp, severity, localized message, and category or operation context
- **AND** the displayed message SHALL use the active UI language

#### Scenario: Technical detail is available without replacing the summary
- **WHEN** an entry contains diagnostic code, path, process output, exception text, or other technical detail
- **THEN** the log window SHALL keep the concise localized summary visible
- **AND** it SHALL make the technical detail selectable or expandable for inspection and copying

#### Scenario: Severity filter changes the visible set
- **WHEN** the user selects an available log severity filter
- **THEN** the log window SHALL show only entries that match the selected severity rule
- **AND** clearing the filter SHALL restore all retained entries without losing captured history

### Requirement: Log window actions remain live and bounded
The log window SHALL provide clear and copy actions while continuing to receive new entries and respecting the configured bounded history.

#### Scenario: Copy action copies the selected log content
- **WHEN** the user invokes copy for a selected log entry or its technical detail
- **THEN** the window SHALL send the selected text to the injectable clipboard service
- **AND** the action SHALL not require a direct platform clipboard call from the ViewModel

#### Scenario: Clear action keeps logging active
- **WHEN** the user clears the log window
- **THEN** the visible and in-memory retained history SHALL be empty
- **AND** a later accepted log event SHALL appear in the window without reopening it

#### Scenario: New entries refresh the visible list
- **WHEN** a new accepted structured log event is captured while the log window is open
- **THEN** the window SHALL add the event using the active severity filter
- **AND** the window SHALL preserve the bounded retention policy

#### Scenario: Empty log state is explicit
- **WHEN** no retained event matches the current filter
- **THEN** the log window SHALL show a localized empty-state message
- **AND** it SHALL keep the clear, filter, and window actions usable
