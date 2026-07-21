## MODIFIED Requirements

### Requirement: CLI convert performs basic file conversion
The system SHALL provide a CLI command that imports a chapter source, selects one chapter option, and exports it in a supported output format without launching the GUI. The command SHALL accept optional expression text or a built-in expression preset and SHALL apply the selected expression before formatting the output.

#### Scenario: Convert writes a target file
- **WHEN** the user runs `convert <input> --format <format>` with a file output target or default output path
- **THEN** the CLI SHALL import the requested chapter option
- **AND** it SHALL export using the existing Core/Infrastructure conversion services
- **AND** it SHALL write the output content to the resolved file path

#### Scenario: Convert writes to stdout
- **WHEN** the user runs `convert <input> --format <format> --stdout`
- **THEN** the CLI SHALL write only the exported chapter content to stdout
- **AND** it SHALL not require an output file path

#### Scenario: Convert supports basic stable export options
- **WHEN** the user runs `convert` with stable options such as XML language, CUE source file name, or explicit group/option selection
- **THEN** the CLI SHALL map those values into export/import selection behavior without requiring GUI-only state

#### Scenario: Convert applies an inline expression
- **WHEN** the user runs `convert` with `--expression <lua-source>`
- **THEN** the CLI SHALL apply the Lua source to each non-separator chapter through the Core expression projection
- **AND** every supported output format SHALL use the transformed chapter times

#### Scenario: Convert applies a built-in expression preset
- **WHEN** the user runs `convert` with `--expression-preset <preset-id>` for an engine-provided preset
- **THEN** the CLI SHALL resolve the preset from the shared expression engine
- **AND** it SHALL apply the preset source through the Core expression projection

#### Scenario: Convert preserves the basic default
- **WHEN** the user runs `convert` without `--expression` and without `--expression-preset`
- **THEN** the CLI SHALL leave expression projection disabled
- **AND** the output SHALL preserve the existing non-expression conversion behavior

#### Scenario: Convert rejects conflicting expression sources
- **WHEN** the user provides both `--expression` and `--expression-preset`
- **THEN** the CLI SHALL print a validation error to stderr
- **AND** it SHALL exit with code `1` without importing the source

#### Scenario: Convert rejects an unknown preset
- **WHEN** the user provides an expression preset identifier that the shared expression engine does not expose
- **THEN** the CLI SHALL print a validation error to stderr
- **AND** it SHALL exit with code `1` without exporting content

### Requirement: CLI construction reuses shared composition factories
CLI inspect and convert workflows SHALL obtain importer-registry and export-service construction through the shared application composition factories or an equivalent shared factory surface, rather than permanently maintaining a fully independent private wiring path. The default CLI export service SHALL use the shared Lua expression engine so opt-in expression projection has the same semantics as the GUI.

#### Scenario: CLI inspect uses shared importer factory rules
- **WHEN** the user runs `inspect` without injecting a test registry
- **THEN** the CLI SHALL construct its importer registry through the shared composition factory rules used by the GUI load path
- **AND** importer fallback behavior SHALL remain available under those same construction rules

#### Scenario: CLI convert uses shared export factory rules
- **WHEN** the user runs `convert` without injecting a test exporter
- **THEN** the CLI SHALL construct its exporter through the shared composition export factory path
- **AND** the exporter SHALL use the shared Lua expression engine for explicit expression options

#### Scenario: Tests can still inject CLI dependencies
- **WHEN** CLI unit tests provide an importer registry, exporter, console, or settings store
- **THEN** the CLI application SHALL use those injected dependencies instead of the shared factories
