## MODIFIED Requirements

### Requirement: Expression editor authoring experience
The Avalonia shell SHALL use a dedicated Lua expression/script editor for all expression inputs instead of a plain text box or the previous custom formula grammar editor, while allowing simple arithmetic expressions without requiring an explicit `return`.

#### Scenario: Main expression input uses the Lua expression editor
- **WHEN** the main window is rendered
- **THEN** the expression input SHALL provide Lua syntax highlighting for expression script tokens
- **AND** it SHALL expose Lua-aware completion candidates based on the caret token, including provided globals, safe helper functions, and discoverable `preset.*` entries

#### Scenario: Expression tool uses the same Lua editor
- **WHEN** the expression tool window is opened
- **THEN** its expression input SHALL use the same Lua editor behavior as the main window

#### Scenario: Completion items are visually categorized
- **WHEN** the Lua expression completion popup is open
- **THEN** completion items SHALL visually identify whether they are variables, functions, keywords, or presets using category labels and distinct colors

#### Scenario: Tab accepts Lua completion
- **WHEN** a completion popup is open for a Lua prefix
- **THEN** pressing `Tab` SHALL insert the selected completion
- **AND** focus SHALL remain in the expression editor

#### Scenario: Lua syntax errors show correction guidance
- **WHEN** the user enters an invalid Lua expression script
- **THEN** the editor SHALL display the specific Lua syntax or validation problem
- **AND** it SHALL display a correction suggestion derived from the diagnostic

#### Scenario: Valid Lua script clears errors
- **WHEN** the user corrects an invalid Lua expression script to a valid script
- **THEN** the error and suggestion feedback SHALL be cleared

## ADDED Requirements

### Requirement: Lua expression script authoring
The Avalonia shell SHALL allow users to apply Lua expression transforms with built-in presets and external script selection.

#### Scenario: Expression tool exposes Lua script editing
- **WHEN** the expression tool window is opened
- **THEN** it SHALL present Lua script editing as the expression authoring surface
- **AND** it SHALL NOT require users to choose or understand the previous formula/postfix grammar

#### Scenario: Built-in Lua preset can populate script text
- **WHEN** the user selects a built-in Lua script preset in the expression tool or accepts a `preset.*` completion in the editor
- **THEN** the tool SHALL show or insert the preset script text and apply that script through the owner ViewModel when the user applies the tool

#### Scenario: External Lua script can be selected
- **WHEN** the user chooses an external `.lua` script from the main expression input or expression tool
- **THEN** the UI SHALL expose the load action as a button at the right side of the Lua expression input
- **AND** it SHALL use the file picker service abstraction to select and read the script text
- **AND** applying or using the loaded script SHALL pass that script text into chapter preview and save options without requiring Core to read the file path

#### Scenario: Lua expression or script participates in preview and save
- **WHEN** expression application is enabled and the current chapters are previewed or saved
- **THEN** the main ViewModel SHALL project chapter output using Lua expression/script options
- **AND** Lua diagnostics SHALL be surfaced through the same status/log diagnostic path as expression diagnostics

#### Scenario: Simple arithmetic remains approachable through Lua
- **WHEN** the user enters a simple Lua arithmetic expression such as `t + 1`
- **THEN** preview and save SHALL apply the transform without requiring `return`, a function wrapper, or any legacy postfix expression syntax

### Requirement: Preview and save use shared Lua projection
The Avalonia shell SHALL route preview, text preview, and save through the same Lua expression projection options exposed by the main ViewModel.

#### Scenario: Main ViewModel builds one projection option set
- **WHEN** the main ViewModel previews or saves chapters with expression application enabled
- **THEN** it SHALL pass the same Lua expression/script text, preset/source metadata, order shift, naming, format, and language options into the Core projection/export path

#### Scenario: Save does not re-read external script files
- **WHEN** a user has loaded an external Lua script in the expression tool and later saves chapters
- **THEN** the ViewModel SHALL pass the already-loaded script text to Core
- **AND** save SHALL NOT require Core or the save service to re-read the external script path

#### Scenario: Preview matches save projection
- **WHEN** the user previews projected chapters and then saves without changing options
- **THEN** the times, generated numbers, generated names, and Lua diagnostics used for preview SHALL match the data used for save output
