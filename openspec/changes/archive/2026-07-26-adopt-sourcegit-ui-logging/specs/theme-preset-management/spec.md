## ADDED Requirements

### Requirement: Theme presets populate SourceGit theme tokens
Each built-in ChapterTool preset SHALL provide the SourceGit color tokens that the imported style layer uses.

#### Scenario: Light preset is applied
- **WHEN** the user applies a light preset
- **THEN** all `Color.*` and `Brush.*` resources required by the imported layer SHALL resolve
- **AND** control foregrounds SHALL remain readable

#### Scenario: Dark preset is applied
- **WHEN** the user applies a dark preset
- **THEN** all `Color.*` and `Brush.*` resources required by the imported layer SHALL resolve
- **AND** information, warning, and error states SHALL remain distinct

#### Scenario: Open window receives a theme change
- **WHEN** the user changes a preset while a window is open
- **THEN** dynamic SourceGit and ChapterTool resources SHALL refresh
- **AND** the preset identifier and settings schema SHALL remain unchanged
