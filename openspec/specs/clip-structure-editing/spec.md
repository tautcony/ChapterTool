# clip-structure-editing Specification

## Purpose
TBD - created by archiving change unified-editing-05-clip-operations. Update Purpose after archive.
## Requirements
### Requirement: Merge replaces current tracks in one transaction
Merge SHALL use the current edited content, preserve chapter names, identities, explicit end times, and required source information, and replace selected tracks with one merged track. It SHALL retain boundaries and media placement without a second chapter backup.

#### Scenario: Merge after editing a source track
- **WHEN** the user edits a chapter and then merges its track
- **THEN** the merged track SHALL contain the edited value and the same chapter identity
- **AND** merge SHALL create one undoable transaction

#### Scenario: Merge tracks with different frame rates
- **WHEN** source tracks have different effective frame rates
- **THEN** merge SHALL apply an explicit compatible frame-rate policy or report a conflict
- **AND** it SHALL NOT silently interpret every time using the first track rate

### Requirement: Split uses current merged content and boundaries
Split SHALL distribute current chapters by their current start time and segment intervals. It SHALL preserve chapter identity and value, convert current start and explicit end values to the target segment timeline, and report an explicit-end conflict that crosses a boundary.

#### Scenario: Chapter starts at a boundary
- **WHEN** a chapter start equals a segment boundary
- **THEN** the chapter SHALL belong to the segment after the boundary, except a marker at total duration which belongs to the last segment

#### Scenario: Merged chapter was edited before split
- **WHEN** the user edits a merged chapter and splits by boundaries
- **THEN** split SHALL use the edited current chapter and SHALL NOT restore pre-merge chapter data

#### Scenario: End time crosses a segment boundary
- **WHEN** an explicit end time falls outside its chapter's assigned segment
- **THEN** split SHALL report the conflict and SHALL NOT silently truncate or duplicate the chapter

### Requirement: Append preserves edits and commits atomically
Append SHALL build a complete candidate from the captured current document, preserve all existing edits, allocate fresh identities for imported content, and commit once only if the captured session token remains current.

#### Scenario: Append completes after the session changes
- **WHEN** another edit or document replacement occurs while append is reading input
- **THEN** the append result SHALL be rejected as stale and SHALL NOT overwrite the newer document

#### Scenario: Append follows merge
- **WHEN** the user appends another source to a merged document
- **THEN** existing edits and boundaries SHALL remain intact and imported tracks SHALL be included in one undoable result

