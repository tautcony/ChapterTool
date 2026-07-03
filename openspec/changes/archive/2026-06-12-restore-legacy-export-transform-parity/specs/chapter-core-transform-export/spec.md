## ADDED Requirements

### Requirement: Legacy-compatible rounding policies
The system SHALL use documented legacy compatibility rounding policies for chapter timestamp formatting and frame-number conversions.

#### Scenario: Half-millisecond timestamp rounds compatibly
- **WHEN** a timestamp is exactly on a half-millisecond formatting boundary
- **THEN** Core SHALL round it according to the documented Time_Shift compatibility policy for timestamp text

#### Scenario: Exporters share timestamp rounding policy
- **WHEN** TXT, XML, QPF, TimeCodes, tsMuxeR meta, CUE, JSON, WebVTT, celltimes, or Chapter2Qpfile output formats convert times to text or frames
- **THEN** equivalent timestamp-text outputs SHALL use the same Core timestamp rounding policy and equivalent frame-number outputs SHALL use the documented Core frame rounding policy

### Requirement: Legacy-compatible ChangeFps transform
The system SHALL expose a ChangeFps transform that recalculates chapter times and durations by preserving frame positions from a source frame rate to a target frame rate.

#### Scenario: ChangeFps preserves chapter frame numbers
- **WHEN** a chapter at source FPS maps to frame `N`
- **THEN** ChangeFps SHALL set the transformed chapter time to `N / targetFps`

#### Scenario: ChangeFps preserves frame durations
- **WHEN** a chapter has an end time or duration that maps to a source frame span
- **THEN** ChangeFps SHALL preserve that frame span and recalculate the transformed end or duration at the target FPS

#### Scenario: Invalid ChangeFps input fails structurally
- **WHEN** source FPS or target FPS is invalid or zero
- **THEN** ChangeFps SHALL return a structured failure diagnostic and SHALL NOT mutate the chapter set

### Requirement: Legacy-compatible Matroska XML export
The system SHALL export Matroska chapter XML in a legacy-compatible structured format for user-facing XML saves.

#### Scenario: XML export includes document preamble
- **WHEN** XML export runs
- **THEN** output SHALL include an XML declaration and the documented legacy-compatible Matroska chapters comment or doctype guidance before the `Chapters` document body

#### Scenario: XML export is formatted
- **WHEN** XML export runs
- **THEN** output SHALL be indented and line-broken consistently rather than emitted as a single-line document

#### Scenario: XML export uses non-trivial UIDs
- **WHEN** XML export creates `EditionUID` or `ChapterUID` values
- **THEN** generated UID values SHALL be valid positive Matroska UID values and SHALL NOT default every edition to `1` or every chapter UID to only the chapter number unless an explicit compatibility option requests deterministic IDs

#### Scenario: XML export preserves selected language
- **WHEN** XML export runs with a selected ISO chapter language code
- **THEN** each `ChapterLanguage` SHALL contain that code exactly when the code is valid
