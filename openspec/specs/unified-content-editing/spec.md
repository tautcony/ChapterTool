# unified-content-editing Specification

## Purpose
TBD - created by archiving change unified-editing-04-content-operations. Update Purpose after archive.
## Requirements
### Requirement: All content tools commit one complete candidate
Content tools SHALL capture a document snapshot and explicit target identities, compute a complete candidate, validate it, and apply it as one history transaction. A row failure, cancellation, or stale base SHALL reject the whole candidate.

#### Scenario: Batch candidate has one invalid row
- **WHEN** a template, expression, or other batch candidate contains an invalid result
- **THEN** no part of the candidate SHALL be committed
- **AND** the current state and history cursor SHALL remain unchanged

#### Scenario: Preview is cancelled or stale
- **WHEN** a preview is cancelled or its base token no longer matches the session
- **THEN** it SHALL NOT change the document
- **AND** a stale preview SHALL require recomputation before apply

### Requirement: Cell edits have explicit draft and commit semantics
Cell editing SHALL capture the chapter ID, field, original value, and base token. Enter, Tab, or valid focus loss SHALL commit one transaction; Escape SHALL discard the draft. Invalid input SHALL remain visible and SHALL NOT be coerced to zero.

#### Scenario: Same canonical value is entered
- **WHEN** a cell commits the value it already contains
- **THEN** no history node SHALL be created

#### Scenario: Undo is invoked during text editing
- **WHEN** a text control owns focus
- **THEN** its undo command SHALL affect only the draft
- **AND** document history SHALL remain unchanged

### Requirement: Tool operations are one-shot and scope is explicit
Template, expression, replacement, offset, frame-rate, numbering, ordering, and metadata tools SHALL show their target scope and compute from the captured original snapshot. Applying a tool SHALL store the result as current document content and SHALL NOT leave a transform rule active during export.

#### Scenario: Expression is applied then a cell is edited
- **WHEN** an expression is applied and a transformed time is edited in the table
- **THEN** the edited value SHALL become current content
- **AND** later serialization SHALL NOT run the expression again

#### Scenario: Operation targets selected chapters
- **WHEN** a tool uses selection scope
- **THEN** it SHALL capture the selected chapter IDs at calculation start and SHALL NOT substitute different rows after reordering

#### Scenario: Expression calculation fails for one target
- **WHEN** any target expression fails, times out, or is cancelled
- **THEN** the entire operation SHALL fail without partial content or history changes

### Requirement: Timing edits preserve document invariants
Time edits, shifts, scaling, delete normalization, and frame-rate actions SHALL update their defined related fields consistently, validate overflow and end-before-start, and reject invalid results before commit.

#### Scenario: Delete normalization shifts explicit ends
- **WHEN** chapter starts are normalized after deletion
- **THEN** each retained start and explicit end SHALL receive the same checked shift
- **AND** one undo SHALL restore the complete prior document

#### Scenario: Scale collapses a segment
- **WHEN** time scaling would make a segment zero length or overflow
- **THEN** the candidate SHALL be rejected and the current state SHALL remain unchanged

