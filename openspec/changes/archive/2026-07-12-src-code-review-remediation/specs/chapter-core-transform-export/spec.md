## MODIFIED Requirements

### Requirement: Expression transforms
The system SHALL transform chapter times through Lua scripts using structured success or failure results. The Lua evaluation host SHALL open only math, string, and table standard libraries (plus explicit safe host helpers such as `round`/`sign` aliases) and SHALL NOT expose host I/O, OS, or package-loading libraries to expression scripts.

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

#### Scenario: Lua host does not expose OS or package libraries
- **WHEN** a Lua expression attempts to call host OS, I/O, or package-loading APIs that are not part of the documented safe expression surface
- **THEN** evaluation SHALL fail with a Lua compile/runtime diagnostic rather than performing host I/O
- **AND** successful evaluations SHALL continue to allow documented math/string/table helpers and chapter globals
