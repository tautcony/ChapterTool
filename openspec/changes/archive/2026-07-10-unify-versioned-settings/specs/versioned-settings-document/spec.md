## ADDED Requirements

### Requirement: Settings use one sectioned document
The system SHALL persist current user configuration in one `settings.json` document containing a top-level schema version and typed application, theme, and font sections.

#### Scenario: Saving the aggregate creates the unified shape
- **WHEN** aggregate settings are saved and no active configuration exists
- **THEN** `settings.json` SHALL be written with the current `schemaVersion`
- **AND** it SHALL contain `application`, `theme`, and `font` child content

#### Scenario: Settings workflow loads and saves once
- **WHEN** a settings workflow loads application, theme, and font values and then saves changes across one or more child sections
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
- **AND** the system SHALL NOT expose separate application, theme, or font persistence stores

### Requirement: Known settings versions upgrade explicitly
The system SHALL identify the settings document structure with an integer `schemaVersion` and SHALL apply contiguous, ordered upgrade steps for supported older versions.

#### Scenario: Unversioned section document upgrades to version one
- **WHEN** `settings.json` contains a recognized section document without `schemaVersion`
- **THEN** it SHALL be treated as version zero
- **AND** it SHALL be upgraded and persisted as the current version before typed settings are returned

#### Scenario: Current version loads without rewrite
- **WHEN** `settings.json` already uses the current schema version and typed values only require runtime normalization
- **THEN** loading SHALL return normalized typed values
- **AND** loading alone SHALL NOT rewrite the document

#### Scenario: Invalid version metadata is corrupt
- **WHEN** `settings.json` contains missing-version content that is not a recognized older shape, a negative version, or a non-integral version
- **THEN** the active file SHALL use the corrupt-settings preservation behavior
- **AND** the caller SHALL receive a structured load failure

### Requirement: Newer settings versions are protected from downgrade
The system SHALL reject a settings document whose `schemaVersion` is greater than the running application's supported version.

#### Scenario: Future version load fails without moving the file
- **WHEN** the running application loads a future-version `settings.json`
- **THEN** it SHALL report an unsupported-version I/O failure
- **AND** it SHALL leave the active file unchanged at its original path

#### Scenario: Future version save cannot overwrite the file
- **WHEN** the aggregate store attempts to save or update after a future-version document exists
- **THEN** the save SHALL fail before writing a replacement
- **AND** the future-version document SHALL remain byte-for-byte unchanged

### Requirement: Predecessor settings import is best effort
When `settings.json` is absent, the system SHALL independently import compatible values from `appsettings.json`, `theme-settings.json`, and `font-settings.json` into the current unified document.

#### Scenario: Three valid predecessor files are combined
- **WHEN** all three predecessor files contain valid typed settings and `settings.json` is absent
- **THEN** their values SHALL be written into the corresponding unified sections
- **AND** all predecessor files SHALL remain unchanged

#### Scenario: One malformed predecessor does not block other sections
- **WHEN** at least one predecessor file is valid and another contains malformed JSON
- **THEN** the valid section SHALL be imported
- **AND** the malformed section SHALL use current defaults
- **AND** the malformed predecessor file SHALL remain available for manual recovery

#### Scenario: Missing configuration stays read-only
- **WHEN** neither `settings.json` nor any importable predecessor file exists
- **THEN** typed loads SHALL return current defaults
- **AND** loading alone SHALL NOT create a configuration file

### Requirement: Malformed active settings are preserved
The system SHALL apply the existing corrupt-file preservation behavior to malformed active `settings.json` content.

#### Scenario: Invalid active JSON is moved aside
- **WHEN** active `settings.json` cannot be parsed or converted to the current typed document
- **THEN** it SHALL be moved to an available `.corrupt` backup path
- **AND** the caller SHALL receive `CorruptSettingsFileException` containing the active and backup paths
