## ADDED Requirements

### Requirement: Avalonia expression editing provides a live read-only preview
Avalonia expression inputs SHALL refresh a read-only candidate preview while the user edits the script or selects a preset. The preview SHALL remain separate from the editable chapter grid and SHALL require an explicit Apply action to change the document.

#### Scenario: Main expression input refreshes the candidate preview
- **WHEN** the user edits the expression in the main window or selects an expression preset
- **THEN** the UI SHALL refresh the candidate from the current committed document
- **AND** it SHALL display changed chapter values or a no-change result
- **AND** the main chapter grid SHALL continue to display committed values

#### Scenario: Expression tool shows live differences and diagnostics
- **WHEN** the expression tool is open and the user edits its Lua script
- **THEN** the UI SHALL refresh a read-only before-and-after preview
- **AND** it SHALL show current Lua and document validation errors
- **AND** it SHALL disable Apply while the candidate is invalid

#### Scenario: User applies or cancels the live candidate
- **WHEN** the user applies a valid current expression candidate
- **THEN** the UI SHALL commit one history transaction and refresh the grid from the committed document
- **AND** format preview and save SHALL use those committed values without rerunning the expression
- **WHEN** the user cancels the candidate
- **THEN** the UI SHALL clear preview data without changing document content or history

#### Scenario: Expression text is a draft without a persistent enable toggle
- **WHEN** the user edits expression text or selects a preset
- **THEN** the UI SHALL treat that value as an unapplied operation draft
- **AND** it SHALL require an explicit Apply action to commit it
- **AND** it SHALL NOT expose a persistent checkbox that silently enables expression projection during preview or save

## MODIFIED Requirements

### Requirement: Lua expression script authoring
The Avalonia shell SHALL allow users to author and apply Lua expression transforms with built-in presets and external script selection. Expression text SHALL remain an operation draft until the user explicitly applies its candidate.

#### Scenario: Expression tool exposes Lua script editing
- **WHEN** the expression tool window is opened
- **THEN** it SHALL present Lua script editing as the expression authoring surface
- **AND** it SHALL NOT require users to choose or understand the previous formula/postfix grammar

#### Scenario: Built-in Lua preset can populate script text
- **WHEN** the user selects a built-in Lua script preset in the expression tool or accepts a `preset.*` completion in the editor
- **THEN** the tool SHALL show or insert the preset script text
- **AND** it SHALL prepare a live candidate preview without changing the document
- **AND** it SHALL apply that script to the document only after the user confirms Apply

#### Scenario: External Lua script can be selected
- **WHEN** the user chooses an external `.lua` script from the main expression input or expression tool
- **THEN** the UI SHALL expose the load action as a button at the right side of the Lua expression input
- **AND** it SHALL use the file picker service abstraction to select and read the script text
- **AND** loading the script SHALL prepare a preview without changing the document
- **AND** applying the loaded script SHALL pass its text into the candidate operation without requiring Core to read the file path

#### Scenario: Lua expression or script is applied as a document operation
- **WHEN** the user edits or loads a Lua expression script
- **THEN** the UI SHALL refresh a read-only candidate preview and surface Lua diagnostics through the status/log diagnostic path
- **AND** format preview and save SHALL use committed chapter values and SHALL NOT apply the expression draft
- **WHEN** the user applies a valid candidate
- **THEN** the UI SHALL commit the candidate as one undoable transaction

#### Scenario: Simple arithmetic remains approachable through Lua
- **WHEN** the user enters a simple Lua arithmetic expression such as `t + 1`
- **THEN** the live candidate preview SHALL apply the transform without requiring `return`, a function wrapper, or any legacy postfix expression syntax
