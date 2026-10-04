## ADDED Requirements

### Requirement: Expression draft previews remain read-only until applied
Expression drafts SHALL produce candidate previews from one current session snapshot. Preparing or refreshing a preview SHALL NOT change the committed document, table rows, export output, or history. Applying a valid current candidate SHALL create one atomic history transaction.

#### Scenario: Expression draft refreshes its candidate
- **WHEN** the user changes expression text or selects a preset
- **THEN** the workspace SHALL compute a candidate from the current committed document snapshot
- **AND** the preview SHALL report before-and-after values and validation errors
- **AND** the committed document, rows, exports, and history SHALL remain unchanged

#### Scenario: Invalid expression candidate is shown without partial changes
- **WHEN** expression evaluation or document validation fails
- **THEN** the workspace SHALL expose the current diagnostic in the preview
- **AND** it SHALL NOT change the committed document or history
- **AND** it SHALL NOT allow the candidate to be applied

#### Scenario: Current valid expression candidate is applied once
- **WHEN** the user applies a valid candidate whose base snapshot is still current
- **THEN** the workspace SHALL commit the candidate as one transaction
- **AND** rows, format preview, and export SHALL use the committed candidate values
- **AND** undo SHALL restore the values before the expression operation without rerunning Lua

#### Scenario: Stale expression candidate is not committed
- **WHEN** the document changes after the expression preview was prepared
- **THEN** applying that preview SHALL NOT overwrite the newer document
- **AND** the host SHALL prepare a new candidate from the latest snapshot

#### Scenario: Expression preview is canceled
- **WHEN** the user cancels an expression preview
- **THEN** the preview SHALL be discarded
- **AND** the committed document, rows, export output, and history SHALL remain unchanged
