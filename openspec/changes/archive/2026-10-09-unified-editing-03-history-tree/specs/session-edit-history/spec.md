## ADDED Requirements

### Requirement: Session history retains all committed branches
Each document session SHALL retain its root and every committed state in an in-memory history tree. The system SHALL NOT prune history by operation count, age, or memory threshold.

#### Scenario: Editing after undo creates a branch
- **WHEN** a new edit is committed while the cursor is not at a leaf
- **THEN** the previous child path SHALL remain reachable
- **AND** the new state SHALL be added as another child

#### Scenario: History contains many edits
- **WHEN** the session commits more edits than any conventional fixed history limit
- **THEN** all committed nodes SHALL remain reachable until the session ends

### Requirement: Undo, redo, and branch selection restore committed states
Undo and redo SHALL restore a previously committed state without rerunning the operation. Redo SHALL support the preferred child and an explicitly selected retained branch.

#### Scenario: Undo then redo
- **WHEN** the user undoes a committed edit and redoes it
- **THEN** the exact committed document and stable identities SHALL be restored
- **AND** mutation revision SHALL continue increasing

#### Scenario: Redo chooses an alternate branch
- **WHEN** multiple retained child branches exist and the user selects one
- **THEN** the cursor SHALL move to that branch's committed state without changing other branches

#### Scenario: Undo at root or redo at leaf
- **WHEN** undo has no parent or redo has no child
- **THEN** the operation SHALL return `NoChange` and SHALL NOT increase the revision

### Requirement: History failure preserves the current session
If change-set construction, branch navigation, or a recoverable resource allocation fails before publication, the session SHALL retain its document, history nodes, preferred branch, and cursor.

#### Scenario: History node allocation fails
- **WHEN** a commit cannot allocate its change set or indexes
- **THEN** the operation SHALL return a resource failure and preserve the original session state

#### Scenario: Cross-branch reconstruction fails
- **WHEN** reconstruction fails before the target is ready to publish
- **THEN** no intermediate document SHALL be visible and the cursor SHALL remain unchanged
