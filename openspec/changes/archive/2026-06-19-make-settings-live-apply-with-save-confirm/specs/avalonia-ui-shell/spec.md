## MODIFIED Requirements

### Requirement: Settings changes apply predictably
The settings panel SHALL save, apply, reset, validate, and discard changes in a way that keeps the main ViewModel, visible runtime state, and persisted settings consistent.

#### Scenario: Runtime-safe settings apply immediately
- **WHEN** the user changes a runtime-safe setting in the settings panel
- **THEN** the running shell SHALL update language, default save directory, save defaults, frame accuracy tolerance, and appearance state without waiting for Save
- **AND** the typed settings stores SHALL NOT be written solely because the value changed in the panel

#### Scenario: Save persists currently applied settings
- **WHEN** the user saves settings after making live changes
- **THEN** the current settings panel values SHALL be written to the typed settings stores
- **AND** the running shell SHALL continue using those values without requiring a restart
- **AND** the settings panel SHALL no longer be considered dirty

#### Scenario: Closing with unsaved live changes requires confirmation
- **WHEN** the user closes the settings window after changing settings that have not been saved
- **THEN** the shell SHALL show a localized confirmation prompt before closing
- **AND** canceling the prompt SHALL keep the settings window open with the live changes still applied

#### Scenario: Discarding unsaved live changes restores saved settings
- **WHEN** the user confirms discarding unsaved settings changes while closing the settings window
- **THEN** the shell SHALL restore the last loaded or saved settings to the running main ViewModel and appearance service
- **AND** the settings window SHALL be allowed to close
- **AND** the typed settings stores SHALL remain unchanged

#### Scenario: Reset restores defaults
- **WHEN** the user resets a settings group to defaults
- **THEN** the panel SHALL restore the same defaults used by a fresh application start
- **AND** reset values SHALL apply immediately to the running shell while still requiring Save for persistence

#### Scenario: Invalid settings are surfaced
- **WHEN** a setting value is invalid or an external tool path cannot be resolved
- **THEN** the settings panel SHALL show a localized validation message and SHALL NOT silently discard the user's input
