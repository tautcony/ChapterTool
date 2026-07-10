## MODIFIED Requirements

### Requirement: Font settings persist canonical family identity
The system SHALL persist font selections by canonical family name in the font section of the unified versioned settings document.

#### Scenario: Selected families are saved
- **WHEN** the user saves non-default UI and monospace selections
- **THEN** the unified font section SHALL persist the canonical family name for each category
- **AND** it SHALL NOT persist selection indexes, localized option labels, or serialized Avalonia objects

#### Scenario: Default selections have stable storage values
- **WHEN** the user saves either category's explicit default option
- **THEN** that category SHALL be persisted using its stable default representation
- **AND** the representation SHALL be independent of the active UI language and installed-font ordering

#### Scenario: Loading does not rewrite current font settings
- **WHEN** current-version font settings are loaded and resolved for runtime use
- **THEN** the store SHALL NOT write or replace `settings.json` solely because runtime normalization occurred

#### Scenario: Predecessor font settings import once
- **WHEN** `font-settings.json` contains compatible family identities and the unified settings document does not exist
- **THEN** both categories SHALL be imported into the unified font section
- **AND** the predecessor file SHALL remain unchanged

#### Scenario: Malformed unified font settings are preserved
- **WHEN** active `settings.json` contains malformed JSON or an incompatible font section
- **THEN** the existing corrupt-settings preservation behavior SHALL preserve the malformed active file
- **AND** the running application SHALL use both font defaults

### Requirement: Font settings resolve missing and unavailable values safely
The system SHALL resolve each font category independently against the current system catalog.

#### Scenario: Missing settings use both defaults
- **WHEN** the unified settings document or its font section does not exist and no compatible predecessor font file is importable
- **THEN** the UI font SHALL resolve to the system UI default
- **AND** the monospace font SHALL resolve to the system monospace fallback

#### Scenario: Blank values use category defaults
- **WHEN** either persisted family name is blank
- **THEN** only that category SHALL resolve to its default
- **AND** the other category SHALL retain its valid selection

#### Scenario: Unavailable values use category defaults
- **WHEN** a persisted family name is not present in the current system catalog
- **THEN** only that category SHALL resolve to its default
- **AND** runtime resolution alone SHALL NOT rewrite the persisted file

#### Scenario: Monospace default retains a generic fallback
- **WHEN** the system monospace default is resolved on any supported platform
- **THEN** its fallback chain SHALL end with the generic `monospace` family
