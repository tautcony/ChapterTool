## MODIFIED Requirements

### Requirement: Settings use one sectioned document
The system SHALL persist current user configuration in one `settings.json` document containing a top-level schema version and typed application, theme, font, and shortcut sections.

#### Scenario: Saving the aggregate creates the unified shape
- **WHEN** aggregate settings are saved and no active configuration exists
- **THEN** `settings.json` SHALL be written with the current `schemaVersion`
- **AND** it SHALL contain `application`, `theme`, `font`, and `shortcuts` child content

#### Scenario: Settings workflow loads and saves once
- **WHEN** a settings workflow loads application, theme, font, and shortcut values and then saves changes across one or more child sections
- **THEN** it SHALL call the aggregate store load exactly once
- **AND** it SHALL commit the updated aggregate exactly once
- **AND** the store SHALL replace `settings.json` exactly once for that commit

#### Scenario: Unchanged document is parsed once
- **WHEN** multiple consumers load settings through the shared aggregate store and `settings.json` has not changed
- **THEN** the first load SHALL parse the document
- **AND** subsequent loads SHALL reuse the cached normalized aggregate without reopening or reparsing the file

#### Scenario: Isolated update preserves other child content
- **WHEN** a consumer updates one child value through the aggregate store update operation
- **THEN** the store SHALL read the latest document once under the canonical-path lock
- **AND** it SHALL write the transformed aggregate once while preserving other child content

#### Scenario: No section persistence stores exist
- **WHEN** application services, ViewModels, CLI workflows, and tool discovery access persisted settings
- **THEN** they SHALL depend on `ISettingsStore<ChapterToolSettings>`
- **AND** the system SHALL NOT expose separate application, theme, font, or shortcut persistence stores
