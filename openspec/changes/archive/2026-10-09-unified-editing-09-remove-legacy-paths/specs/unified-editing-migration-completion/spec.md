## ADDED Requirements

### Requirement: Interactive content has one authoritative edit path
After migration, every interactive content command SHALL commit to the current document session. No host table, preview, or export path SHALL read a competing editable chapter set or persistent transform projection.

#### Scenario: User edits then previews and exports
- **WHEN** a user edits a chapter or applies a tool operation
- **THEN** the table, preview, and each supported export SHALL read the same committed document content

#### Scenario: Repository migration is reviewed
- **WHEN** the migration is declared complete
- **THEN** every prior content mutation entry point SHALL be listed with its session transaction and behavior test
- **AND** no required content command SHALL remain disabled as a migration workaround

### Requirement: History verification includes branch, failure, and resource behavior
The system SHALL verify full-state identity across undo/redo and branch navigation, injected recoverable failures, deep-history traversal, and retained history without automatic truncation.

#### Scenario: Source files disappear during a live session
- **WHEN** the imported source is removed after loading
- **THEN** undo, redo, and branch switching SHALL use in-memory history and SHALL remain available

#### Scenario: Large history is measured
- **WHEN** the performance suite exercises increasing depth, branch count, and edit size
- **THEN** it SHALL report memory, allocation, undo, and distant-branch navigation data for the recorded environment
- **AND** the system SHALL NOT prune history to pass the measurement

### Requirement: Migration documentation and verification match shipped behavior
Current code maps and OpenSpec requirements SHALL identify the unified session owner and primary tests. Completion reporting SHALL list executed validation commands, performance evidence, screenshots when UI layout changed, and any unavailable checks.

#### Scenario: Maintainer follows the code map
- **WHEN** a maintainer looks up editing, session, host, export, or test ownership
- **THEN** the current code map SHALL point to the unified document, transaction, history, and host integration entry points
