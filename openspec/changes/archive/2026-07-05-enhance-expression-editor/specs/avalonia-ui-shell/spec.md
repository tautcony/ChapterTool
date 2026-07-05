## ADDED Requirements

### Requirement: Expression editor authoring experience
The Avalonia shell SHALL use a dedicated expression editor for all expression inputs instead of a plain text box.

#### Scenario: Main expression input uses the expression editor
- **WHEN** the main window is rendered
- **THEN** the expression input SHALL provide syntax highlighting for expression tokens
- **AND** it SHALL expose context-aware completion candidates based on the caret token

#### Scenario: Expression tool uses the same editor
- **WHEN** the expression tool window is opened
- **THEN** its expression input SHALL use the same editor behavior as the main window

#### Scenario: Tab accepts completion
- **WHEN** a completion popup is open for an expression prefix
- **THEN** pressing `Tab` SHALL insert the selected completion
- **AND** focus SHALL remain in the expression editor

#### Scenario: Syntax errors show correction guidance
- **WHEN** the user enters an invalid expression
- **THEN** the editor SHALL display the specific syntax problem
- **AND** it SHALL display a correction suggestion derived from the diagnostic

#### Scenario: Valid expression clears errors
- **WHEN** the user corrects an invalid expression to a valid expression
- **THEN** the error and suggestion feedback SHALL be cleared
