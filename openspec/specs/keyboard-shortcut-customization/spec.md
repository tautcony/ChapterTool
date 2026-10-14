## Purpose

Provide a unified, testable, and persistent shortcut catalog for the main window and settings tool so users can safely customize gestures and use them in the current session.

## Requirements

### Requirement: Shortcut catalog exposes stable actions and defaults
The system SHALL expose a shortcut action catalog with stable identifiers, display names, default gestures, and availability descriptions.

#### Scenario: Catalog contains existing actions
- **WHEN** the shortcut catalog is initialized
- **THEN** it SHALL contain save、reload、log、preview and clip-selection actions with their current default gestures

#### Scenario: Defaults are deterministic
- **WHEN** no user shortcut overrides exist
- **THEN** runtime routing and visible menu gestures SHALL use the catalog defaults

### Requirement: Users can edit shortcuts in settings
The settings tool SHALL provide a dedicated "Shortcuts" tab that allows users to record, clear, reset, and save gestures for configurable actions.

#### Scenario: Record a new gesture
- **WHEN** the user presses a valid key combination in a shortcut row and saves
- **THEN** the draft gesture for that action SHALL update and appear in the current window

#### Scenario: Reset one shortcut
- **WHEN** the user selects reset for an action
- **THEN** that action SHALL restore the catalog default gesture and other drafts SHALL remain unchanged

### Requirement: Invalid and conflicting gestures are rejected
The system SHALL reject gestures that cannot be parsed, reserved key combinations without modifiers, and duplicate gestures assigned to multiple actions, and SHALL provide localizable error states.

#### Scenario: Duplicate gesture
- **WHEN** two actions use the same non-empty gesture
- **THEN** the settings tool SHALL mark the conflict and prevent saving

#### Scenario: Invalid gesture input
- **WHEN** the user enters text that Avalonia KeyGesture cannot parse
- **THEN** the row SHALL show a validation error and SHALL NOT change the saved mapping

### Requirement: Runtime routing uses the active mapping
The main window SHALL route gestures to the corresponding commands according to the current session shortcut mapping; it SHALL NOT intercept editing shortcuts when a text input control has focus.

#### Scenario: Customized gesture invokes command
- **WHEN** the user presses a customized gesture in the main window after saving it
- **THEN** the corresponding command SHALL execute, and the old default gesture SHALL no longer trigger that action unless it remains assigned to another action

#### Scenario: Text editing preserves control shortcuts
- **WHEN** an editable control has focus and the user presses an editing shortcut
- **THEN** the shortcut router SHALL NOT execute an application command

### Requirement: Shortcut changes apply and persist safely
Shortcut drafts SHALL follow the settings tool's existing load, live-apply, save, reset, and discard lifecycle, and SHALL preserve the last valid mapping when saving fails.

#### Scenario: Save and reload
- **WHEN** the user saves valid shortcuts and reopens the settings tool
- **THEN** the saved gestures SHALL be restored one-to-one with the catalog action identifiers

#### Scenario: Discard unsaved edits
- **WHEN** the user changes shortcuts and discards the settings
- **THEN** the current session SHALL restore the most recently saved mapping

### Requirement: Headless workflow verifies shortcut customization
The system SHALL provide Avalonia Headless tests that drive the real settings view and main-window commands to verify shortcut customization behavior.

#### Scenario: Headless customize workflow
- **WHEN** a Headless test records and saves a gesture in the Shortcuts tab and triggers the customized gesture
- **THEN** the test SHALL observe settings persistence, an updated menu gesture, and execution of the target command
