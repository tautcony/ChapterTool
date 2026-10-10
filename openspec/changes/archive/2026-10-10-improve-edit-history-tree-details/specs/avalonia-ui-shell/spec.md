## MODIFIED Requirements

### Requirement: History opens in a button-invoked dialog

The Avalonia history button MUST open a separate modal dialog for the current document session. The main window MUST NOT reserve permanent space for a history list or sidebar. The dialog MUST use the current session's history and commands. Opening, closing, inspecting, or resizing the dialog MUST NOT create history transactions. Selecting a history node MUST inspect it. Restoration MUST require an explicit history action.

#### Scenario: Load without opening history
- **WHEN** a user loads a chapter document without opening history
- **THEN** the main window does not show a history list or reserved history sidebar
- **AND** the chapter workspace uses its full allocated central width
- **AND** the history button has a localized accessible name

#### Scenario: Open and dismiss history
- **WHEN** a user opens history
- **THEN** one modal history dialog owned by the main window opens
- **AND** it shows the current node, inspection selection, undo, redo, branch choices, and session-lifetime notice
- **AND** background clicks, shortcuts, and drag/drop cannot modify the document through the dialog
- **WHEN** the user closes the dialog through Close, window close, or Escape
- **THEN** focus returns to the history button
- **AND** closing does not roll back completed history navigation
- **AND** the main-window chapter region does not change its allocated size

#### Scenario: Inspect a node in the dialog
- **WHEN** the user selects, double-clicks, or presses Enter on a retained node
- **THEN** the dialog displays its details without restoring document content
- **AND** Enter moves focus to the detail region
- **AND** document revision, current history node, preferred redo choices, and pending content preview remain unchanged

#### Scenario: Navigate history in the dialog
- **WHEN** the user explicitly invokes Restore to This Node, Undo, Redo, or an alternate redo choice
- **THEN** the action uses the current session's existing history command behavior
- **AND** successful navigation refreshes the main-window chapter data and the dialog's current marker, inspection selection, action availability, and descriptions
- **AND** reopening the dialog shows the same current history state
- **AND** navigation does not add a history node or discard retained branches

#### Scenario: Reopen history and release its resources
- **WHEN** the user repeatedly opens and closes history
- **THEN** at most one dialog exists for that session at a time
- **AND** each closed dialog releases its content, subscriptions, and detail requests
- **AND** reopening selects the current node
- **AND** the history button is disabled without an available document session

#### Scenario: Inspect a long branching history
- **WHEN** history has many entries, several branches, and long operation titles
- **THEN** users can reach retained nodes through native tree scrolling, expansion, and keyboard navigation
- **AND** current position and branch relationships do not depend only on color or spaces
- **AND** undo, redo, restore, and close actions remain reachable

## ADDED Requirements

### Requirement: History presents connected branch topology

The dialog SHALL render the retained hierarchy in native Avalonia `TreeView` controls. Each node SHALL expose its actual children to the tree control. The tree SHALL provide file-tree expansion, indentation, selection, and keyboard behavior. The dialog SHALL NOT render a separate connector rail or flattened visual lane projection. Current position SHALL have a marker distinct from selected-row highlighting. Preferred redo SHALL be identified in the explicit redo choices.

#### Scenario: Editing after undo creates a visible fork
- **WHEN** a new edit is committed after undo while an older child path remains retained
- **THEN** history shows both child paths connected to their actual common parent
- **AND** each path remains reachable for inspection and explicit restoration

#### Scenario: Selection differs from the current node
- **WHEN** a user inspects an earlier or alternate node
- **THEN** the selected row has a selection indicator
- **AND** the current node retains a separate Current marker
- **AND** viewing the node does not change the preferred redo edge

#### Scenario: Locate a hidden current node
- **WHEN** the current node lies inside a collapsed fork and the user invokes Locate Current
- **THEN** the dialog expands its ancestor path and brings the current row into view
- **AND** locating the current node selects it for inspection without restoring content

#### Scenario: Scroll and expand a large tree
- **WHEN** the user scrolls, collapses, or expands branches in a large history
- **THEN** the TreeView retains the correct parent and child relationships
- **AND** collapsed descendants are not realized as visible controls
- **AND** Core history nodes are not removed

#### Scenario: Collapse the inspected descendant
- **WHEN** collapsing a fork hides the selected inspection node
- **THEN** the native tree retains its inspection selection and details
- **AND** expanding the fork makes the inspected row visible again
- **AND** the document cursor remains unchanged

### Requirement: Node details explain the selected committed change

The inspector SHALL show the selected node title, its available timestamp, nonzero effect counts, meaningful captured parameters, and typed before/after field values. It SHALL hide empty text, headings, and inapplicable columns. It SHALL show change filters only when the node has changes. It SHALL keep that filter reachable when its current choice has no matches. It SHALL NOT show explanatory comparison paragraphs or repeat a changed value above its field row. Chapter changes SHALL use historical track and chapter-position context. The initial node SHALL show the source and ordered read-only chapter values. Operation titles and field labels SHALL follow the active locale.

#### Scenario: Inspector has no applicable optional content
- **WHEN** a node has no meaningful parameters or time deltas
- **THEN** the inspector omits the parameter section and Delta column
- **AND** empty descriptions and status text do not reserve blank lines

#### Scenario: A filter has no matches
- **WHEN** the selected change filter has no matching fields
- **THEN** the inspector hides the empty field table and keeps the filter available
- **AND** the user can return to All changes

