## MODIFIED Requirements

### Requirement: Hosts explain session-only history before destructive replacement
Reload or replacement MUST offer a continue/cancel choice when the current session has history, an active draft, or unexported content. Desktop close MUST request a decision when content has not been exported or a candidate is pending. History alone MUST NOT require desktop close confirmation. Browser refresh or process termination MUST NOT be represented as recoverable history.

#### Scenario: User cancels replacement
- **WHEN** the user cancels the loss confirmation
- **THEN** the current session, draft, history, and export state MUST remain unchanged

#### Scenario: User reloads the application
- **WHEN** the user reopens a source after process restart or browser refresh
- **THEN** a new document session and history root MUST be created

#### Scenario: User cancels desktop close
- **WHEN** unexported content or a pending candidate exists and the user cancels close
- **THEN** the window, document, history, candidate, and session lifetime MUST remain available

#### Scenario: User exports before close
- **WHEN** the user selects export and close
- **THEN** the host MUST export committed content through the existing export workflow
- **AND** it MUST explain that pending candidates are excluded
- **AND** it MUST keep the window open if export fails, is canceled, or leaves unexported content in any track

#### Scenario: User discards work before close
- **WHEN** the user explicitly selects discard and close
- **THEN** the window MUST close and end its session

#### Scenario: History remains after successful export
- **WHEN** all content is exported, no candidate is pending, and the user closes the window
- **THEN** history alone MUST NOT trigger a confirmation

#### Scenario: Repeated close while a decision is pending
- **WHEN** the user requests close again while a close decision or export is running
- **THEN** the host MUST keep one decision workflow active and MUST NOT discard the session early
