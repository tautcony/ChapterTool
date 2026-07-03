## ADDED Requirements

### Requirement: Typed settings cover editable application preferences
The settings system SHALL persist all settings exposed by the unified settings panel through typed cross-platform stores while preserving legacy migration behavior.

#### Scenario: Existing settings still load
- **WHEN** existing `appsettings.json` files or migrated `chaptertool.json` files omit newly added settings fields
- **THEN** the settings store SHALL load successfully and use defaults matching the current application startup behavior

#### Scenario: Workflow defaults persist
- **WHEN** the user saves default save format or default XML language from the settings panel
- **THEN** those defaults SHALL be written to typed settings and applied when a new main window ViewModel loads settings

#### Scenario: Theme colors remain compatible
- **WHEN** appearance settings are saved from the settings panel
- **THEN** the six theme color slots SHALL continue using the existing theme settings store and legacy color slot order

### Requirement: External tool settings are editable and verifiable
The application SHALL allow users to configure, clear, and verify external tool paths used by current import workflows.

#### Scenario: Configured paths preserve locator precedence
- **WHEN** MKVToolNix/mkvextract, eac3to, ffprobe, or ffmpeg path settings are saved
- **THEN** the external tool locator SHALL use those configured values before environment or platform discovery according to the existing precedence rules

#### Scenario: Cleared paths restore discovery
- **WHEN** a configured external tool path is cleared in settings
- **THEN** the external tool locator SHALL fall back to environment and platform discovery rather than retaining the old override

#### Scenario: Directory values resolve executable names
- **WHEN** a configured tool setting points to a directory supported by the locator
- **THEN** validation and runtime lookup SHALL resolve the platform-appropriate executable name from that directory

#### Scenario: Tool validation returns structured status
- **WHEN** the settings panel tests an external tool setting
- **THEN** the validation result SHALL indicate found, missing, invalid path, or unsupported status without showing a Core-layer UI dialog

### Requirement: Settings panel services are injectable
Settings-related UI behavior SHALL use injectable services for settings stores, file and directory picking, localization, tool location, platform support, and shell operations.

#### Scenario: Settings ViewModel is testable
- **WHEN** tests construct the settings panel ViewModel
- **THEN** settings stores, picker behavior, tool locator, localizer, and platform capability checks SHALL be replaceable with fakes

#### Scenario: Browsing uses platform abstractions
- **WHEN** the user browses for a save directory or external tool path
- **THEN** the settings panel SHALL use Avalonia/platform file picker abstractions rather than direct WinForms or registry APIs

#### Scenario: Settings status is localizable
- **WHEN** settings validation, save, reset, or unsupported-platform feedback is displayed
- **THEN** visible messages SHALL be formatted through the active localization resources
