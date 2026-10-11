## ADDED Requirements

### Requirement: Discoverable portable main-window controls
The browser main window MUST expose Reload and Append through a visible load menu. It MUST expose XML language, numbering offset from zero through 1000, and template loading beside format and naming controls. XML language MUST be enabled only for XML output. Changing a content draft MUST NOT change committed chapters or exports.

#### Scenario: Use main-window options
- **WHEN** a user loads chapters and changes naming, numbering, or a template
- **THEN** the main window retains a draft and offers explicit preview and application
- **AND** output remains unchanged until application

#### Scenario: Choose XML output language
- **WHEN** a user selects XML output and changes UI language
- **THEN** XML choices use localized names with stable codes and preserve the selected code
- **AND** non-XML output disables the XML selector

### Requirement: Compact inline reviewed candidates
The browser main window MUST present relevant before and candidate values in the original chapter cells. It MUST keep the visible original frame basis. The main surface MUST use compact apply, discard, and refresh actions without duplicate review summaries. Display preferences MUST NOT discard a candidate or create content history. Apply MUST commit the reviewed candidate once and preserve selection and scroll position.

#### Scenario: Review and apply naming with numbering
- **WHEN** a user previews main-window naming and numbering drafts
- **THEN** changed names and numbers appear inside the original chapter table
- **AND** applying creates one atomic operation that one Undo restores

#### Scenario: Refresh display during review
- **WHEN** a user changes frame display rounding during an active candidate
- **THEN** the review remains active and can still be applied or discarded
- **AND** committed timestamps and history are unchanged

#### Scenario: Change other options after applying an expression
- **WHEN** the current history branch already contains the active track's applied expression and the user changes naming or numbering
- **THEN** the candidate retains the committed timestamps and does not replay that expression
- **AND** the editor retains the script for inspection or explicit expression authoring

#### Scenario: Keep a candidate behind the expression editor
- **WHEN** the user opens the expression dialog while a valid main-window candidate is visible and changes its independent draft
- **THEN** the background candidate values and cell geometry remain unchanged
- **AND** Cancel restores the original main candidate and leaves exports unchanged

### Requirement: Responsive portable tool workflows
Changed browser surfaces MUST preserve toolbar, grid, options, and status zones. Normal, editor, and candidate cells MUST keep timestamps on one line and decimals readable as one number. The default, wide, narrow, short, and landscape layouts MUST retain accessible primary actions and avoid page-level horizontal overflow. Portable export, log, and content dialogs MUST preserve cancellation, localized values, and modal isolation.

#### Scenario: Use compact controls on a phone viewport
- **WHEN** the user opens and uses changed controls at 390x640
- **THEN** primary actions remain reachable by real clicks and keyboard navigation
- **AND** chapter content can scroll internally without page-level horizontal overflow

### Requirement: Main toolbar history entry
The browser main toolbar MUST expose one `Edit history` action. It MUST NOT expose separate Undo and Redo buttons. Undo and Redo shortcuts and history workflows MUST remain available.

#### Scenario: Open history from the main toolbar
- **WHEN** a user opens history from the browser main toolbar
- **THEN** the toolbar exposes the same single history entry as Avalonia
- **AND** the user can still undo and redo through supported shortcuts and history actions

### Requirement: Non-layout syntax diagnostics
The browser expression editor MUST show syntax diagnostics in an accessible floating layer. Diagnostics MUST NOT participate in normal layout flow or shift the editor, grid, or workflow controls.

#### Scenario: Show syntax diagnostics while editing
- **WHEN** the expression editor contains invalid syntax
- **THEN** the diagnostic layer displays the errors and supports keyboard access
- **AND** the editor and main workflow geometry remain unchanged
