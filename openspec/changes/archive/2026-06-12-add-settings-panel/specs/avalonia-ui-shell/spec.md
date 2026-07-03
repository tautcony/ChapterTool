## ADDED Requirements

### Requirement: Unified settings command
The Avalonia shell SHALL expose a unified Settings command that opens a settings panel for durable application, external tool, appearance, and platform preferences.

#### Scenario: Settings command exists
- **WHEN** the main window ViewModel is constructed
- **THEN** it SHALL expose a Settings command that can be invoked by the shell and tested without creating platform UI directly

#### Scenario: Settings panel opens as a secondary tool window
- **WHEN** the user invokes Settings
- **THEN** the window service SHALL show a dedicated settings view and ViewModel instead of building the settings UI imperatively inside the window service

#### Scenario: Settings entry stays compact
- **WHEN** the main window is rendered
- **THEN** the Settings entry SHALL be reachable from a compact command surface without adding a large preferences section to the primary chapter workflow

### Requirement: Settings panel groups durable configurable features
The settings panel SHALL organize the durable configurable features discovered from the current app into general, external tools, output defaults, appearance, and platform integration groups.

#### Scenario: General settings are editable
- **WHEN** the settings panel is opened
- **THEN** it SHALL expose UI language, default save directory, and main window location reset controls

#### Scenario: External tool settings are editable
- **WHEN** the settings panel is opened
- **THEN** it SHALL expose MKVToolNix/mkvextract path, eac3to path, ffprobe path, and ffmpeg directory fallback controls with browse, clear, and validation status behavior

#### Scenario: Output defaults are editable
- **WHEN** the settings panel is opened
- **THEN** it SHALL expose default save format and default XML chapter language rather than the current working values being edited on the main screen

#### Scenario: Appearance settings are editable
- **WHEN** the settings panel is opened
- **THEN** it SHALL expose the six theme color slots in their legacy order

#### Scenario: High-frequency main workflow controls stay on the main screen
- **WHEN** the settings panel is opened
- **THEN** high-frequency current working controls such as naming mode, template use, order shift, expression, frame-rate choice, and round-frames SHALL remain on the main workflow surface instead of being duplicated as settings

#### Scenario: Platform integration is gated
- **WHEN** file association or another platform-specific integration is shown in settings
- **THEN** it SHALL be hidden, disabled, or clearly marked unsupported when the current platform cannot perform it

### Requirement: Settings changes apply predictably
The settings panel SHALL save, apply, reset, and validate changes in a way that keeps the main ViewModel and persisted settings consistent.

#### Scenario: Save applies runtime-safe settings
- **WHEN** the user saves settings that can be applied at runtime
- **THEN** the running shell SHALL update language, save defaults, and appearance state without requiring a restart

#### Scenario: Reset restores defaults
- **WHEN** the user resets a settings group to defaults
- **THEN** the panel SHALL restore the same defaults used by a fresh application start

#### Scenario: Invalid settings are surfaced
- **WHEN** a setting value is invalid or an external tool path cannot be resolved
- **THEN** the settings panel SHALL show a localized validation message and SHALL NOT silently discard the user's input
