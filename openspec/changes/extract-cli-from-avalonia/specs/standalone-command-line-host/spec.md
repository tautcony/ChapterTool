## ADDED Requirements

### Requirement: Standalone CLI has no desktop dependency

The standalone CLI executable SHALL target `net10.0`, reference `ChapterTool.CommandLine`, and SHALL NOT reference `ChapterTool.Avalonia` or Avalonia packages.

#### Scenario: Standalone project builds without Avalonia

- **WHEN** the standalone CLI project is built
- **THEN** the build SHALL resolve its command-line behavior through `ChapterTool.CommandLine`
- **AND** the project SHALL not require Avalonia desktop assemblies

### Requirement: Standalone host delegates complete arguments

The standalone process entry point SHALL pass the complete argument array to the command-line facade without recognizing or dispatching raw argument values.

#### Scenario: Explicit command reaches the shared command tree

- **WHEN** the standalone executable receives `formats`, `inspect <input>`, or `convert <input>` arguments
- **THEN** it SHALL invoke the shared command-line facade with those arguments
- **AND** it SHALL return the facade's exit code

### Requirement: Standalone host uses explicit CLI semantics

The standalone host SHALL show CLI help when it receives no arguments. It SHALL treat a plain existing file-system path as a CLI usage error instead of starting a GUI.

#### Scenario: No arguments show help

- **WHEN** the standalone executable receives no arguments
- **THEN** it SHALL show generated root CLI help
- **AND** it SHALL exit with code `0`

#### Scenario: Plain existing path is rejected

- **WHEN** the standalone executable receives one existing file-system path without a CLI subcommand
- **THEN** it SHALL report a CLI usage failure
- **AND** it SHALL exit with code `1`

### Requirement: Standalone host reports unhandled failures

The standalone process host SHALL report an unhandled CLI exception to standard error and SHALL return exit code `2`.

#### Scenario: Command exception is reported

- **WHEN** command execution raises an exception that the workflow does not convert to a user-facing failure
- **THEN** the process host SHALL write an exception summary to standard error
- **AND** it SHALL return exit code `2`
