# atomic-edit-transactions Specification

## Purpose
TBD - created by archiving change unified-editing-02-atomic-transactions. Update Purpose after archive.
## Requirements
### Requirement: Document mutations commit atomically
A document session SHALL compute and validate a complete candidate before publishing it. A failed, cancelled, invalid, or unchanged operation SHALL leave the document and session revision unchanged.

#### Scenario: Candidate contains one invalid result
- **WHEN** a batch operation produces any invalid row
- **THEN** the session SHALL reject the entire candidate
- **AND** it SHALL preserve the original document

#### Scenario: Candidate is unchanged
- **WHEN** a command produces the same canonical document value
- **THEN** it SHALL return `NoChange` without increasing the mutation revision

### Requirement: Session commits reject stale work and serialize commands
Each candidate SHALL capture its document identity, state identity, and mutation revision. The session SHALL serialize mutations and reject a candidate whose captured token is no longer current.

#### Scenario: An edit occurs while a candidate is computing
- **WHEN** another mutation commits before the candidate is applied
- **THEN** the stale candidate SHALL be rejected without changing the current document

#### Scenario: Cancellation before publication
- **WHEN** an operation is cancelled before atomic publication
- **THEN** no candidate state or history-visible metadata SHALL be published

### Requirement: Retried transactions are idempotent
The session SHALL return the original result for a retry with the same transaction ID and request. It SHALL reject reuse of that ID with a different request.

#### Scenario: Commit notification is retried
- **WHEN** the same committed transaction is submitted again
- **THEN** the original result SHALL be returned and the document SHALL not change again

#### Scenario: Transaction ID is reused with different input
- **WHEN** a transaction ID is submitted with a different operation payload
- **THEN** the session SHALL reject it and preserve the document

