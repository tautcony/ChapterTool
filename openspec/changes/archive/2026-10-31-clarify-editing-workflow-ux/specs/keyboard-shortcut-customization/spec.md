## ADDED Requirements

### Requirement: Shortcut hints reflect active gestures and platform aliases
Action tooltips and menu gestures MUST derive their shortcuts from the active mapping. Clearing or remapping a configurable gesture MUST remove its old hint. UI language changes MUST retain the active mapping. Open and save MUST support macOS Command aliases with the same editing-focus rules as existing actions. Fixed aliases MUST participate in conflict validation.

#### Scenario: Remap open and save
- **WHEN** the user saves new open and save gestures
- **THEN** visible action hints MUST show the new mapping and the old configurable gestures MUST stop dispatching those actions

#### Scenario: Clear a configurable gesture
- **WHEN** the user clears an action gesture
- **THEN** its tooltip MUST omit the cleared gesture while any fixed alias remains accurate

#### Scenario: Use macOS aliases
- **WHEN** the user presses Command+O or Command+S outside a text-editing scope
- **THEN** the host MUST dispatch open or save respectively
- **AND** text editing shortcuts MUST retain their established focus behavior

#### Scenario: Use browser shortcuts
- **WHEN** the browser receives a configured document shortcut outside a text editor
- **THEN** it MUST dispatch the action and cancel the corresponding browser default
- **AND** an editor MUST retain native text undo
