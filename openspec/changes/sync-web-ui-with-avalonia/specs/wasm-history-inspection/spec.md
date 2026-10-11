## ADDED Requirements

### Requirement: Complete read-only history inspection
The browser history inspector MUST expose localized operation details, parameters, root chapter rows, and meaningful before/after field values. It MUST filter added, removed, and modified changes. Inspection MUST NOT move the history cursor or alter content. Detail requests MUST be canceled or ignored when selection or the document identity changes. Recoverable inspection failures MUST support retry.

#### Scenario: Inspect a node and filter its changes
- **WHEN** the user selects a retained node and filters its changed fields
- **THEN** the inspector shows the corresponding values and parameters
- **AND** the committed document and current history cursor remain unchanged

### Requirement: Alternate retained redo choices
The browser history tool MUST offer direct children of the current cursor as redo choices. Choosing a preferred branch MUST NOT restore it until the user invokes Redo. Explicit restore MUST remain separate from inspection selection.

#### Scenario: Review alternate futures
- **WHEN** two retained children follow the current cursor and the user selects one as the redo path
- **THEN** content stays unchanged until Redo
- **AND** Redo restores the selected child through the shared Core session

### Requirement: Responsive and keyboard-accessible history navigation
Wide history layouts MUST show tree and details together. Narrow layouts MUST provide tree and details navigation with reachable restore and close actions. The current node MUST be locatable. Tree and inspection controls MUST retain keyboard and modal focus behavior.

#### Scenario: Inspect and restore on a narrow viewport
- **WHEN** the user selects a history node at 390x640 and opens its details
- **THEN** values and the restore action are reachable
- **AND** Escape closes the dialog and restores focus to its opener
