# document-session-lifecycle Specification

## Purpose
TBD - created by archiving change unified-editing-07-session-lifecycle. Update Purpose after archive.
## Requirements
### Requirement: Session replacement is built before the old session is discarded
Hosts SHALL build and validate a replacement document session before asking to discard the current session. Failed construction or cancellation SHALL preserve the old session.

#### Scenario: Import fails during replacement
- **WHEN** replacement import or session construction fails
- **THEN** the active document, history, draft, and export baseline SHALL remain available

#### Scenario: Current session changes during confirmation
- **WHEN** the old session is edited while replacement confirmation is open
- **THEN** the host SHALL recheck loss conditions before replacing it

### Requirement: Ending a session cancels work and releases history ownership
Closing or replacing a session SHALL invalidate its token, cancel its outstanding tasks, and release host references to history nodes, drafts, previews, subscriptions, and history display items. An in-flight read-only export MAY retain only its captured document snapshot.

#### Scenario: Old asynchronous result arrives after replacement
- **WHEN** a load, append, or export from the prior session completes after a new session opens
- **THEN** it SHALL NOT mutate the new session or its export baseline

#### Scenario: Session ends
- **WHEN** the desktop window closes or the user confirms replacing the active session
- **THEN** its history SHALL become unreachable from the host while a captured export may finish with content only

#### Scenario: Window close is canceled
- **WHEN** another close handler cancels the desktop window close
- **THEN** the active session and its lifetime token SHALL remain valid

### Requirement: Hosts explain session-only history before destructive replacement
Reload or replacement SHALL offer a continue/cancel choice when the current session has history, an active draft, or unexported content. Closing the desktop window SHALL end the session without a session-loss confirmation. Browser refresh or process termination SHALL NOT be represented as recoverable history.

#### Scenario: User cancels replacement
- **WHEN** the user cancels the loss confirmation
- **THEN** the current session, draft, history, and export state SHALL remain unchanged

#### Scenario: User reloads the application
- **WHEN** the user reopens a source after process restart or browser refresh
- **THEN** a new document session and history root SHALL be created

### Requirement: Export baselines describe only the captured track snapshot
Successful export SHALL record the captured track's canonical content digest, format options, and state identity. Failed or asynchronous stale exports SHALL NOT mark later content or another session as exported.

#### Scenario: Edit continues during export
- **WHEN** an export succeeds for a captured snapshot after a newer edit commits
- **THEN** only the captured snapshot SHALL be marked exported and the newer edit SHALL remain dirty

#### Scenario: One track is exported
- **WHEN** a document with multiple tracks exports one track
- **THEN** other tracks SHALL remain unexported unless separately included in a successful export
