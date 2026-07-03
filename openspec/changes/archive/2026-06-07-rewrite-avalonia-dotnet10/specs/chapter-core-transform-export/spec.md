## ADDED Requirements

### Requirement: UI-independent chapter core
The system SHALL represent chapters, chapter sets, and source groups without depending on Avalonia, WinForms, or UI control state.

#### Scenario: Chapter fields are preserved
- **WHEN** a chapter is edited, transformed, or exported through Core services
- **THEN** `Number`, `Time`, `Name`, and `FramesInfo` SHALL be available from Core models without UI tags or control references

#### Scenario: Source groups are selectable by structure
- **WHEN** multiple chapter sets are imported from one source
- **THEN** Core SHALL expose structured options for clip, title, edition, and source metadata without requiring display-text parsing

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

### Requirement: Expression transforms
The system SHALL transform chapter times through supported infix and postfix expressions using structured success or failure results.

#### Scenario: Expression uses chapter time and fps
- **WHEN** an expression references `t` or `fps`
- **THEN** Core SHALL evaluate it using chapter time in seconds and the current frame rate

#### Scenario: Invalid expression does not crash
- **WHEN** expression parsing or evaluation fails
- **THEN** Core SHALL return a failure diagnostic and preserve original chapter time behavior

#### Scenario: Unsupported legacy operators are explicit
- **WHEN** `and`, `or`, `xor`, or other incomplete legacy operators are encountered
- **THEN** the behavior SHALL be either tested as supported or reported as unsupported; it SHALL NOT be silently documented as complete

### Requirement: Chapter editing operations
The system SHALL expose chapter editing as Core operations callable by ViewModels.

#### Scenario: Edit chapter frame
- **WHEN** a chapter frame cell is edited and the current frame rate is valid
- **THEN** Core SHALL convert the frame value to time using `frame / fps` and refresh derived frame display

#### Scenario: Delete first chapter
- **WHEN** the first chapter is deleted and chapters remain
- **THEN** Core SHALL shift remaining chapter times so the new first chapter starts at zero

#### Scenario: Insert chapter
- **WHEN** exactly one chapter is selected for insertion
- **THEN** Core SHALL insert a chapter named `New Chapter` before it and renumber the chapter list

### Requirement: Export formats
The system SHALL export TXT/OGM, Matroska XML, QPF, TimeCodes, tsMuxeR meta, CUE, and JSON through UI-independent exporter contracts.

#### Scenario: TXT export writes OGM pairs
- **WHEN** TXT export runs
- **THEN** output SHALL contain `CHAPTERNN=hh:mm:ss.sss` and `CHAPTERNNNAME=name` pairs for non-separator chapters

#### Scenario: XML export applies language fallback
- **WHEN** XML export runs with no selected language
- **THEN** `ChapterLanguage` SHALL be `und`

#### Scenario: QPF export has no BOM
- **WHEN** QPF export writes bytes
- **THEN** the encoding SHALL be UTF-8 without BOM

#### Scenario: JSON export handles MPLS source name
- **WHEN** JSON export runs for an MPLS source
- **THEN** `sourceName` SHALL be `{SourceName}.m2ts`

