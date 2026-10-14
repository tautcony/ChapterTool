# wasm-expression-editing Specification

## Purpose
TBD - created by archiving change live-expression-preview. Update Purpose after archive.
## Requirements
### Requirement: WASM expression editing provides a live read-only preview
The WASM application SHALL let users edit Lua expression drafts and inspect a live read-only candidate preview. The preview SHALL NOT mutate the committed document, grid rows, export output, or history.

#### Scenario: Typing refreshes the browser candidate preview
- **WHEN** the user enters or edits expression text
- **THEN** the browser SHALL refresh a candidate preview from the current committed document
- **AND** it SHALL show changed chapter values, no-change state, or validation errors
- **AND** it SHALL keep the expression input usable without a persistent enable checkbox

#### Scenario: Selecting a preset refreshes the browser preview
- **WHEN** the user selects a built-in expression preset
- **THEN** the editor SHALL populate the preset script and refresh the read-only preview
- **AND** selecting the preset SHALL NOT change chapter values or history

#### Scenario: Invalid candidate cannot be applied
- **WHEN** Lua evaluation or document validation fails
- **THEN** the page SHALL display the current diagnostic
- **AND** the Apply action SHALL be disabled
- **AND** the committed grid and history SHALL remain unchanged

#### Scenario: Applying a valid candidate updates committed chapters
- **WHEN** the user applies a valid current candidate
- **THEN** the page SHALL commit one atomic history transaction
- **AND** the chapter grid, format preview, and download SHALL use the committed values
- **AND** Undo SHALL restore the values before the expression operation without rerunning Lua

#### Scenario: Canceling a candidate leaves the document unchanged
- **WHEN** the user cancels a live candidate preview
- **THEN** the page SHALL discard that preview
- **AND** the document, grid, export output, and history SHALL remain unchanged

#### Scenario: Editing an applied row does not rerun the expression
- **WHEN** the user edits a chapter after applying an expression
- **THEN** the page SHALL retain the user's chapter value
- **AND** preview and download SHALL NOT run the previous expression again