#### Scenario: Inspect a name edit
- **WHEN** a node changes one chapter name
- **THEN** the inspector identifies the historical track and chapter position and shows the exact old and new name once
- **AND** the summary reports one modified chapter

#### Scenario: Inspect a batch or property-only edit
- **WHEN** a node changes multiple chapters or only document, track, or segment properties
- **THEN** all actual changes can be reached through virtualized, scrollable details
- **AND** All, Added, Removed, and Modified filters preserve the full unfiltered summary counts
- **AND** property-only changes remain visible with zero changed chapters

#### Scenario: Inspect the initial node
- **WHEN** the root node is selected
- **THEN** the inspector labels it Initial State and shows its initial source, track overview, and ordered read-only chapter values
- **AND** it does not display a fabricated before/after edit

#### Scenario: Read precise and long values
- **WHEN** a changed time is below ordinary precision or text is too long for its initial cell
- **THEN** expanded detail exposes the exact time and signed delta or full text
- **AND** displayed text can be selected and copied
- **AND** unknown values, empty text, and absent objects have distinct localized labels

#### Scenario: Switch locale with existing history
- **WHEN** the locale changes between supported English, Chinese, and Japanese resources
- **THEN** titles, summaries, field labels, parameters, status, and accessible names update without new transactions
- **AND** operation kind is not inferred from an English description
- **AND** missing structured metadata uses a localized generic title with the original description available as secondary detail

### Requirement: History restoration actions are explicit and observable

Restore SHALL target the inspection selection. Undo and Redo SHALL target the current document node. The dialog SHALL identify the preferred redo child and expose alternate direct-child choices. Navigation SHALL expose execution and failure state. Inspection SHALL preserve pending previews; explicit navigation SHALL use the existing preview-discard behavior.

#### Scenario: Restore the selected node
- **WHEN** a user invokes Restore to This Node on a valid non-current selection
- **THEN** the selected committed document is restored without rerunning its operation
- **AND** duplicate navigation submissions are blocked during execution
- **AND** the resulting current node becomes selected and visible

#### Scenario: Selection is already current
- **WHEN** the selected node is the current document node
- **THEN** Restore is disabled and the inspector indicates Current State

#### Scenario: Choose an alternate redo path
- **WHEN** the current node has multiple direct children
- **THEN** Redo exposes those children with localized summaries and a preferred-child indicator
- **AND** selecting a choice explicitly restores that child rather than the inspection selection

#### Scenario: Navigate with a pending preview
- **WHEN** a pending content preview exists and the user inspects history
- **THEN** inspection preserves that preview
- **AND** navigation actions show a notice that executing them discards the preview
- **WHEN** the user executes a history navigation action
- **THEN** the command discards the preview through the existing invalidation path

#### Scenario: Restoration fails
- **WHEN** restoration fails before publication
- **THEN** the current document and cursor remain unchanged
- **AND** the selected inspection node remains selected
- **AND** the dialog displays a localized failure and remains usable

### Requirement: History inspection handles asynchronous and session lifetimes

The inspector SHALL expose loading, available, and error states. Results SHALL be accepted only for the current selection, session root, request generation, and open view lifetime. Closing or ending the session SHALL cancel detail work and release owned references.

#### Scenario: A slow query completes after a newer selection
- **WHEN** a user selects node A and then node B before A's query completes
- **THEN** only B's result can populate the current inspector
- **AND** A's obsolete work is canceled or its result is discarded

#### Scenario: Detail query fails for a valid target
- **WHEN** inspection fails for a retained non-current node
- **THEN** the inspector shows an error and a retry action
- **AND** an otherwise valid Restore action remains available

#### Scenario: The session is replaced or ended
- **WHEN** the active session ends or is replaced while the dialog has detail work
- **THEN** old rows, selection, details, and actions are cleared or the dialog is closed by its host
- **AND** no old result is displayed in a new session

### Requirement: History layout and input remain responsive and accessible

Wide layouts SHALL show a resizable tree pane beside an inspector. Narrow layouts SHALL provide History and Details pages with preserved selection, expansion, and scroll anchors. Footers SHALL remain reachable independently of content scrolling. Native and embedded Avalonia hosts SHALL provide the same inspection and explicit-restoration semantics.

#### Scenario: Resize between wide and narrow layouts
- **WHEN** the dialog is resized between its wide and narrow layouts
- **THEN** no tree, details, or footer control overlaps or becomes unreachable
- **AND** the selected node, expansion choices, and scroll anchors remain available
- **AND** long titles and values can still be read through tooltips or expanded detail

#### Scenario: Complete the workflow with a keyboard
- **WHEN** a user navigates without a pointer
- **THEN** native tree arrow keys select rows or expand branches, and Enter enters details
- **AND** Tab reaches the history actions, inspector, restore, and close controls
- **AND** Escape closes the dialog and returns focus to the history button
- **AND** selection alone does not restore content

#### Scenario: Inspect through narrow pages
- **WHEN** a user selects a row on the narrow History page
- **THEN** the History page remains open while the selected detail is prepared
- **WHEN** the user invokes Enter or the Details tab
- **THEN** the selected inspector becomes visible
- **AND** returning through the History tab preserves the selected row and scroll anchor

#### Scenario: Theme and text size change
- **WHEN** history is rendered in light or dark theme with long localized strings or enlarged UI text
- **THEN** native tree expanders, current/selected indicators, values, and enabled actions remain readable
- **AND** accessible names communicate current, selected, and expanded states without color-only cues
