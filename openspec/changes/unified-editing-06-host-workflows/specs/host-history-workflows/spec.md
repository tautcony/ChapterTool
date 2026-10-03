## ADDED Requirements

### Requirement: Interactive hosts share the Core document session
Avalonia and browser workspaces SHALL read and mutate one Core document session per open document. Views in one session SHALL use its command queue and observe the same snapshot.

#### Scenario: Two views mutate one session
- **WHEN** two views issue content commands for the same document
- **THEN** both commands SHALL serialize through one Core session
- **AND** both views SHALL refresh from the committed session snapshot

#### Scenario: Separate browser tabs open the same file
- **WHEN** two browser tabs import the same source independently
- **THEN** each tab SHALL have a separate document identity and history

### Requirement: Hosts expose undo, redo, and history branches
Each interactive host SHALL provide undo and redo commands, the next operation description, the preferred redo path, and a branch choice for alternate retained futures. The history tree display SHALL create only visible and expanded entries.

#### Scenario: User selects a retained alternate branch
- **WHEN** the user opens redo branch choices and selects a retained branch
- **THEN** the session SHALL restore that branch and each host SHALL display its current document

#### Scenario: Deep history is displayed
- **WHEN** a long history is opened in the history panel
- **THEN** the panel SHALL virtualize visible entries without dropping Core history nodes

### Requirement: Editing shortcuts respect text drafts and platform defaults
Windows and Linux SHALL provide Ctrl+Z undo and Ctrl+Y/Ctrl+Shift+Z redo. macOS SHALL provide Cmd+Z and Cmd+Shift+Z redo. Shortcut routing SHALL use the shortcut catalog and SHALL not intercept text-control draft editing.

#### Scenario: Text editor owns focus
- **WHEN** a text control is editing a draft and the user presses its undo shortcut
- **THEN** only the control draft SHALL change
- **AND** the document history cursor SHALL remain unchanged

#### Scenario: Undo button is used with an active draft
- **WHEN** a document undo button is invoked while a draft exists
- **THEN** the host SHALL discard the draft first and then undo one committed document transaction

### Requirement: Host status distinguishes candidates, application, and session lifetime
Hosts SHALL distinguish candidate preview, applied in-memory edits, exported output, and failed commits. The history panel SHALL state that history lasts only for the current document session.

#### Scenario: User cancels a preview
- **WHEN** the user cancels a candidate preview
- **THEN** the document SHALL remain unchanged and status SHALL NOT say the edit was applied
