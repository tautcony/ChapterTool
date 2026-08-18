# CLI Host Architecture

## Document Status

- Date: 2026-07-28
- Result: Implemented
- Scope: Command-line host ownership and desktop boundary

## Decision

`ChapterTool.CommandLine` is the standalone command-line executable and .NET Tool package.
It owns the process entry point, DotMake command definitions, command workflows, console output, and CLI package metadata.

`ChapterTool.Avalonia` is a desktop GUI executable.
It does not reference `ChapterTool.CommandLine`.
It does not parse or dispatch CLI commands.
It starts the GUI for every process invocation.

The two hosts share `ChapterTool.Core`, `ChapterTool.Contracts`, and the applicable Infrastructure contracts through their own composition roots.

## Project Graph

```text
ChapterTool.CommandLine ---> ChapterTool.Infrastructure ---> ChapterTool.Contracts
             |                         |                         |
             +-----------------------> ChapterTool.Core <---------+

ChapterTool.Avalonia ------> ChapterTool.Infrastructure
       |                    ChapterTool.Contracts
       +-------------------> ChapterTool.Core
```

| Project | Type | Responsibility | Direct references |
| --- | --- | --- | --- |
| `ChapterTool.Core` | Multi-target library | Chapter models, importers, transforms, and exporters | None in the product graph |
| `ChapterTool.Contracts` | `net10.0` library | Host-neutral settings models and platform contracts | Core |
| `ChapterTool.Infrastructure` | `net10.0` library | Files, settings, processes, external tools, and runtime importer composition | Core, Contracts |
| `ChapterTool.CommandLine` | `net10.0` executable and .NET Tool | DotMake commands, CLI workflows, process startup, and package output | Core, Infrastructure, DotMake.CommandLine |
| `ChapterTool.Avalonia` | `net10.0` executable | Desktop shell and desktop adapter composition | Core, Contracts, Infrastructure, shared Avalonia UI |

## CommandLine Ownership

The following paths own the standalone CLI:

- `src/ChapterTool.CommandLine/Program.cs`
- `src/ChapterTool.CommandLine/ChapterToolCliHost.cs`
- `src/ChapterTool.CommandLine/Cli/ChapterToolCliCommands.cs`
- `src/ChapterTool.CommandLine/Cli/ChapterToolCliSupport.cs`
- `src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.cs`
- `tests/ChapterTool.CommandLine.Tests/Cli/ChapterToolCliApplicationTests.cs`

`Program.cs` passes the complete argument array to `ChapterToolCliHost`.
It does not recognize individual argument values.

The standalone host shows help for no arguments.
It returns code `0` for successful commands, code `1` for user or workflow failures, and code `2` for unhandled exceptions.

The command tree provides `formats`, `inspect`, and `convert`.
The old GUI-only `load` command is removed.

`ChapterTool.CommandLine.csproj` produces the `ChapterTool` package and installs the `chaptertool` command.
The package must not reference Avalonia assemblies.

## Avalonia Boundary

The following paths own the desktop GUI:

- `src/ChapterTool.Avalonia/Program.cs`
- `src/ChapterTool.Avalonia/App.axaml.cs`
- `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs`
- `src/ChapterTool.Avalonia/Views/MainWindow.axaml`
- `src/ChapterTool.Avalonia/Views/MainWindow.axaml.cs`

`Program.cs` initializes Sentry and starts the Avalonia desktop lifetime.
It does not call `ChapterToolCliHost`.

The desktop composition root creates desktop importers, exporters, settings, clipboard, window, shell, logging, font, and external-tool services.
The shared Avalonia UI receives these services through platform ports.

CLI-looking arguments do not select a CLI workflow in the Avalonia executable.
The standalone CLI is the only process entry point for terminal workflows.

## Release And Verification

CI builds and packs `src/ChapterTool.CommandLine/ChapterTool.CommandLine.csproj`.
NuGet publishing restores, builds, tests, and packs the same project.

Run the focused checks in sequence:

1. `dotnet test tests/ChapterTool.CommandLine.Tests/ChapterTool.CommandLine.Tests.csproj --no-restore`
2. `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore`
3. `dotnet build src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj --no-restore`
4. `dotnet test ChapterTool.slnx --no-restore`
5. `openspec validate "migrate-avalonia-browser-shared-ui" --strict`

The compiled project references and the package dependency graph must show no Avalonia dependency for `ChapterTool.CommandLine`.
The Avalonia project references must show no `ChapterTool.CommandLine` dependency.
