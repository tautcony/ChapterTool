## ADDED Requirements

### Requirement: Browser frame cells use Core edit semantics

The Web chapter grid MUST permit edits to time, name, and frame fields through Core cell-edit semantics. Frame edits MUST use the same FPS basis, parsing, related-field updates, and validation as Avalonia. Separator and missing-FPS restrictions MUST match the applicable Core operation.

#### Scenario: Edit a frame value
- **WHEN** the user commits a valid frame value for an ordinary chapter with valid FPS
- **THEN** the chapter time and derived frame display match the Avalonia result
- **AND** export uses the committed timing and one Undo restores the prior document

#### Scenario: Edit a protected or invalid field
- **WHEN** the user attempts an unsupported separator edit or frame edit without required FPS
- **THEN** the host prevents or rejects that edit with an accessible explanation
- **AND** no partial content or history change occurs

### Requirement: Browser cells have explicit draft and commit states

A Web cell edit MUST capture chapter identity, field, original value, and base version. Enter, Tab, or valid focus loss MUST commit once. Escape MUST discard the draft. Invalid input MUST remain visible with a localized error and MUST NOT be coerced to zero. Text-control Undo MUST affect the draft while the input owns focus.

#### Scenario: Enter is followed by focus loss
- **WHEN** the user presses Enter on a valid edit and the input then loses focus
- **THEN** at most one content transaction is created
- **AND** an unchanged canonical value creates no history node

#### Scenario: Escape or local Undo
- **WHEN** the user presses Escape during an edit
- **THEN** the original value is restored without a document transaction
- **WHEN** the focused input receives a text Undo gesture
- **THEN** document history does not move

#### Scenario: Invalid input stays available for correction
- **WHEN** a time or frame draft cannot be parsed or violates timing invariants
- **THEN** the draft remains visible with an accessible error
- **AND** committed rows, history, and export retain the preceding valid values

#### Scenario: A row position becomes stale
- **WHEN** history navigation or clip replacement changes the row projection during an edit
- **THEN** the old edit cannot commit against a different chapter at that row index
- **AND** the host resolves the stale draft without changing unrelated chapters

### Requirement: Table workflows remain reachable by keyboard and scrolling

The Web grid MUST preserve surviving chapter selection identities and usable focus after accepted edits. Keyboard users MUST reach editable fields, selection actions, and portable row commands. A rendered table with 1000 chapters MUST make every row reachable without losing committed edits or blocking the primary workflow controls.

#### Scenario: Edit a late row in a large table
- **WHEN** the user scrolls to and edits the last chapter in a 1000-row table
- **THEN** that chapter commits correctly and remains reachable
- **AND** preview, history, and output actions remain usable

#### Scenario: Undo an edit on a selected chapter
- **WHEN** the user commits an edit and invokes document Undo with table focus
- **THEN** the previous content is restored
- **AND** the surviving chapter remains selected with a usable focus target

### Requirement: Browser expression tools load Lua files as drafts

The Web expression tool MUST provide a `.lua` file input. Loading MUST use the shared script-content rules and bounded input behavior. Success MUST populate the script draft and source metadata without committing chapters. Cancel, failed read, or invalid script content MUST preserve the entry expression state and document.

#### Scenario: Load a multiline script
- **WHEN** the user loads an accepted Lua file containing multiline UTF-8 text
- **THEN** line breaks and script content appear in the editor
- **AND** the source filename is inspectable and a read-only preview starts
- **AND** document history remains unchanged until explicit Apply

#### Scenario: File read fails or exceeds its limit
- **WHEN** Lua file input fails shared validation or bounded reading
- **THEN** the tool reports a localized error
- **AND** the preceding script, preset, source metadata, document, and history are preserved

#### Scenario: Cancel a loaded script draft
- **WHEN** the user loads a script and closes the tool without applying it
- **THEN** the entry script, preset, and source metadata are restored on reopening
- **AND** no chapter content changes

### Requirement: Browser authoring uses shared diagnostics and completion

The Web editor MUST present Lua syntax highlighting, completion, and positioned diagnostics from the shared authoring service. Assistance MUST operate on the current draft and caret. Diagnostic positions MUST be shown only when the service supplies them. Completion MUST preserve surrounding text and multiline formatting.

#### Scenario: Accept a completion
- **WHEN** the user requests and accepts a valid completion at the caret
- **THEN** the selected completion replaces only its intended draft range
- **AND** surrounding script text remains intact and no chapter transaction occurs

#### Scenario: Rapid edits replace old diagnostics
- **WHEN** a later draft replaces an earlier draft before authoring results arrive
- **THEN** only diagnostics for the latest draft are displayed
- **AND** supplied positions map to that draft without invented locations

### Requirement: Editor assistance preserves expression review and accessibility

The enhanced editor MUST preserve the existing expression candidate review, debounce, composition, cancellation, and exact-candidate application contract. Highlighting MUST NOT duplicate text for assistive technology. Enter MUST insert a newline when completion is inactive. Completion MUST consume Escape before the outer dialog. Ctrl/Cmd+Z MUST remain local to the editor.

#### Scenario: Type with an input method
- **WHEN** IME composition is active
- **THEN** incomplete composition does not trigger candidate evaluation or destructive completion
- **AND** composition completion schedules only the current draft

#### Scenario: Dismiss completion before the dialog
- **WHEN** completion is open and the user presses Escape
- **THEN** completion closes and the expression tool remains open
- **WHEN** the user presses Escape again with no inner surface active
- **THEN** the expression tool follows its existing cancel and focus-restoration behavior

#### Scenario: Use the editor at narrow sizes
- **WHEN** the expression tool is used at default, wide, narrow, or short supported sizes
- **THEN** editor text, diagnostics, completion, Cancel, and Apply remain reachable
- **AND** editor assistance does not create page-level horizontal overflow
