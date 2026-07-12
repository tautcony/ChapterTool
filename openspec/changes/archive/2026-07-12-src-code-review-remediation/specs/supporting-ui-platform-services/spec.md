## ADDED Requirements

### Requirement: Composition root shares long-lived core services
The application composition root SHALL reuse shared instances for long-lived pure services that have no per-window state—at minimum the chapter time formatter, expression engine (or authoring service wrapping it), chapter export service construction inputs, and external tool locator bound to the settings store—rather than silently constructing divergent instances on every factory call in production paths.

#### Scenario: Export and load paths share formatter and expression policy
- **WHEN** the GUI constructs load, save, and export services through the composition root
- **THEN** those services SHALL observe the same time-formatter and expression-engine instances (or equivalent single-policy factories) configured for that root
- **AND** production paths SHALL NOT create a second default expression engine only for save while the shell uses another for preview

#### Scenario: External tool locator is not recreated without need
- **WHEN** the composition root builds the importer registry and the settings window service that also resolve external tools
- **THEN** both paths SHALL use the same settings store and SHALL reuse a shared tool-locator instance for that root unless a documented lifetime reason requires otherwise

### Requirement: Production shell dependencies that the product always needs are required
Production construction of the main window ViewModel and settings tool SHALL supply non-null shell and settings-store services when those capabilities are always present in the shipping application. Optional nullability MAY remain only for deliberately partial test doubles, not as the normal product contract.

#### Scenario: Production main window receives shell and settings store
- **WHEN** `AppCompositionRoot` creates the production `MainWindowViewModel`
- **THEN** it SHALL pass non-null `IShellService` and `ISettingsStore<ChapterToolSettings>` instances
- **AND** production “open related media” and settings persistence paths SHALL NOT permanently depend on null-checks that encode missing product capabilities

#### Scenario: Tests still substitute fakes
- **WHEN** unit or Headless tests construct the main ViewModel or settings tool
- **THEN** they SHALL still be able to inject fake shell, settings, picker, and window services
