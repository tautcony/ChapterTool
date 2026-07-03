## MODIFIED Requirements

### Requirement: Time and frame conversion
The system SHALL provide deterministic time parsing, formatting, frame-rate selection, and frame display behavior compatible with documented Time_Shift behavior.

#### Scenario: Invalid time text is lenient
- **WHEN** Core parses empty or malformed chapter time text
- **THEN** it SHALL return `TimeSpan.Zero` with a diagnostic when the caller requests diagnostics

#### Scenario: Frame-rate options are explicit
- **WHEN** a caller selects a frame rate
- **THEN** selection SHALL use a `FrameRateOption` value with stable code, display name, numeric value, and validity instead of ComboBox indexes

#### Scenario: Rounded frame display is marked
- **WHEN** rounded frame display is enabled
- **THEN** Core SHALL round frames with the compatibility policy and mark values within tolerance as `K`, otherwise `*`

#### Scenario: Frame-rate detection prefers smallest cumulative deviation
- **WHEN** two or more valid frame-rate options have the same number of accurate chapters within tolerance
- **THEN** Core SHALL select the option whose cumulative `|frames - round(frames)|` (clamped per chapter at the tolerance value) is smallest, instead of relying on iteration order

#### Scenario: Frame-rate detection reports confidence
- **WHEN** a caller invokes `DetectDetailed(info, tolerance)`
- **THEN** Core SHALL return a `FrameRateDetectionResult` containing the selected `FrameRateOption`, the count of within-tolerance chapters, the count of evaluated non-separator chapters, the cumulative clamped deviation, and a `FrameRateConfidence` of `High`, `Medium`, or `Low`

#### Scenario: Empty chapter set has low confidence
- **WHEN** `DetectDetailed` is called on a chapter set with zero non-separator chapters
- **THEN** Core SHALL return the default `Fps23976` option with `FrameRateConfidence.Low` and an evaluated chapter count of zero
