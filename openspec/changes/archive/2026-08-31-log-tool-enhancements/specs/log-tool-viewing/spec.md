## ADDED Requirements

### Requirement: The log tool opens as a list-first surface

The LogTool MUST open with no selected entry and no inspector. The retained log
list must occupy the available content width until the user requests details.

#### Scenario: Open an empty or populated log tool

- **WHEN** the log tool is created with zero or more retained entries
- **THEN** the inspector is hidden, no entry is selected automatically, and the
  list or localized empty state is visible

#### Scenario: A live entry arrives

- **WHEN** a new accepted entry arrives while the tool is open
- **THEN** the list updates without selecting the new entry, moving keyboard
  focus, or opening the inspector

### Requirement: Rows provide concise summaries

Each retained entry MUST render as a stable compact row with a severity marker,
localized severity accessible text, local time, and a truncated summary. Optional
operation, category, or event identity may appear as muted context when it adds
information. Empty values must not reserve space.

#### Scenario: Routine entry is rendered

- **WHEN** an entry has a summary and timestamp but no extra identity
- **THEN** the row shows one readable summary line, a severity marker, and the
  local time without empty badges or columns

#### Scenario: Diagnostic identity is useful

- **WHEN** an entry has category, operation, or event identity that is not in
  the summary
- **THEN** the row exposes that identity as compact secondary context and the
  inspector remains closed until explicitly requested

### Requirement: Details require an explicit action

Each row MUST expose an accessible details action. Selecting a row alone MUST
not open the inspector. Activating the details action, Enter, or Space must
select the entry and open the inspector.

#### Scenario: Select without inspecting

- **WHEN** the user clicks a row body
- **THEN** the row becomes selected and the list remains the only visible data
  surface

#### Scenario: Open and close the inspector

- **WHEN** the user activates the selected row details action
- **THEN** the inspector displays that entry without removing the list on a
  wide window
- **WHEN** the user closes the inspector or presses Escape
- **THEN** the list remains at its current filter and scroll state, the selected
  row remains selected, and focus returns to the row or its details action

### Requirement: The inspector uses progressive disclosure

The inspector MUST show a compact context line and only non-empty message,
technical, property, exception, or structured sections. It must not show a
large repeated summary header or permanently visible empty tabs.

#### Scenario: Sparse entry is inspected

- **WHEN** the selected entry has no technical, property, exception, or nested
  structured data
- **THEN** the inspector shows compact context and one localized empty state
  without blank tab bodies

#### Scenario: Technical entry is inspected

- **WHEN** the selected entry has technical detail, an exception, or structured
  values
- **THEN** the corresponding populated sections are available, while raw data
  and copy actions remain secondary

### Requirement: Structured values remain expandable

Nested dictionaries and enumerable values MUST render as recursive nodes. A node
with children must expose an accessible disclosure control bound two-way to its
mutable expansion state. The projection must enforce a finite depth limit.

#### Scenario: Expand a nested value

- **WHEN** the selected entry contains nested structured state
- **THEN** the inspector shows a disclosure control and preserves the user's
  expansion choice while that entry remains selected

#### Scenario: Depth limit is reached

- **WHEN** structured data exceeds the supported nesting depth
- **THEN** the terminal node shows a stable depth-limit marker and the view
  remains responsive

### Requirement: Search and severity filtering stay primary

The LogTool MUST provide case-insensitive search and severity filtering without
changing retained provider history. Search must cover the summary, category,
operation, event identity, technical detail, exception text, and structured/raw
values. The filter surface must label its controls and expose an active state.

#### Scenario: Search finds hidden diagnostic text

- **WHEN** the query occurs only in exception text or structured state
- **THEN** the entry remains visible and the row exposes a localized indication
  or highlight that the match is in technical data

#### Scenario: Search highlights a summary

- **WHEN** the query occurs in the visible summary
- **THEN** the matching segment is rendered with the log highlight style and
  clearing the query removes stale highlights

#### Scenario: Severity filter changes membership

- **WHEN** the user selects a severity filter
- **THEN** only matching retained entries are shown, the count updates, and no
  replacement entry is selected automatically

### Requirement: Wide and narrow layouts remain usable

The inspector MUST use a bounded width beside a star-sized list when the tool
is wide. When the list and inspector minimum widths cannot coexist, the
inspector must replace the list with a localized back action. Hidden columns,
fixed-width overflow, and clipped text are not valid responsive behavior.

#### Scenario: Inspector opens at the wide size

- **WHEN** details are opened in a wide tool window
- **THEN** the list remains visible, the inspector is bounded, and both surfaces
  have independent scrolling where needed

#### Scenario: Inspector opens at the narrow size

- **WHEN** details are opened at the supported narrow width
- **THEN** the inspector fits within the window, exposes a back or close action,
  and no control or text is clipped horizontally

### Requirement: Localization and accessibility are preserved

User-facing labels, empty states, status text, tooltips, and automation names
MUST resolve from the shared locale resources. Icon-only actions MUST have
accessible names. Keyboard navigation MUST reach the row details action, the
inspector close/back action, structured disclosures, and the filter surface.
#### Scenario: Locale changes while the tool is open

- **WHEN** the active UI locale changes
- **THEN** list labels, inspector labels, filter labels, and accessibility names
  refresh without losing the selected entry or expansion state

#### Scenario: Keyboard inspection workflow

- **WHEN** a keyboard user moves to a row and presses Enter or Space
- **THEN** the selected row opens its inspector and Escape returns focus to the
  list
