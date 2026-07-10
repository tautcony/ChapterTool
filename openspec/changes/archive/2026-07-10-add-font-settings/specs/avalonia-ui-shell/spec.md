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
- **AND** it SHALL expose independent UI and monospace font-family selectors without exposing font-size controls

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
- **THEN** the running shell SHALL update language, default save directory, save defaults, frame accuracy tolerance, selected appearance preset, selected UI font, and selected monospace font without waiting for Save
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
- **THEN** the shell SHALL restore the last loaded or saved settings to the running main ViewModel and appearance services
- **AND** the settings window SHALL be allowed to close
- **AND** the typed settings stores SHALL remain unchanged

#### Scenario: Reset restores defaults
- **WHEN** the user resets a settings group to defaults
- **THEN** the panel SHALL restore the same defaults used by a fresh application start
- **AND** reset values SHALL apply immediately to the running shell while still requiring Save for persistence
- **AND** appearance reset SHALL select the `Avalonia Default` theme preset, system UI font default, and system monospace font default

#### Scenario: Invalid settings are surfaced
- **WHEN** a setting value is invalid or an external tool path cannot be resolved
- **THEN** the settings panel SHALL show a localized validation message and SHALL NOT silently discard the user's input

## ADDED Requirements

### Requirement: Settings exposes two simple font selectors
The Avalonia settings panel SHALL present UI and monospace font-family choices as two independent selectors in the Appearance section.

#### Scenario: Font selectors list defaults and installed families
- **WHEN** the Appearance section is rendered
- **THEN** each font selector SHALL show its localized default option followed by the available installed family names
- **AND** choosing an option in one selector SHALL NOT change the other selector

#### Scenario: Installed font options render progressively in their own family
- **WHEN** installed font options are realized in either selector's visible viewport
- **THEN** each installed family name SHALL render using that family
- **AND** off-screen font-specific option rendering SHALL remain deferred until scrolling realizes those items

#### Scenario: Font previews follow selection
- **WHEN** the user changes either font selection
- **THEN** a stable-size non-editable preview for that category SHALL update using the effective selected family
- **AND** the preview SHALL contain representative Latin letters, digits, punctuation, and localized text
- **AND** the preview SHALL expose a localized accessible name identifying its category and effective selection

#### Scenario: Font labels follow runtime language changes
- **WHEN** the UI language changes while the settings window is open
- **THEN** font labels, default option labels, preview text, and accessible names SHALL refresh from Simplified Chinese, English, or Japanese resources
- **AND** installed font options SHALL prefer a family name matching the active culture, then another name for the same language, then the canonical family name
- **AND** localized font metadata SHALL remain lazily resolved for realized options rather than eagerly loading every font
- **AND** the canonical selected family names SHALL remain unchanged

#### Scenario: Unavailable saved fonts appear as defaults
- **WHEN** Settings loads a saved family that is unavailable in the current catalog
- **THEN** the affected selector and preview SHALL show that category's effective default
- **AND** the settings panel SHALL NOT become dirty solely because fallback resolution occurred

### Requirement: Avalonia surfaces consume semantic font resources
The Avalonia shell SHALL apply UI and monospace typography through separate semantic dynamic resources.

#### Scenario: UI font covers normal application surfaces
- **WHEN** a UI font is applied
- **THEN** normal windows, controls, menus, table headers, dialogs, settings content, and tool surfaces SHALL use the semantic UI font resource
- **AND** existing visible surfaces SHALL refresh without reopening their windows

#### Scenario: Monospace font covers fixed-width content
- **WHEN** a monospace font is applied
- **THEN** expression and script editors, chapter-table data cells, text previews, logs, and other content intentionally using fixed-width alignment SHALL use the semantic monospace font resource
- **AND** existing visible editors and text surfaces SHALL refresh without reconstruction

#### Scenario: Chapter table separates cell and header typography
- **WHEN** different effective UI and monospace fonts are applied while the chapter table is visible
- **THEN** displayed and editing data cells SHALL use the semantic monospace font resource
- **AND** column headers SHALL continue using the semantic UI font resource
- **AND** existing cells and headers SHALL refresh without reopening or rebuilding the table

#### Scenario: Chapter-number shift uses monospace numeric entry
- **WHEN** the chapter-number shift control is visible and a monospace font is applied
- **THEN** its numeric input and displayed value SHALL use the semantic monospace font resource
- **AND** its descriptive label SHALL continue using the semantic UI font resource

#### Scenario: Font categories remain visually independent
- **WHEN** different effective families are selected for UI and monospace content
- **THEN** normal UI text SHALL resolve the UI family
- **AND** fixed-width content SHALL resolve the monospace family rather than inheriting the UI family

#### Scenario: Icon glyphs retain their icon family
- **WHEN** either semantic font resource changes
- **THEN** icon-library controls SHALL retain the font family required to render their glyphs
- **AND** settings and main-workflow command icons SHALL remain visible

#### Scenario: Newly opened surfaces use current fonts
- **WHEN** a window, popup, editor, preview, or log surface opens after font settings were applied
- **THEN** it SHALL resolve the current semantic UI or monospace resource according to its content role

### Requirement: Startup font application is resilient
The Avalonia application SHALL establish usable font resources before the main window is created and then apply persisted choices without blocking startup.

#### Scenario: Defaults exist before asynchronous load completes
- **WHEN** application composition starts before font settings have loaded
- **THEN** both semantic font resources SHALL contain their defaults
- **AND** the main window SHALL be able to render immediately

#### Scenario: Persisted settings replace startup defaults
- **WHEN** valid persisted font settings finish loading
- **THEN** the application SHALL apply both effective selections to the semantic resources
- **AND** already-created surfaces SHALL refresh through dynamic resource resolution

#### Scenario: Font settings load failure keeps the shell usable
- **WHEN** loading font settings fails because of malformed data, I/O, or access errors
- **THEN** the application SHALL retain both defaults
- **AND** the main window SHALL continue opening and remain usable
