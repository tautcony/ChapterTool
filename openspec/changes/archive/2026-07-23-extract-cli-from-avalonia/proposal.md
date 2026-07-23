## Why

The command-line workflows are currently hosted by the Avalonia executable. This couples terminal use to the desktop project and prevents a small standalone CLI from using the existing conversion features. The CLI can move to reusable library code because its workflow logic already uses Core and Infrastructure services.

## What Changes

- Add a `ChapterTool.CommandLine` class library for DotMake command definitions, CLI workflows, console output, and typed launch decisions.
- Add a `ChapterTool.Cli` `net10.0` executable that delegates all arguments to the command-line library.
- Move runtime importer composition and its settings and tool-path policies to `ChapterTool.Infrastructure`.
- Keep the Avalonia executable as a compatibility host that uses the same command-line facade for CLI commands and GUI launch decisions.
- Remove the direct `DotMake.CommandLine` dependency from `ChapterTool.Avalonia`.
- Preserve functional CLI behavior for `formats`, `inspect`, `convert`, help, exit codes, output encoding, settings lookup, fallback imports, Lua expressions, and expression presets.
- Define standalone host behavior so no arguments show CLI help and a plain existing path returns a usage error.
- Add focused tests for the extracted library, standalone host delegation, Infrastructure composition, and Avalonia compatibility behavior.

## Capabilities

### New Capabilities

- `standalone-command-line-host`: Run ChapterTool CLI workflows from an executable that has no Avalonia dependency.

### Modified Capabilities

- `command-line-conversion-workflows`: Keep the existing command tree and workflow behavior while exposing it through a reusable facade and explicit host semantics.

## Impact

- Add `src/ChapterTool.CommandLine` and `src/ChapterTool.Cli` to `ChapterTool.Avalonia.slnx`.
- Move the importer registry contract and runtime implementation from Avalonia to Infrastructure.
- Update `AppCompositionRoot`, `Program.Main`, project references, and CLI tests.
- Add a product-owned launch facade that does not expose DotMake result types to host callers.
- Update `docs/code-map/avalonia.md`, `docs/code-map/infrastructure.md`, and `docs/code-map/testing.md` for the new ownership and test locations.
