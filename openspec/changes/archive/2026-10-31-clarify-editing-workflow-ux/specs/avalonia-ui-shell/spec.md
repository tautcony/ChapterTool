## ADDED Requirements

### Requirement: Frame controls distinguish display from content conversion
Desktop and Web frame controls MUST identify integer-frame display as a display preference. The chapter frame-rate conversion button MUST show only its icon, with its action name available through a tooltip and accessible name. Conversion review MUST show source rate, target rate, and whether chapter times change in the action context. Desktop MUST show conversion review in the conversion button tooltip while a conversion candidate is pending. Web MUST show review in the conversion dialog. Frame accuracy guidance MUST appear only on hover over the integer-frame control. Do not add a separate information icon or persistent explanatory text. Display preference changes MUST preserve committed ticks, history, and any current candidate.

#### Scenario: Change integer-frame display
- **WHEN** the user toggles integer-frame display with a candidate active
- **THEN** only visible formatting MUST change and the candidate MUST remain applicable

#### Scenario: Review a frame-rate conversion
- **WHEN** the user requests chapter frame-rate conversion
- **THEN** the source and target rates and the time effect MUST be available before Apply in the action context
- **AND** unknown or approximate source rates MUST retain their provenance

#### Scenario: Find frame accuracy guidance
- **WHEN** the user hovers the integer-frame control
- **THEN** concise frame accuracy guidance MUST appear
- **AND** no separate information icon or persistent explanation MUST occupy the controls area

#### Scenario: Identify the conversion action
- **WHEN** the user sees the chapter frame-rate conversion control
- **THEN** the control MUST show an icon without a visible text label
- **AND** its action name MUST remain available on hover and to assistive technology

#### Scenario: Switch UI language
- **WHEN** the user switches between Chinese, English, and Japanese
- **THEN** frame labels, conversion review, selected labels, and existing rows MUST update without committing content

### Requirement: Narrow chapter tables preserve readable content
Desktop and Web chapter tables MUST allocate useful width to chapter names in narrow layouts. Time values MUST stay on one line. Frame decimals MUST read as one number. Normal cells, editors, and candidate comparisons MUST remain usable at supported narrow sizes. Desktop resizing MUST preserve user-adjusted column widths. The four main workflow zones MUST remain available.

#### Scenario: Resize a chapter table
- **WHEN** a loaded table changes between default, wide, and narrow supported sizes
- **THEN** names, times, and frame values MUST remain readable through adaptive widths or an explicit column visibility control
- **AND** user-adjusted desktop widths MUST NOT be reset by unrelated size changes

#### Scenario: Review candidate values in a narrow table
- **WHEN** a candidate is pending in a narrow table
- **THEN** changed values MUST retain before/after identity without clipping time or splitting frame decimals
- **AND** unchanged values MUST NOT require a second comparison line

#### Scenario: Align bottom expression actions
- **WHEN** the window shows the expression editor and its available actions
- **THEN** the actions MUST form a compact group aligned with the editor
- **AND** hidden actions MUST NOT leave trailing gaps that shift the visible buttons

#### Scenario: Align chapter table headers
- **WHEN** the desktop chapter table is visible
- **THEN** each column header MUST align its text horizontally to the center of its column

### Requirement: Workflow names identify their effects
Desktop and Web controls MUST use distinct localized terms for source reload, frame-display recalculation, candidate refresh, and export-text preview. The terms MUST remain consistent in visible labels, tooltips, accessible names, and shortcut settings.

#### Scenario: Identify refresh and preview actions
- **WHEN** a user inspects the available actions in any supported language
- **THEN** each name MUST identify whether it reloads source content, refreshes display, refreshes a candidate, or displays export text
