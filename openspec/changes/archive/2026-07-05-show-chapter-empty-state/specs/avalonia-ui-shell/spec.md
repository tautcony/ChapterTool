## ADDED Requirements

### Requirement: Chapter grid empty-state visual
The Avalonia main window SHALL render the existing chapter empty SVG as a centered visual state when the chapter table has no rows.

#### Scenario: Empty chapter grid shows SVG
- **WHEN** the main window is rendered before any chapter source has been loaded
- **THEN** the chapter grid area SHALL contain a visible centered image loaded from `Assets/Images/chapter-empty.svg`
- **AND** the chapter grid SHALL remain present as the command and layout surface

#### Scenario: Loaded chapters hide SVG
- **WHEN** a chapter source is loaded and chapter rows are displayed
- **THEN** the empty-state SVG SHALL be hidden
- **AND** the chapter rows SHALL remain displayed in the grid
