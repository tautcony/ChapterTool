## ADDED Requirements

### Requirement: CLI construction reuses shared composition factories
CLI inspect and convert workflows SHALL obtain importer-registry and export-service construction through the shared application composition factories or an equivalent shared factory surface, rather than permanently maintaining a fully independent private wiring path.

#### Scenario: CLI inspect uses shared importer factory rules
- **WHEN** the user runs `inspect` without injecting a test registry
- **THEN** the CLI SHALL construct its importer registry through the shared composition factory rules used by the GUI load path
- **AND** importer fallback behavior SHALL remain available under those same construction rules

#### Scenario: CLI convert uses shared export factory rules
- **WHEN** the user runs `convert` without injecting a test exporter
- **THEN** the CLI SHALL construct its exporter through the shared composition export factory path
- **AND** expression transforms SHALL remain disabled for CLI convert as already required by the CLI conversion surface

#### Scenario: Tests can still inject CLI dependencies
- **WHEN** CLI unit tests provide an importer registry, exporter, console, or settings store
- **THEN** the CLI application SHALL use those injected dependencies instead of the shared factories
