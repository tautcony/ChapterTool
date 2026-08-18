## MODIFIED Requirements

### Requirement: Secondary tools consume narrow workspace ports
Secondary tool ViewModels SHALL depend on narrow workspace or shell ports for the capabilities they need, not on the full main-window ViewModel type for unrelated session fields. The main-window ViewModel and session facade SHALL NOT expose a second concrete adapter access path for those ports.

#### Scenario: Expression tool uses an expression port
- **WHEN** the expression tool is constructed
- **THEN** it SHALL depend on an expression/session port that can read and apply expression script state and diagnostics formatting
- **AND** it SHALL NOT require access to unrelated main-window commands such as clip combine or zones

#### Scenario: Settings live-apply uses a preference sink
- **WHEN** the settings tool applies runtime-safe preferences live
- **THEN** it SHALL call a preference-sink/workspace API that applies language, save directory, output defaults, frame tolerance, and related session preferences
- **AND** it SHALL NOT need the entire main-window command surface to do so

#### Scenario: Preview format selector uses an export-format port
- **WHEN** the preview tool changes output format for preview rendering
- **THEN** it SHALL update export format through a narrow export-preference port
- **AND** preview content SHALL still match save projection rules for the same preferences

#### Scenario: Concrete adapter access is not a second session route
- **WHEN** secondary tools or tests obtain workspace ports
- **THEN** they SHALL obtain them from `IWorkspaceToolSession`
- **AND** `MainWindowViewModel` and `MainWindowToolSession` SHALL NOT expose `MainWindowPortAdapters` as a public or compatibility property
