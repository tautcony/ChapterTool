## ADDED Requirements

### Requirement: Built-in theme preset catalog
The system SHALL provide a built-in theme preset catalog containing an Avalonia baseline preset and curated `Solarized`, `Gruvbox`, and `Ayu` families.

#### Scenario: Preset catalog includes required variants
- **WHEN** the settings panel loads available theme presets
- **THEN** it SHALL provide `Avalonia Default`, `Solarized Light`, `Solarized Dark`, `Gruvbox Light`, `Gruvbox Dark`, `Ayu Light`, `Ayu Mirage`, and `Ayu Dark`

#### Scenario: Avalonia Default is the reset target
- **WHEN** the user resets appearance settings to defaults
- **THEN** the selected theme preset SHALL become `Avalonia Default`

### Requirement: Theme presets define semantic surface colors
Each built-in theme preset SHALL define semantic surface colors instead of legacy slot-oriented color names.

#### Scenario: Preset maps to semantic theme fields
- **WHEN** a built-in theme preset is resolved for application
- **THEN** it SHALL provide values for `WindowBackground`, `PanelBackground`, `ControlBackground`, `ControlForeground`, `MutedForeground`, `Accent`, `AccentForeground`, `Border`, `HoverBackground`, and `ActiveBackground`

### Requirement: Theme presets preserve semantic colors outside cosmetic theming
The theme system SHALL leave semantic diagnostic colors outside the preset color map.

#### Scenario: Diagnostic colors are not replaced by presets
- **WHEN** a theme preset is applied
- **THEN** frame-accuracy colors, validation-error colors, and warning or destructive action colors SHALL remain controlled by their dedicated semantic styling rather than by the preset palette

### Requirement: Theme preset selection is preset-only
The system SHALL NOT expose manual theme color editing or a `Custom` preset state in the first preset-selection release.

#### Scenario: Settings exposes built-in presets only
- **WHEN** the appearance settings section is rendered
- **THEN** theme selection SHALL be limited to built-in presets
- **AND** the UI SHALL NOT expose manual color editors for semantic theme values
