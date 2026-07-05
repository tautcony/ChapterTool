## ADDED Requirements

### Requirement: Expression authoring metadata
The Core expression subsystem SHALL expose supported variables, constants, functions, and operators as structured metadata for UI authoring features.

#### Scenario: Metadata exposes supported tokens
- **WHEN** expression authoring metadata is requested
- **THEN** the result SHALL include variables `t` and `fps`, the supported mathematical constants, supported functions with arity, and supported operators with display text

### Requirement: Expression authoring analysis
The Core expression subsystem SHALL analyze expression text for editor classification, completion, and validation without mutating chapter data.

#### Scenario: Analyze valid expression
- **WHEN** the expression `t + floor(fps / 2)` is analyzed
- **THEN** the analysis SHALL classify variable, operator, function, punctuation, and number spans
- **AND** it SHALL report no diagnostics

#### Scenario: Analyze invalid expression
- **WHEN** the expression `t +` is analyzed
- **THEN** the analysis SHALL include an `InvalidExpression.*` diagnostic
- **AND** the diagnostic SHALL include a correction suggestion suitable for display to the user

#### Scenario: Completion uses caret context
- **WHEN** completions are requested after the prefix `flo`
- **THEN** the result SHALL include `floor`
- **AND** accepting the completion SHALL identify the replacement span for only the prefix token
