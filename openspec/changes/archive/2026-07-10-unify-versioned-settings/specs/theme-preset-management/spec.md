## MODIFIED Requirements

### Requirement: Theme preset persistence resolves safely
The system SHALL persist stable preset identity in the theme section of the unified versioned settings document and SHALL resolve unavailable selections to a deterministic default.

#### Scenario: Selected preset is persisted by id
- **WHEN** the user saves appearance settings
- **THEN** the unified theme section SHALL persist the selected stable preset id rather than semantic palette values or a localized display name

#### Scenario: Missing theme section uses the default
- **WHEN** the unified settings document or its theme section does not exist and no compatible predecessor theme file is importable
- **THEN** the selected preset SHALL resolve to `Avalonia Default`

#### Scenario: Blank or unknown preset id uses the default
- **WHEN** the unified theme section contains a blank or unrecognized preset id
- **THEN** the selected preset SHALL resolve to `Avalonia Default`
- **AND** loading or live application alone SHALL NOT rewrite a current-version settings document

#### Scenario: Predecessor preset settings import once
- **WHEN** `theme-settings.json` contains a compatible preset id and the unified settings document does not exist
- **THEN** the preset id SHALL be imported into the unified theme section
- **AND** the predecessor file SHALL remain unchanged

#### Scenario: Legacy theme colors remain intentionally ignored
- **WHEN** only the legacy `theme-colors.json` file exists
- **THEN** the selected preset SHALL resolve to `Avalonia Default`
- **AND** the legacy six-slot values SHALL NOT be migrated into the preset model

#### Scenario: Malformed unified settings are preserved
- **WHEN** active `settings.json` contains malformed JSON
- **THEN** the existing corrupt-settings preservation behavior SHALL be used
- **AND** the running application SHALL fall back to `Avalonia Default`
