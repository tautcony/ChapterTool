## 1. CLI contract and composition

- [x] 1.1 Add `DotMake.CommandLine` and define the root CLI command plus `convert`, `inspect`, and `formats` subcommands.
- [x] 1.2 Update the Avalonia program entrypoint to run CLI commands without launching the desktop lifetime and keep the existing GUI startup path for non-CLI usage.

## 2. Conversion and output implementation

- [x] 2.1 Implement a CLI application service that resolves importers, selects chapter groups/options, renders diagnostics, and maps stable CLI options into `ChapterExportOptions`.
- [x] 2.2 Implement file output and stdout output for basic conversions while explicitly leaving expression-based transforms disabled.

## 3. Verification

- [x] 3.1 Add automated tests for CLI parsing and service behavior, including formats output, inspect output, deterministic selection, file conversion, stdout conversion, and non-zero failure exits.
- [x] 3.2 Validate the OpenSpec change and run the relevant .NET test commands for the modified solution.
