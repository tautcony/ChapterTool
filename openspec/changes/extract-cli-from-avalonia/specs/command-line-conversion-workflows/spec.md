## MODIFIED Requirements

### Requirement: CLI command tree

The system SHALL expose a maintained command-line interface for ChapterTool through structured DotMake commands instead of ad-hoc argument branching. The command definitions and workflow entry points SHALL live in `ChapterTool.CommandLine`. The Avalonia host SHALL consume a typed launch facade and SHALL not dispatch raw CLI argument strings.

#### Scenario: Root command shows help

- **WHEN** the user runs a supported host with `--help`, `-h`, or `-?`
- **THEN** the CLI SHALL show generated usage/help output for the root command

#### Scenario: Version is available from CLI

- **WHEN** the user runs a supported host with the CLI version option
- **THEN** the process SHALL print the application version to stdout and exit without launching the GUI

#### Scenario: Existing startup paths stay on the GUI path

- **WHEN** the user launches the Avalonia executable with a single existing file-system path and no CLI subcommand or switch
- **THEN** the application SHALL treat that argument as GUI startup input instead of forcing CLI parsing

### Requirement: CLI construction reuses shared composition factories

CLI inspect and convert workflows SHALL obtain importer-registry and export-service construction through Infrastructure-owned shared factories or an equivalent shared factory surface. The default CLI export service SHALL use the shared Lua expression engine so opt-in expression projection has the same semantics as the GUI. The CLI application SHALL depend on `IChapterImporterRegistry` rather than the concrete registry type in its public constructor.

#### Scenario: CLI inspect uses shared importer factory rules

- **WHEN** the user runs `inspect` without injecting a test registry
- **THEN** the CLI SHALL construct its importer registry through the shared Infrastructure factory rules used by the GUI load path
- **AND** importer fallback behavior SHALL remain available under those same construction rules

#### Scenario: CLI convert uses shared export factory rules

- **WHEN** the user runs `convert` without injecting a test exporter
- **THEN** the CLI SHALL construct its exporter through the shared composition export factory path
- **AND** the exporter SHALL use the shared Lua expression engine for explicit expression options

#### Scenario: Tests can still inject CLI dependencies

- **WHEN** CLI unit tests provide an importer registry, exporter, console, or settings store
- **THEN** the CLI application SHALL use those injected dependencies instead of the shared factories
