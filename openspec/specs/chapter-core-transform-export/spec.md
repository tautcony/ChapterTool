# chapter-core-transform-export Specification

## Purpose
TBD - created by archiving change rewrite-avalonia-dotnet10. Update Purpose after archive.
## Requirements
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

### Requirement: Expression transforms
The system SHALL transform chapter times through Lua scripts using structured success or failure results.

#### Scenario: Lua script receives chapter time and fps
- **WHEN** a Lua expression script references `t` or `fps`
- **THEN** Core SHALL evaluate it using chapter time in seconds and the current frame rate

#### Scenario: Invalid expression does not crash
- **WHEN** Lua compilation, Lua execution, or Lua return conversion fails
- **THEN** Core SHALL return a failure diagnostic and preserve original chapter time behavior

#### Scenario: Lua execution is bounded
- **WHEN** a Lua expression or transform function does not complete within the execution budget
- **THEN** Core SHALL stop the script, return a structured timeout diagnostic, and preserve original chapter time behavior

#### Scenario: Postfix expressions are not a required expression target
- **WHEN** expression transform behavior is implemented for this change
- **THEN** Core SHALL NOT require postfix expression authoring or postfix token-list evaluation as part of the user-facing expression workflow

#### Scenario: Lua script receives chapter context
- **WHEN** Lua expression mode is applied to a non-separator chapter
- **THEN** Core SHALL provide the script with chapter time `t`, frame rate `fps`, one-based chapter `index`, non-separator chapter `count`, and a chapter context table
- **AND** the numeric Lua result SHALL become the chapter time in seconds

#### Scenario: Lua transform function is supported
- **WHEN** a Lua script defines `transform(chapter)` and that function returns a numeric value
- **THEN** Core SHALL call the function for each non-separator chapter and use its returned seconds value

#### Scenario: Lua expression shorthand is supported
- **WHEN** the user enters a simple Lua arithmetic expression such as `t + 1` without an explicit `return`
- **THEN** Core SHALL evaluate it as a returned Lua expression
- **AND** the numeric result SHALL become the transformed seconds value for the current chapter

#### Scenario: Lua direct return is supported
- **WHEN** a Lua script directly returns a numeric expression such as `return t + 1`
- **THEN** Core SHALL use that numeric return as the transformed seconds value for the current chapter

#### Scenario: Built-in Lua presets are available
- **WHEN** Lua expression presets are requested
- **THEN** Core SHALL expose stable preset identifiers, display names, descriptions, and script text for common transforms including identity, offset seconds, frame rounding, and half-frame earlier adjustment

#### Scenario: Lua invalid return preserves chapter time
- **WHEN** a Lua script returns nil, a string, a boolean, NaN, infinity, or another non-finite non-numeric result
- **THEN** Core SHALL report an `InvalidExpression.Lua*` diagnostic
- **AND** the original chapter time SHALL be preserved for that chapter

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
The system SHALL export TXT/OGM, Matroska XML, QPFile, TimeCodes, tsMuxeR meta, CUE, JSON, WebVTT, celltimes, and Chapter-to-QPFile through UI-independent exporter contracts.

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

#### Scenario: Frame-number exporters reject non-finite frame rates
- **WHEN** QPFile, celltimes, Chapter-to-QPFile, or expression projection needs frame-rate arithmetic and the selected frame rate is NaN or infinity
- **THEN** Core SHALL return a structured invalid-frame-rate diagnostic instead of throwing or producing invalid frame numbers

#### Scenario: JSON export handles MPLS source name
- **WHEN** JSON export runs for an MPLS source
- **THEN** `sourceName` SHALL be `{SourceName}.m2ts`

#### Scenario: WebVTT export preserves explicit chapter ends
- **WHEN** WebVTT export runs for chapters with explicit end timestamps
- **THEN** each cue SHALL use the chapter's explicit end timestamp
- **AND** chapters without an explicit end SHALL fall back to the next chapter start or the chapter set duration

