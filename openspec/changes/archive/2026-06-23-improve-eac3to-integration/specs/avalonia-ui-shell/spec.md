## MODIFIED Requirements

### Requirement: Main window load progress
The main window SHALL present bounded progress during source loading when the load pipeline reports intermediate progress.

#### Scenario: Importer reports intermediate progress
- **WHEN** a load operation reports progress before returning its import result
- **THEN** the main-window view model SHALL update the progress value to a bounded intermediate value
- **AND** completion or failure handling SHALL remain responsible for the final progress state
