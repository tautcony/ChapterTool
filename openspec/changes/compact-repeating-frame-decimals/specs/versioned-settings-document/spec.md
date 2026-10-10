## ADDED Requirements

### Requirement: Repeating frame display is an additive application preference

The system MUST persist the optional boolean application.showRepeatingFrameDecimals in the existing version-one settings document. The value MUST default to true when absent. An explicit false MUST survive normalization and saving. Loading this additive preference MUST NOT rewrite an otherwise valid current-version document. Saving it MUST preserve all other application, theme, font, and shortcut values.

#### Scenario: Load existing desktop settings
- **WHEN** a valid version-one settings.json omits application.showRepeatingFrameDecimals
- **THEN** the normalized preference is true
- **AND** loading alone leaves the file unchanged

#### Scenario: Restore an explicitly disabled preference
- **WHEN** application.showRepeatingFrameDecimals is false
- **THEN** normalization and reopening retain false
- **AND** ordinary decimal presentation remains active when rounding is inactive

#### Scenario: Save without changing unrelated preferences
- **WHEN** the user saves a changed repeating-frame preference
- **THEN** the aggregate uses the existing save operation
- **AND** schemaVersion remains one
- **AND** other settings content is preserved

#### Scenario: Load and save browser settings
- **WHEN** the existing browser settings document lacks the optional preference
- **THEN** the preference defaults to true without rewriting storage on load
- **WHEN** the user saves a false draft value
- **THEN** the existing storage key retains false for the next browser session
- **AND** unrelated settings remain unchanged

### Requirement: Frame display mode persists full precision

The application settings MUST accept and persist `frameDisplayMode` values `round`, `decimal-places`, and `full-precision`. Unknown values MUST continue to normalize to `round`. The setting MUST retain the existing schema version and preserve the selected decimal-place count for later mode changes.

#### Scenario: Save and reload no-truncation mode
- **WHEN** the user selects no truncation and saves Settings
- **THEN** the normalized settings store `full-precision`
- **AND** reloading Settings selects no truncation with the saved decimal-place count unchanged
