## MODIFIED Requirements

### Requirement: UI-independent chapter core
The system SHALL represent chapters, chapter sets, and source groups without depending on Avalonia, WinForms, or UI control state.

#### Scenario: Chapter fields are preserved
- **WHEN** a chapter is edited, transformed, or exported through Core services
- **THEN** `Number`, `Time`, `Name`, and numeric `FramesInfo` SHALL be available from Core models without UI tags or control references
- **AND** `FramesInfo` SHALL NOT include frame accuracy marker suffixes such as `K` or `*`

#### Scenario: Source groups are selectable by structure
- **WHEN** multiple chapter sets are imported from one source
- **THEN** Core SHALL expose structured options for clip, title, edition, and source metadata without requiring display-text parsing

### Requirement: Time and frame conversion
The system SHALL provide deterministic time parsing, formatting, frame-rate selection, frame accuracy, and frame display behavior compatible with documented Time_Shift behavior.

#### Scenario: Invalid time text is lenient
- **WHEN** Core parses empty or malformed chapter time text
- **THEN** it SHALL return `TimeSpan.Zero` with a diagnostic when the caller requests diagnostics

#### Scenario: Frame-rate options are explicit
- **WHEN** a caller selects a frame rate
- **THEN** selection SHALL use a `FrameRateOption` value with stable code, display name, numeric value, and validity instead of ComboBox indexes

#### Scenario: Rounded frame display reports accuracy separately
- **WHEN** rounded frame display is enabled
- **THEN** Core SHALL round frame values with the compatibility policy
- **AND** Core SHALL expose separate frame accuracy state indicating whether the calculation error is within tolerance
- **AND** Core SHALL store or expose the frame text as the numeric rounded frame value without appending `K` or `*`

#### Scenario: Frame accuracy uses caller-provided tolerance
- **WHEN** Core updates frame display with a valid positive frame accuracy tolerance
- **THEN** Core SHALL classify rounded frame accuracy using that tolerance

#### Scenario: Unrounded frame display is neutral
- **WHEN** rounded frame display is disabled
- **THEN** Core SHALL expose the unrounded numeric frame text
- **AND** Core SHALL expose neutral frame accuracy state rather than accurate or inexact state

#### Scenario: Frame-rate detection prefers smallest cumulative deviation
- **WHEN** two or more valid frame-rate options have the same number of accurate chapters within tolerance
- **THEN** Core SHALL select the option whose cumulative `|frames - round(frames)|` (clamped per chapter at the tolerance value) is smallest, instead of relying on iteration order

#### Scenario: Frame-rate detection reports confidence
- **WHEN** a caller invokes `DetectDetailed(info, tolerance)`
- **THEN** Core SHALL return a `FrameRateDetectionResult` containing the selected `FrameRateOption`, the count of within-tolerance chapters, the count of evaluated non-separator chapters, the cumulative clamped deviation, and a `FrameRateConfidence` of `High`, `Medium`, or `Low`

#### Scenario: Empty chapter set has low confidence
- **WHEN** `DetectDetailed` is called on a chapter set with zero non-separator chapters
- **THEN** Core SHALL return the default `Fps23976` option with `FrameRateConfidence.Low` and an evaluated chapter count of zero

### Requirement: Export formats
The system SHALL export TXT/OGM, Matroska XML, QPFile, TimeCodes, tsMuxeR meta, CUE, and JSON through UI-independent exporter contracts.

#### Scenario: TXT export writes OGM pairs
- **WHEN** TXT export runs
- **THEN** output SHALL contain `CHAPTERNN=hh:mm:ss.sss` and `CHAPTERNNNAME=name` pairs for non-separator chapters

#### Scenario: XML export applies language fallback
- **WHEN** XML export runs with no selected language
- **THEN** `ChapterLanguage` SHALL be `und`

#### Scenario: QPFile export has no BOM
- **WHEN** QPFile export writes bytes
- **THEN** the encoding SHALL be UTF-8 without BOM

#### Scenario: QPFile export calculates frames from time and frame rate
- **WHEN** QPFile export runs for chapters whose frame display text is missing, stale, or marked with UI accuracy state
- **THEN** output SHALL contain integer frame `I` entries calculated from chapter time and selected frame rate
- **AND** output SHALL NOT copy frame display text or accuracy markers

#### Scenario: Frame-number exporters apply export rounding
- **WHEN** QPFile or celltimes output is generated
- **THEN** each exported frame number SHALL use the configured compatibility rounding policy for integer-frame output

#### Scenario: JSON export handles MPLS source name
- **WHEN** JSON export runs for an MPLS source
- **THEN** `sourceName` SHALL be `{SourceName}.m2ts`

### Requirement: Legacy-compatible rounding policies
The system SHALL use documented legacy compatibility rounding policies for chapter timestamp formatting and frame-number conversions.

#### Scenario: Half-millisecond timestamp rounds compatibly
- **WHEN** a timestamp is exactly on a half-millisecond formatting boundary
- **THEN** Core SHALL round it according to the documented Time_Shift compatibility policy for timestamp text

#### Scenario: Exporters share timestamp rounding policy
- **WHEN** TXT, XML, QPFile, TimeCodes, tsMuxeR meta, CUE, JSON, WebVTT, or celltimes output formats convert times to text or frames
- **THEN** equivalent timestamp-text outputs SHALL use the same Core timestamp rounding policy and equivalent frame-number outputs SHALL use the documented Core frame rounding policy
