## ADDED Requirements

### Requirement: Frame accuracy is visual state
The Avalonia shell SHALL render frame accuracy as visual styling rather than as `K` or `*` characters in frame text.

#### Scenario: Accurate rounded frames glow green
- **WHEN** a chapter row has rounded frame display and the frame calculation error is within tolerance
- **THEN** the frame cell SHALL show only the numeric frame text
- **AND** the frame text SHALL use a green outer glow treatment
- **AND** the glow SHALL be visually centered around the text rather than offset down or right
- **AND** the glow SHALL use a softened radius large enough to read as glow rather than hard outline

#### Scenario: Inexact rounded frames glow red
- **WHEN** a chapter row has rounded frame display and the frame calculation error exceeds tolerance
- **THEN** the frame cell SHALL show only the numeric frame text
- **AND** the frame text SHALL use a red outer glow treatment
- **AND** the glow SHALL be visually centered around the text rather than offset down or right
- **AND** the glow SHALL use a softened radius large enough to read as glow rather than hard outline

#### Scenario: Unrounded frames are neutral
- **WHEN** frame rounding is disabled
- **THEN** the frame cell SHALL show the unrounded numeric frame text
- **AND** the frame text SHALL render with neutral black styling rather than green or red accuracy styling

#### Scenario: Frame edits use numeric text
- **WHEN** a user edits the frame cell
- **THEN** the committed value SHALL be interpreted as numeric frame text without requiring or preserving `K` or `*` suffixes

#### Scenario: Frame accuracy tolerance is configurable
- **WHEN** the user opens Settings
- **THEN** the settings panel SHALL expose frame accuracy tolerance as a continuous slider from `0.01` through `0.30`
- **AND** the slider SHALL show recommended tick marks at each `0.05` value
- **AND** values within `0.01` of a recommended tick SHALL snap to that recommended value
- **AND** the current tolerance value SHALL be displayed adjacent to the slider
- **AND** saving settings SHALL persist that tolerance for future frame accuracy classification

#### Scenario: Frame accuracy tolerance has a recommended default
- **WHEN** settings have no frame accuracy tolerance or reset to defaults
- **THEN** the shell SHALL use `0.15` as the default tolerance value

#### Scenario: Invalid frame accuracy tolerance is normalized
- **WHEN** settings contain a non-positive or excessive frame accuracy tolerance
- **THEN** the shell SHALL normalize it to the supported `0.01` through `0.30` range before applying frame accuracy classification
