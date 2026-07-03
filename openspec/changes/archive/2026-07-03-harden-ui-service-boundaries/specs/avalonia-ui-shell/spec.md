## ADDED Requirements

### Requirement: Async load updates observable UI state safely
The Avalonia shell SHALL keep file IO and parsing separate from observable UI state mutation during asynchronous loads.

#### Scenario: Import work completes asynchronously
- **WHEN** a load service or importer completes asynchronously after performing file IO or parsing
- **THEN** the main-window ViewModel SHALL update `Rows`, `ClipOptions`, selected clip state, status, and progress through its command flow
- **AND** background import code SHALL NOT mutate UI-bound `ObservableCollection` instances directly

#### Scenario: Load progress does not bypass ViewModel state
- **WHEN** an importer reports intermediate progress during a load
- **THEN** progress updates SHALL be surfaced through ViewModel state or command-owned progress handling
- **AND** the view SHALL NOT rely on importer callbacks to update controls directly

### Requirement: Main window ViewModel stays independent of Avalonia windows
The main-window ViewModel SHALL remain independent of Avalonia `Window`, storage-provider, and control instances.

#### Scenario: File picking remains view or service responsibility
- **WHEN** a user browses for a source file, MPLS file, chapter-name template, or save directory
- **THEN** the selection SHALL be performed by a file-picker service or view adapter
- **AND** the ViewModel SHALL receive the selected path or cancellation result without holding an Avalonia `Window`

#### Scenario: Routine state does not require manual window refresh
- **WHEN** clip selection, combine state, save availability, selected rows, frame options, naming options, expression options, or current chapter data changes
- **THEN** the ViewModel SHALL raise observable property or command-availability notifications sufficient for bound controls to update
- **AND** the window SHALL NOT need to run a routine manual refresh method to synchronize that state

### Requirement: Import formats are routed through the importer registry
The Avalonia shell SHALL route source loading through the load service and importer registry rather than constructing importers from ViewModel or window extension switches.

#### Scenario: New source format is added
- **WHEN** support for a new chapter source format is introduced
- **THEN** runtime selection SHALL be added through an `IChapterImporterRegistry` implementation or registered importer path
- **AND** `MainWindowViewModel` SHALL continue to call the load service without branching on the file extension

#### Scenario: Import fallback diagnostics are logged structurally
- **WHEN** the primary importer cannot be invoked and a fallback importer is used
- **THEN** the load pipeline SHALL return or log a structured diagnostic identifying the primary importer, fallback importer, source path context, and reason for fallback
