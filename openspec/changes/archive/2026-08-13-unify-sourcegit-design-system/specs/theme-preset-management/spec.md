# theme-preset-management Delta

## MODIFIED Requirements

### Requirement: Theme presets populate imported theme tokens
Each built-in ChapterTool preset SHALL provide the color tokens that the imported style layer uses, and the imported token set SHALL contain only tokens that the style layer consumes.

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
- **THEN** dynamic imported theme and ChapterTool resources SHALL refresh
- **AND** the preset identifier and settings schema SHALL remain unchanged

#### Scenario: Unused imported tokens are absent
- **WHEN** the imported token definitions and the theme application service are audited
- **THEN** every defined `Color.*` and `Brush.*` token SHALL have at least one consumer in the style layer or the views
- **AND** the theme application service SHALL NOT write tokens that no consumer resolves
