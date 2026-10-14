# log-export-archive Specification

## Purpose
TBD - created by archiving change log-tool-enhancements. Update Purpose after archive.
## Requirements
### Requirement: Export is a secondary action

The LogTool MUST expose an explicit export action from a labeled secondary
overflow surface. JSON and CSV choices must be available within that surface.
The primary list and search toolbar must remain usable without opening export.

#### Scenario: Export choices stay out of the primary toolbar

- **WHEN** the log tool opens with no menu open
- **THEN** search, count, and the severity filter remain visible while export
  format choices are hidden in the secondary action surface

#### Scenario: Export a selected format

- **WHEN** the user chooses JSON or CSV and activates export
- **THEN** the host exporter receives that format and a snapshot of the current
  visible log membership

### Requirement: Export preserves the visible state

Export MUST use the current severity-filtered and search-filtered membership by
default. It must not change selection, inspector visibility, filter values, or
the provider's bounded history.

#### Scenario: Export after filtering

- **WHEN** the user applies a severity filter or search query and exports
- **THEN** the output request contains only entries visible under those filters
  in deterministic timestamp order

#### Scenario: Export succeeds or fails

- **WHEN** the exporter returns success or a recoverable failure
- **THEN** the list, selected row, inspector state, and filter values remain
  unchanged and a compact localized status is shown

### Requirement: Export failures are recoverable

The export boundary MUST convert expected path, permission, encoding, and file
write failures into a failure result. The UI command must remain usable after a
failure and must not clear retained entries.

#### Scenario: Export target cannot be written

- **WHEN** the host cannot create or write the export target
- **THEN** the command completes without an uncaught file-system exception,
  shows localized failure feedback, and leaves the log view usable

### Requirement: Existing automatic archives remain unchanged

The desktop logging composition MUST keep its existing rolling Serilog files
below `settings/logs/`. This change must not add a second archive workflow or
change retention settings.

#### Scenario: Desktop logging starts

- **WHEN** the desktop composition starts with a settings directory
- **THEN** the existing log directory and rolling sink behavior remain intact

