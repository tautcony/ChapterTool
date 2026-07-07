## MODIFIED Requirements

### Requirement: Expression transforms
The system SHALL transform chapter times through Lua scripts using structured success or failure results.

#### Scenario: Lua script receives chapter time and fps
- **WHEN** a Lua expression script references `t` or `fps`
- **THEN** Core SHALL evaluate it using chapter time in seconds and the current frame rate

#### Scenario: Invalid expression does not crash
- **WHEN** Lua compilation, Lua execution, or Lua return conversion fails
- **THEN** Core SHALL return a failure diagnostic and preserve original chapter time behavior

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

## ADDED Requirements

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
