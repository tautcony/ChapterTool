# wasm-modal-workflows Specification

## Purpose
TBD - created by archiving change fix-wasm-modal-layout. Update Purpose after archive.
## Requirements
### Requirement: Independent tool dialogs
The browser UI SHALL display history, expression authoring, advanced export options, settings, logs, preview, frame shift, and related media through one shared modal lifecycle. Only one dialog SHALL be active. Tool content SHALL remain outside the main editing grid.

#### Scenario: Open history on a short viewport
- **WHEN** the user opens history at 390×640 or 844×390
- **THEN** the dialog close button is inside the viewport and accepts an actual click
- **AND** the chapter grid does not change size or overlap the dialog

### Requirement: Accessible modal lifecycle
Dialogs SHALL have an accessible title, contain keyboard focus, make the background inert, close on Escape from focused inputs, and restore focus to the opener. Draft dialogs SHALL require explicit Apply or cancellation. Background shortcuts and file drops SHALL not change workspace data while a dialog is active.

#### Scenario: Cancel an input-focused dialog
- **WHEN** the user edits a dialog input and presses Escape
- **THEN** the dialog closes without committing the draft
- **AND** focus returns to its opener

### Requirement: Expression draft isolation
Expression authoring SHALL retain live debounced validation. Apply SHALL commit a valid candidate once. Cancel SHALL discard the candidate, cancel pending validation, and restore the previous expression and preset without changing chapters or history.

#### Scenario: Cancel an invalid expression
- **WHEN** the user enters an invalid expression and cancels
- **THEN** committed chapters, history, and exported content remain unchanged
- **AND** reopening displays the previous expression

#### Scenario: Apply a valid expression
- **WHEN** the user applies a valid candidate
- **THEN** the dialog closes and the chapter table reflects one committed operation
- **AND** undo and redo retain their existing behavior

### Requirement: Advanced export draft isolation
Order shift, XML language, template loading, encoding, and BOM SHALL be available in advanced export options. Apply SHALL use the existing projection and settings persistence paths. Cancel SHALL leave those values and projections unchanged.

#### Scenario: Cancel advanced options
- **WHEN** the user changes advanced export values or selects a template and cancels
- **THEN** exported bytes, persisted settings, and chapter projections remain unchanged

### Requirement: Responsive workflow zones
The main UI SHALL retain toolbar, chapter grid, compact export controls, and status zones. Dialog bodies SHALL scroll independently. At widths of 320 pixels and above, dialog controls SHALL remain readable without page horizontal overflow. Mobile dialog action targets SHALL be at least 44 pixels high.

#### Scenario: Long diagnostics in landscape
- **WHEN** expression diagnostics exceed the body height at 844×390
- **THEN** the body scrolls and the header and footer remain reachable
- **AND** Apply, Cancel, and Close do not overlap

### Requirement: Browser regression gates
The default browser suite SHALL assert modal geometry, focus, real hit targets, cancellation, history navigation, and background isolation. Visual checks SHALL cover idle and modal states in the fixed Linux environment. Pull requests SHALL run Chromium, targeted WebKit regression checks, and visual checks.

#### Scenario: Detect a layout regression
- **WHEN** a change puts history back into the main grid or covers its close action
- **THEN** a default browser test fails without relying only on screenshot generation

#### Scenario: Validate a published site
- **WHEN** CI checks the published browser artifact
- **THEN** a short-viewport smoke test opens and closes a native dialog

