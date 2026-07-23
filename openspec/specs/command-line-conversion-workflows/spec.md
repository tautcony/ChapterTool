# command-line-conversion-workflows Specification

## Purpose
Define the maintained ChapterTool CLI surface for inspecting supported formats and converting chapter sources without launching the desktop UI.

## Requirements

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

### Requirement: Supported formats are discoverable
The system SHALL provide a CLI command that lists supported input and output formats for basic conversion workflows.

#### Scenario: Formats command lists stable conversion surface
- **WHEN** the user runs the `formats` command
- **THEN** stdout SHALL list the basic input families supported by the runtime importer registry
- **AND** stdout SHALL list the output formats supported by CLI conversion
- **AND** the output SHALL not advertise expression or other high-order transforms as implemented CLI features

### Requirement: CLI inspection reports import structure
The system SHALL provide a CLI command that inspects an input source and reports selectable chapter sets and diagnostics in terminal-friendly text.

#### Scenario: Inspect reports available chapter options
- **WHEN** the user runs `inspect <input>`
- **THEN** stdout SHALL include each imported group and selectable option with stable group/index identifiers
- **AND** the output SHALL identify the default option for each group when one exists

#### Scenario: Inspect reports import diagnostics
- **WHEN** an importer returns warnings, partial results, or informational diagnostics during `inspect`
- **THEN** the CLI SHALL print those diagnostics with severity and code
- **AND** the process SHALL still succeed when at least one selectable chapter option was produced

#### Scenario: Inspect fails structurally when import fails
- **WHEN** the input path is unsupported, missing, or the importer reports import failure
- **THEN** the CLI SHALL print a failure summary and diagnostics to stderr
- **AND** the process SHALL exit with a non-zero code

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

#### Scenario: Convert fails on ambiguous selection
- **WHEN** the importer returns multiple selectable chapter options and the user did not provide enough selection information
- **THEN** the CLI SHALL exit with a non-zero code
- **AND** stderr SHALL list the available group and option identifiers needed to rerun the command deterministically

#### Scenario: Convert fails on unsupported output format
- **WHEN** the user requests an output format that the CLI does not support
- **THEN** the CLI SHALL print a validation error to stderr
- **AND** the process SHALL exit with a non-zero code without launching the GUI

### Requirement: CLI diagnostics and exit codes
The system SHALL provide deterministic console diagnostics and exit codes for CLI workflows.

#### Scenario: Successful command exits zero
- **WHEN** `formats`, `inspect`, or `convert` completes successfully
- **THEN** the process SHALL exit with code `0`

#### Scenario: User-facing CLI failure exits non-zero
- **WHEN** CLI argument validation fails, import/export fails, or selection is ambiguous
- **THEN** the process SHALL exit with code `1`

#### Scenario: Unhandled runtime failure exits with exception code
- **WHEN** a CLI command throws an unhandled exception
- **THEN** the process SHALL print the exception summary to stderr
- **AND** it SHALL exit with code `2`

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
