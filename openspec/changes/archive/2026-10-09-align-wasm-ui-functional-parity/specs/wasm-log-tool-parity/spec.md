## ADDED Requirements

### Requirement: Browser logs open as a bounded list-first surface

The Web log tool MUST open without automatic selection or an inspector. It MUST retain the existing bounded browser log source. Rows MUST provide a concise summary, local time, and localized severity text. Live append MUST not steal focus or open details. Entries MUST have stable identity for selection and inspection.

#### Scenario: Open populated logs and append an entry
- **WHEN** the user opens logs and a new retained entry arrives
- **THEN** the list updates without selecting the entry or opening an inspector
- **AND** keyboard focus remains at its current target

### Requirement: Search and severity filter the browser projection

The Web log tool MUST provide case-insensitive text search and severity filtering. Search MUST include message, details, and any recorded identity or structured fields. Filtering MUST preserve the retained source and show a visible count. It MUST not invent data absent from browser entries.

#### Scenario: Search hidden details
- **WHEN** the query occurs only in a retained entry's details
- **THEN** the entry remains visible with a localized indication of the detail match
- **AND** opening details exposes that text

#### Scenario: Change or clear filters
- **WHEN** the user changes severity or clears search
- **THEN** visible membership and count update without altering retained entries
- **AND** stale search highlights disappear

### Requirement: Browser inspection requires an explicit action

A row details action, Enter, or Space MUST open the inspector for that entry. Row selection alone MUST not open it. The inspector MUST show populated browser data and secondary raw/copy actions. Closing inspection MUST preserve filters and surviving selection. Filter exclusion or retention eviction MUST close an unavailable inspector without selecting a replacement.

#### Scenario: Select and inspect an entry
- **WHEN** the user selects a row body
- **THEN** the list remains the only data surface
- **WHEN** the user activates its details action
- **THEN** the inspector displays that entry's recorded data

#### Scenario: Entry leaves visible membership
- **WHEN** filtering or bounded retention removes the inspected entry from visible membership
- **THEN** the inspector closes or presents an explicit unavailable state
- **AND** no different entry is selected automatically

#### Scenario: Return from details
- **WHEN** the user closes details or invokes its Escape action
- **THEN** the list retains its filters, scroll state, and surviving selection
- **AND** focus returns to the row or its details action

### Requirement: Browser log copy and clear preserve defined state

The tool MUST provide explicit copy for selected entry data and explicit clear for retained logs. Copy MUST not change retained entries or filters. Copy failure MUST be recoverable. Clear MUST empty the retained source and resolve selection and inspector state without stale data.

#### Scenario: Copy details
- **WHEN** the user copies the inspected entry
- **THEN** the clipboard request contains the selected recorded data
- **AND** the list, filters, and inspector remain usable after success or failure

#### Scenario: Clear retained entries
- **WHEN** the user activates Clear
- **THEN** retained logs, visible membership, selection, and inspector data are cleared
- **AND** a localized empty state is visible

### Requirement: Log export captures the filtered membership

The Web log tool MUST expose JSON and CSV through a secondary export surface. Export MUST capture current filtered membership and serialize it in deterministic timestamp order. Output MUST use UTF-8 and preserve Unicode, quotes, delimiters, and multiline details. The existing browser download adapter MUST deliver the result. Export MUST not change chapter export baselines, log retention, filters, selection, or inspector state.

#### Scenario: Export filtered Unicode entries
- **WHEN** the user filters logs and exports JSON or CSV containing Unicode and multiline details
- **THEN** output contains exactly the captured visible entries in deterministic order
- **AND** JSON parses and CSV round-trips its quoted fields correctly
- **AND** no invisible retained entry is included

#### Scenario: Export fails or a new entry arrives
- **WHEN** download fails or logs change after the export snapshot was captured
- **THEN** the tool reports the applicable outcome without changing the captured membership
- **AND** filters, surviving selection, inspector state, and retained entries remain usable

### Requirement: Browser log presentation is responsive and localized

Wide layouts MUST place a bounded inspector beside the list. Narrow layouts MUST show the inspector with a back action when both surfaces cannot fit. Search, severity, count, details, close/back, copy, clear, and export MUST have localized labels and accessible keyboard targets. Meaning MUST not depend on color alone.

#### Scenario: Inspect at narrow and short sizes
- **WHEN** the user opens details at supported narrow or short sizes
- **THEN** details and back/close actions remain reachable without horizontal clipping
- **AND** returning restores the filtered list

#### Scenario: Change locale while inspecting
- **WHEN** the UI language changes with a surviving entry inspected
- **THEN** controls and accessible labels update from shared locale resources
- **AND** the selected entry and recorded data remain stable
