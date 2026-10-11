## MODIFIED Requirements

### Requirement: Auto frame-rate detection in the UI
The Avalonia main window SHALL expose Auto first in the frame-rate selector and SHALL surface detection feedback when Auto is active. Detection SHALL be a presentation suggestion. It SHALL NOT assign source authority or authorize unknown/mixed-rate content calculations.

#### Scenario: Auto entry appears in the frame-rate selector
- **WHEN** the main window renders
- **THEN** the frame-rate ComboBox SHALL include Auto first and preserve the existing catalog row order
- **AND** a non-catalog current rate SHALL have an additional stable custom entry

#### Scenario: Auto detection updates status text
- **WHEN** the user picks Auto and the chapter set has at least one non-separator chapter
- **THEN** the ViewModel SHALL report the detected display name and confidence through localized status text
- **AND** source/current metadata SHALL remain unchanged

#### Scenario: Auto remains selected after detection
- **WHEN** Auto detection completes
- **THEN** selection SHALL stay on the Auto row
- **AND** later refreshes SHALL NOT turn the suggestion into a committed rate

#### Scenario: Manual frame-rate choice does not emit detection status
- **WHEN** the user picks a valid non-Auto frame-rate row
- **THEN** the ViewModel SHALL use its canonical value as the explicit computation choice
- **AND** it SHALL NOT replace that choice with numeric lookup or detection
- **AND** selection alone SHALL NOT create content history

#### Scenario: Unknown or mixed rate requires explicit choice
- **WHEN** Auto is active on unknown or mixed-rate content
- **THEN** suggested frame display SHALL remain available with uncertainty feedback
- **AND** frame edits, shifts, FPS conversion, and constant-FPS export SHALL require an explicit computation rate
- **AND** time-only editing and serialization SHALL remain available

## ADDED Requirements

### Requirement: Frame-rate selection preserves canonical identity
Selection and presentation SHALL preserve rate kind and payload through load, reselection, track changes, language changes, and settings refresh. Custom rates SHALL NOT be replaced by nearby catalog rates.

#### Scenario: Load approximate catalog-like input
- **WHEN** a document carries Approximate 23.976
- **THEN** its selector/display SHALL retain that classification
- **AND** no initial selection SHALL silently assign Rational 24000/1001

#### Scenario: Load non-catalog rational input
- **WHEN** a document carries a valid rational outside the catalog
- **THEN** the selector SHALL retain a stable entry for that rate
- **AND** calculation SHALL remain rational

#### Scenario: Language changes on existing rows
- **WHEN** the user switches among Chinese, English, and Japanese
- **THEN** selected labels and existing rows SHALL refresh without changing canonical rate or ticks
- **AND** normal, editor, and preview frame values SHALL remain readable as one number

#### Scenario: Editor closes without a value change
- **WHEN** a user enters and leaves a rounded frame editor without changing its value
- **THEN** the original chapter ticks and history position SHALL remain unchanged

#### Scenario: Active preview survives display refresh
- **WHEN** settings refresh frame presentation while a valid candidate is pending
- **THEN** the candidate SHALL retain its reviewed ticks and computation rate
- **AND** the refresh SHALL NOT apply or replay the candidate
