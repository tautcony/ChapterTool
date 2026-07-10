## MODIFIED Requirements

### Requirement: Import formats are routed through the importer registry
The Avalonia shell SHALL route source loading through the load service and importer registry rather than constructing importers from ViewModel or window extension switches.

#### Scenario: New source format is added
- **WHEN** support for a new chapter source format is introduced
- **THEN** runtime selection SHALL be added through an `IChapterImporterRegistry` implementation or registered importer path
- **AND** `MainWindowViewModel` SHALL continue to call the load service without branching on the file extension

#### Scenario: Import fallback diagnostics are logged structurally
- **WHEN** the primary importer cannot be invoked and a fallback importer is used
- **THEN** the load pipeline SHALL return or log a structured diagnostic identifying the primary importer, fallback importer, source path context, and reason for fallback

#### Scenario: Output defaults are editable
- **WHEN** the settings panel is opened
- **THEN** it SHALL expose default save format and default XML chapter language rather than the current working values being edited on the main screen

#### Scenario: Appearance settings are editable
- **WHEN** the settings panel is opened
- **THEN** it SHALL expose a preset-first theme selector rather than the legacy six-slot color editor
- **AND** the available built-in presets SHALL include `Avalonia Default` and the documented `Solarized`, `Gruvbox`, and `Ayu` families
- **AND** it SHALL NOT expose manual theme color editors in the first preset-selection release

#### Scenario: High-frequency main workflow controls stay on the main screen
- **WHEN** the settings panel is opened
- **THEN** high-frequency current working controls such as naming mode, template use, order shift, expression, frame-rate choice, and round-frames SHALL remain on the main workflow surface instead of being duplicated as settings

#### Scenario: Platform integration is gated
- **WHEN** file association or another platform-specific integration is shown in settings
- **THEN** it SHALL be hidden, disabled, or clearly marked unsupported when the current platform cannot perform it

### Requirement: Settings changes apply predictably
The settings panel SHALL save, apply, reset, validate, and discard changes in a way that keeps the main ViewModel, visible runtime state, and persisted settings consistent.

#### Scenario: Runtime-safe settings apply immediately
- **WHEN** the user changes a runtime-safe setting in the settings panel
- **THEN** the running shell SHALL update language, default save directory, save defaults, frame accuracy tolerance, and selected appearance preset without waiting for Save
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
- **AND** appearance reset SHALL select the `Avalonia Default` theme preset

#### Scenario: Invalid settings are surfaced
- **WHEN** a setting value is invalid or an external tool path cannot be resolved
- **THEN** the settings panel SHALL show a localized validation message and SHALL NOT silently discard the user's input

## ADDED Requirements

### Requirement: Settings appearance presets use semantic surface coverage
The Avalonia settings panel SHALL define theme appearance through semantic surface coverage rather than through implementation-oriented legacy slot names.

#### Scenario: Semantic theme fields describe UI responsibilities
- **WHEN** the selected theme preset is applied
- **THEN** the resolved theme tokens SHALL map to `WindowBackground`, `PanelBackground`, `ControlBackground`, `ControlForeground`, `MutedForeground`, `Accent`, `AccentForeground`, `Border`, `HoverBackground`, and `ActiveBackground`
- **AND** each field SHALL control a predictable group of shell, tool-window, input, border, and interaction surfaces

#### Scenario: Preset base variant follows the selected palette
- **WHEN** a light or dark theme preset is applied
- **THEN** the application's Avalonia theme variant SHALL switch to the preset's declared light or dark base variant
- **AND** Fluent controls, DataGrid, popups, and editor chrome SHALL use the same base variant as the semantic palette

#### Scenario: DataGrid column headers follow the selected preset
- **WHEN** a theme preset is applied while the chapter grid is visible
- **THEN** every `DataGridColumnHeader` SHALL use semantic background, foreground, border, hover, and pressed colors from the selected preset
- **AND** header text and sort glyphs SHALL remain readable against the header background
- **AND** switching between representative light and dark presets SHALL update existing column headers without reopening the window
- **AND** no column header SHALL retain a stale Fluent default or previously selected preset brush

### Requirement: Settings theme preset selection stays simple
The Avalonia settings panel SHALL keep theme preset selection in a single simple selector rather than splitting family and variant into separate controls.

#### Scenario: Preset selector lists variants directly
- **WHEN** the appearance settings section is rendered
- **THEN** the preset selector SHALL list each built-in theme variant directly as a selectable option
- **AND** the user SHALL NOT be required to choose family and variant from separate selectors

#### Scenario: Palette preview follows selection
- **WHEN** the user selects a different preset
- **THEN** a compact, non-editable palette preview SHALL update to represent that preset's semantic colors
- **AND** the preview SHALL expose an accessible name derived from the localized preset name

#### Scenario: Preset names follow runtime language changes
- **WHEN** the UI language changes while the settings window is open
- **THEN** preset display names and appearance labels SHALL refresh from Simplified Chinese, English, or Japanese resources
- **AND** the stable selected preset id SHALL remain unchanged
