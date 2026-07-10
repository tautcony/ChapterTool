## MODIFIED Requirements

### Requirement: Typed settings cover editable application preferences
The settings system SHALL persist all settings exposed by the unified settings panel through one typed cross-platform aggregate store backed by one versioned settings document while preserving best-effort predecessor migration behavior.

#### Scenario: Existing settings still load
- **WHEN** predecessor `appsettings.json` files omit newly added application settings fields and no unified settings document exists
- **THEN** the settings store SHALL import compatible values successfully and use defaults matching the current application startup behavior

#### Scenario: Settings panel commits once
- **WHEN** the user saves application, theme, and font choices from the settings panel
- **THEN** the panel SHALL submit one aggregate settings value through one store call
- **AND** the store SHALL perform one atomic replacement of the unified document

#### Scenario: Workflow defaults persist
- **WHEN** the user saves default save format or default XML language from the settings panel
- **THEN** those defaults SHALL be written to the application child content and applied when a new main window ViewModel loads aggregate settings

#### Scenario: Appearance settings remain typed
- **WHEN** appearance settings are saved from the settings panel
- **THEN** theme preset identity and font family choices SHALL be written as child content of the same aggregate settings value