### Requirement: Legacy-compatible rounding policies
The system SHALL use documented legacy compatibility rounding policies for chapter timestamp formatting and frame-number conversions.

#### Scenario: Half-millisecond timestamp rounds compatibly
- **WHEN** a timestamp is exactly on a half-millisecond formatting boundary
- **THEN** Core SHALL round it according to the documented Time_Shift compatibility policy for timestamp text

#### Scenario: Exporters share timestamp rounding policy
- **WHEN** TXT, XML, QPFile, TimeCodes, tsMuxeR meta, CUE, JSON, WebVTT, or celltimes output formats convert times to text or frames
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

### Requirement: Expression authoring metadata
The Core expression subsystem SHALL expose Lua expression globals, safe helper functions, script presets, snippets, and editor symbols as structured metadata for UI authoring features.

#### Scenario: Metadata exposes Lua tokens
- **WHEN** expression authoring metadata is requested
- **THEN** the result SHALL include Lua globals `t`, `fps`, `index`, `count`, and `chapter`
- **AND** it SHALL include supported safe math/helper functions, Lua keywords needed for transforms, and built-in Lua script presets under a discoverable `preset.*` namespace with stable identifiers

### Requirement: Expression authoring analysis
The Core expression subsystem SHALL analyze Lua expression script text for editor classification, completion, and validation without mutating chapter data.

#### Scenario: Analyze valid Lua expression shorthand
- **WHEN** the Lua expression text `t + math.floor(fps / 2)` is analyzed
- **THEN** the analysis SHALL classify Lua keyword, global, operator, function/member, punctuation, and number spans
- **AND** it SHALL report no diagnostics

#### Scenario: Analyze invalid Lua expression shorthand
- **WHEN** the Lua expression text `t +` is analyzed
- **THEN** the analysis SHALL include an `InvalidExpression.Lua*` diagnostic
- **AND** the diagnostic SHALL include a correction suggestion suitable for display to the user

#### Scenario: Completion uses Lua caret context
- **WHEN** completions are requested after a Lua prefix such as `math.flo`
- **THEN** the result SHALL include `math.floor` or `floor` according to the exposed helper style
- **AND** accepting the completion SHALL identify the replacement span for only the Lua prefix token

#### Scenario: Preset completion is discoverable
- **WHEN** completions are requested after `pre` or `preset.`
- **THEN** the result SHALL expose a `preset` namespace entry and concrete preset entries such as `preset.round-to-frame`
- **AND** accepting a concrete preset completion SHALL insert that preset script text rather than the display identifier

#### Scenario: Completion items expose categories
- **WHEN** expression completions are returned
- **THEN** each completion SHALL include a token kind/category suitable for the UI to visually distinguish variables, functions, keywords, and presets

### Requirement: Shared preview and save projection pipeline
The system SHALL use one Core output projection pipeline for preview and save so Lua expression transforms, diagnostics, numbering, and naming are consistent.

#### Scenario: Projection does not mutate source chapters
- **WHEN** preview or save requests projected output from a source chapter set
- **THEN** Core SHALL return projected chapter data without mutating the original `ChapterInfo` input

#### Scenario: Projection order is deterministic
- **WHEN** expression application, order shift, and name generation are all enabled
- **THEN** Core SHALL first apply Lua expression transforms to non-separator chapter times
- **AND** it SHALL normalize transformed times and refresh frame display state
- **AND** it SHALL then apply output numbering/order shift and output name generation

#### Scenario: Preview and save share diagnostics
- **WHEN** Lua expression projection emits diagnostics during preview or save
- **THEN** the projection result SHALL include those diagnostics in the same structured form for both callers
- **AND** chapters whose Lua expression failed SHALL retain their original times while the rest of the output projection continues

#### Scenario: Separators are structural during projection
- **WHEN** projected output contains separator rows
- **THEN** Core SHALL NOT execute Lua expressions for separator rows
- **AND** exported `OutputChapters` SHALL include only non-separator chapters after projection
