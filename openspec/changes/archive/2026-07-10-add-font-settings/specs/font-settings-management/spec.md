## ADDED Requirements

### Requirement: Font settings keep UI and monospace choices independent
The system SHALL represent the general UI font family and monospace font family as independent settings.

#### Scenario: UI font changes independently
- **WHEN** the user selects a different UI font family without changing the monospace selection
- **THEN** the effective UI font family SHALL change
- **AND** the effective monospace font family SHALL remain unchanged

#### Scenario: Monospace font changes independently
- **WHEN** the user selects a different monospace font family without changing the UI selection
- **THEN** the effective monospace font family SHALL change
- **AND** the effective UI font family SHALL remain unchanged

#### Scenario: Font settings exclude typography dimensions
- **WHEN** font settings are loaded or saved
- **THEN** they SHALL contain font-family choices only
- **AND** they SHALL NOT introduce configurable font size, weight, style, line height, or letter spacing

### Requirement: Font choices come from the current system catalog
The system SHALL expose font-family choices that Avalonia can discover on the current operating system.

#### Scenario: Installed families are listed deterministically
- **WHEN** the font catalog is built
- **THEN** it SHALL include each non-blank discovered family name once using case-insensitive duplicate detection
- **AND** it SHALL expose the resulting family names in a deterministic culture-aware display order

#### Scenario: Both categories include explicit defaults
- **WHEN** font options are presented
- **THEN** the UI font options SHALL include an explicit system UI default before installed families
- **AND** the monospace font options SHALL include an explicit system monospace default before installed families

#### Scenario: Monospace choices are not guessed by name
- **WHEN** monospace font options are presented
- **THEN** installed families SHALL remain selectable without name-based or platform-specific fixed-pitch filtering
- **AND** the UI SHALL identify that selection as the font used for monospace content

### Requirement: Font settings persist canonical family identity
The system SHALL persist font selections by canonical family name in a dedicated typed settings file.

#### Scenario: Selected families are saved
- **WHEN** the user saves non-default UI and monospace selections
- **THEN** `font-settings.json` SHALL persist the canonical family name for each category
- **AND** it SHALL NOT persist selection indexes, localized option labels, or serialized Avalonia objects

#### Scenario: Default selections have stable storage values
- **WHEN** the user saves either category's explicit default option
- **THEN** that category SHALL be persisted using its stable default representation
- **AND** the representation SHALL be independent of the active UI language and installed-font ordering

#### Scenario: Loading does not rewrite font settings
- **WHEN** font settings are loaded and resolved for runtime use
- **THEN** the store SHALL NOT write or replace `font-settings.json` solely because loading occurred

#### Scenario: Malformed font settings are preserved
- **WHEN** `font-settings.json` contains malformed JSON
- **THEN** the existing corrupt-settings preservation behavior SHALL preserve the malformed file
- **AND** the running application SHALL use both font defaults

### Requirement: Font settings resolve missing and unavailable values safely
The system SHALL resolve each font category independently against the current system catalog.

#### Scenario: Missing settings use both defaults
- **WHEN** `font-settings.json` does not exist
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

### Requirement: Font discovery and resolution remain testable without host fonts
The font-settings runtime boundary SHALL allow deterministic verification without depending on the test machine's installed font set.

#### Scenario: A fixed catalog controls resolution
- **WHEN** tests provide a fixed set of available family names
- **THEN** selection, availability fallback, ordering inputs, and application results SHALL use that fixed catalog
- **AND** tests SHALL NOT require installation of a particular operating-system font
