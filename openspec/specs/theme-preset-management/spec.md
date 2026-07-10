# theme-preset-management Specification

## Purpose
Define the built-in theme preset catalog, semantic palette contract, and safe preset persistence behavior.

## Requirements

### Requirement: Built-in theme preset catalog
The system SHALL provide a built-in theme preset catalog containing an Avalonia baseline preset and curated `Solarized`, `Gruvbox`, and `Ayu` families.

#### Scenario: Preset catalog includes required variants
- **WHEN** the settings panel loads available theme presets
- **THEN** it SHALL provide `Avalonia Default`, `Solarized Light`, `Solarized Dark`, `Gruvbox Light`, `Gruvbox Dark`, `Ayu Light`, `Ayu Mirage`, and `Ayu Dark`

#### Scenario: Avalonia Default is the reset target
- **WHEN** the user resets appearance settings to defaults
- **THEN** the selected theme preset SHALL become `Avalonia Default`

#### Scenario: Presets have stable identity and base variants
- **WHEN** the built-in preset catalog is inspected
- **THEN** every preset SHALL have a unique stable ASCII id that is independent of its localized display name
- **AND** every preset SHALL declare a light or dark base variant
- **AND** `Ayu Mirage` SHALL declare the dark base variant

### Requirement: Theme presets define semantic surface colors
Each built-in theme preset SHALL define semantic surface colors instead of legacy slot-oriented color names.

#### Scenario: Preset maps to semantic theme fields
- **WHEN** a built-in theme preset is resolved for application
- **THEN** it SHALL provide values for `WindowBackground`, `PanelBackground`, `ControlBackground`, `ControlForeground`, `MutedForeground`, `Accent`, `AccentForeground`, `Border`, `HoverBackground`, and `ActiveBackground`

#### Scenario: Semantic color pairs remain readable
- **WHEN** any built-in theme preset is validated
- **THEN** primary and muted foreground/background pairs and `AccentForeground` on `Accent` SHALL have a contrast ratio of at least 4.5:1
- **AND** borders and state indicators against adjacent surfaces SHALL have a contrast ratio of at least 3:1
- **AND** `HoverBackground` and `ActiveBackground` SHALL each remain visually distinct from `ControlBackground`

### Requirement: Theme presets preserve semantic colors outside cosmetic theming
The theme system SHALL leave semantic diagnostic colors outside the preset color map.

#### Scenario: Diagnostic colors are not replaced by presets
- **WHEN** a theme preset is applied
- **THEN** frame-accuracy colors, validation-error colors, and warning or destructive action colors SHALL remain controlled by their dedicated semantic styling rather than by the preset palette
- **AND** those dedicated styles SHALL remain readable on representative light and dark preset surfaces

### Requirement: Theme preset selection is preset-only
The system SHALL NOT expose manual theme color editing or a `Custom` preset state in the first preset-selection release.

#### Scenario: Settings exposes built-in presets only
- **WHEN** the appearance settings section is rendered
- **THEN** theme selection SHALL be limited to built-in presets
- **AND** the UI SHALL NOT expose manual color editors for semantic theme values

#### Scenario: Legacy color editing is not reachable
- **WHEN** the user navigates the main window, menus, tool windows, and Settings
- **THEN** no standalone legacy color-settings command or view SHALL be available

### Requirement: Theme preset persistence resolves safely
The system SHALL persist stable preset identity in the theme section of the unified versioned settings document and SHALL resolve unavailable selections to a deterministic default.

#### Scenario: Selected preset is persisted by id
- **WHEN** the user saves appearance settings
- **THEN** the unified theme section SHALL persist the selected stable preset id rather than semantic palette values or a localized display name

#### Scenario: Missing theme section uses the default
- **WHEN** the unified settings document or its theme section does not exist
- **THEN** the selected preset SHALL resolve to `Avalonia Default`

#### Scenario: Blank or unknown preset id uses the default
- **WHEN** the unified theme section contains a blank or unrecognized preset id
- **THEN** the selected preset SHALL resolve to `Avalonia Default`
- **AND** loading or live application alone SHALL NOT rewrite a current-version settings document

#### Scenario: Legacy theme colors remain intentionally ignored
- **WHEN** only the legacy `theme-colors.json` file exists
- **THEN** the selected preset SHALL resolve to `Avalonia Default`
- **AND** the legacy six-slot values SHALL NOT be migrated into the preset model

#### Scenario: Malformed unified settings are preserved
- **WHEN** active `settings.json` contains malformed JSON
- **THEN** the existing corrupt-settings preservation behavior SHALL be used
- **AND** the running application SHALL fall back to `Avalonia Default`
