# CLI Extraction Feasibility Analysis

## Document Status

- Date: 2026-07-21
- Result: Feasible
- Confidence: High
- Scope: Architecture analysis only
- Implementation status: Not started

## Decision

ChapterTool can move its command-line interface (CLI) out of `ChapterTool.Avalonia`.

The recommended design has a reusable command-line library and a separate CLI executable.
The Avalonia host references the library to preserve its current command-line compatibility.
The standalone CLI also references the library and does not reference Avalonia.

Do not make `ChapterTool.Avalonia` reference the CLI executable project.
An executable assembly is referenceable, but that design mixes reusable APIs with process startup and publish rules.

## Current State

The Avalonia executable currently owns both desktop startup and CLI startup.
`Program.Main` calls `ChapterToolCliSupport.AnalyzeLaunch` before it selects a desktop or CLI path.
See `src/ChapterTool.Avalonia/Program.cs:14` and `src/ChapterTool.Avalonia/Program.cs:17`.

The CLI implementation has four source files:

- `src/ChapterTool.Avalonia/Cli/ChapterToolCliApplication.cs`
- `src/ChapterTool.Avalonia/Cli/ChapterToolCliCommands.cs`
- `src/ChapterTool.Avalonia/Cli/ChapterToolCliSupport.cs`
- `src/ChapterTool.Avalonia/Cli/CliConsole.cs`

`ChapterToolCliCommands` uses `DotMake.CommandLine` for command definitions and argument binding.
The command classes call `ChapterToolCliApplication` for `formats`, `inspect`, and `convert` workflows.

`ChapterToolCliApplication` does not use Avalonia controls, XAML, ViewModels, or the desktop lifetime.
Most dependencies already come from `ChapterTool.Core` and `ChapterTool.Infrastructure`.

Two dependencies keep the CLI inside the Avalonia project:

- `AppCompositionRoot` supplies shared importer and exporter factories.
- `RuntimeChapterImporterRegistry` has an Avalonia namespace and project location.

These dependencies appear in `src/ChapterTool.Avalonia/Cli/ChapterToolCliApplication.cs:3-4` and lines `19-42`.

`RuntimeChapterImporterRegistry` does not use an Avalonia API.
It composes Core importers and Infrastructure adapters.
It also owns path routing and importer fallback rules.
See `src/ChapterTool.Avalonia/Services/RuntimeChapterImporterRegistry.cs:14-82`.

The current project graph is:

```text
ChapterTool.Avalonia
  -> ChapterTool.Core
  -> ChapterTool.Infrastructure

ChapterTool.Infrastructure
  -> ChapterTool.Core
```

`DotMake.CommandLine` is a direct dependency of `ChapterTool.Avalonia`.
The main solution does not contain a separate command-line project.

## Feasibility Findings

### Code Boundary

The extraction does not require a GUI rewrite.
The CLI application already separates command binding from conversion workflows.
The CLI console also has an injectable interface for tests.

The extraction requires one dependency correction.
Desktop runtime composition that has no UI behavior must move below the Avalonia project.

The minimum move includes these types or equivalent replacements:

- `IChapterImporterRegistry`
- `RuntimeChapterImporterRegistry`
- The importer-registry factory from `AppCompositionRoot`
- The default settings-directory policy
- The executable search-path policy used by importer composition

The exporter does not require an Avalonia factory.
`ChapterExportService`, `ChapterTimeFormatter`, and `LuaExpressionScriptService` are Core types.
A shared runtime factory can still construct them to enforce one composition policy.

### Behavior Boundary

The current CLI has stable behavior that the extraction must preserve:

- `formats`, `inspect`, and `convert` command names
- DotMake-generated help and version behavior
- Exit code `0` for success
- Exit code `1` for user or workflow failures
- Exit code `2` for unhandled exceptions
- Exported content on standard output
- Diagnostics on standard error for standard-output conversion
- UTF-8 file output without a byte order mark
- Shared `settings.json` lookup for the default save directory and external tools
- Lua expression and expression-preset behavior

The CLI and GUI do not share one complete import workflow today.
Both use the same registry, but each path implements fallback orchestration.

The GUI retains primary diagnostics when a fallback importer runs.
The CLI currently returns the fallback diagnostics and adds a fallback information diagnostic.
See `RuntimeChapterLoadService.cs:29-56` and `ChapterToolCliApplication.cs:280-319`.

The first extraction must preserve this difference.
A later change can introduce one shared import execution contract after explicit parity tests.

### Framework Boundary

The standalone CLI must target `net10.0`.
`ChapterTool.Infrastructure` targets only `net10.0`, although `ChapterTool.Core` targets three frameworks.

The CLI does not need these Avalonia-host dependencies:

- Avalonia desktop packages
- Avalonia themes and controls
- Optris icon packages
- Sentry startup code
- Serilog GUI file-log composition

This separation reduces the CLI dependency surface.
It also permits execution on systems without a desktop session.

## Recommended Target Design

### Project Graph

```text
ChapterTool.Core
        ^
        |
ChapterTool.Infrastructure
        ^
        |
ChapterTool.CommandLine
       ^             ^
       |             |
ChapterTool.Cli   ChapterTool.Avalonia
```

The arrows point from a consumer to its dependency.
`ChapterTool.CommandLine` can also reference `ChapterTool.Core` directly.

| Project | Type | Responsibility | Direct references |
| --- | --- | --- | --- |
| `ChapterTool.Core` | Library | Chapter models, importers, transforms, and exporters | None in this graph |
| `ChapterTool.Infrastructure` | Library | Files, settings, tools, processes, runtime importer composition | `ChapterTool.Core` |
| `ChapterTool.CommandLine` | Library | DotMake commands, CLI workflows, console output, launch decisions | `Core`, `Infrastructure` |
| `ChapterTool.Cli` | Executable | Standalone process entry point | `CommandLine` |
| `ChapterTool.Avalonia` | Executable | Desktop host and compatibility entry point | `CommandLine`, `Core`, `Infrastructure` |

### `ChapterTool.Infrastructure`

Move the runtime importer registry and its interface into an Infrastructure import namespace.
Add one shared factory for the registry and its desktop dependencies.

The factory must preserve these policies:

- The unified `ChapterToolSettingsStore`
- Configured external-tool paths
- `PATH` search directories
- `ProcessRunner`
- `FfprobeMediaChapterReader`
- `AtlMp4ChapterReader`
- Matroska, media, and BDMV fallback rules

Add one settings-path provider for the current local application data path.
Both process hosts must use the same `ChapterTool` settings directory by default.

Do not move Avalonia file pickers, windows, localization, themes, fonts, or shell ViewModels.
These types remain in `ChapterTool.Avalonia`.

### `ChapterTool.CommandLine`

Move the four current CLI files into this class library.
Keep `DotMake.CommandLine` in this project.

Change `ChapterToolCliApplication` to depend on `IChapterImporterRegistry`.
Do not require the concrete registry type in its public constructor.

Expose a product-owned facade for both hosts.
The facade must hide `CliRunnableResult` and other DotMake implementation types.

The facade needs two operations:

- Run the standalone command tree and return an exit code.
- Analyze the desktop command tree and return a typed launch decision.

The desktop decision can contain these outcomes:

- Start the GUI without a path.
- Start the GUI with a source path.
- Run a CLI command.

The facade must use `DotMake.CommandLine` to define, parse, and bind all CLI arguments.
It must not dispatch raw argument strings with custom branches.

### `ChapterTool.Cli`

Add a small `net10.0` executable project.
Its `Program.Main` passes all arguments to the command-line facade.
It does not inspect argument values.

The process host owns these concerns:

- Process exit-code assignment
- Unhandled exception reporting
- Optional `Ctrl+C` cancellation wiring

Cancellation is a behavior enhancement.
Implement it after the extraction or protect it with explicit tests.

### `ChapterTool.Avalonia`

Add a project reference to `ChapterTool.CommandLine`.
Remove the direct `DotMake.CommandLine` package reference.

`Program.Main` must consume the typed desktop launch decision.
It must not recognize or dispatch raw CLI arguments.

The Avalonia process can retain the current embedded CLI path for compatibility.
This path must call the same facade as the standalone process.

Move Sentry initialization after the launch decision.
Initialize Sentry only for the GUI unless a separate CLI telemetry policy requires it.

## Host Semantics

The two executables have different default purposes.
Their argument behavior must be explicit.

| Input | Avalonia executable | Standalone CLI executable |
| --- | --- | --- |
| No arguments | Start the GUI | Show CLI help |
| `--help` | Show CLI help | Show CLI help |
| `formats` | Run the CLI command | Run the CLI command |
| `inspect ...` | Run the CLI command | Run the CLI command |
| `convert ...` | Run the CLI command | Run the CLI command |
| One existing path | Start the GUI with that path | Return a usage error |
| `load <path>` | Start the GUI with that path | Return a usage error or omit the command |
| Unknown token | Return a CLI usage error | Return a CLI usage error |

The recommended standalone behavior requires an explicit subcommand.
It avoids an implicit dependency on the Avalonia executable.

The current `load` handler only returns success.
The Avalonia launch analyzer intercepts it before command execution.
The standalone command tree must not run this no-op handler.

## Alternatives

| Option | Advantages | Costs | Decision |
| --- | --- | --- | --- |
| Shared library plus CLI executable | Clear host boundaries and reusable tests | Adds two projects | Recommended |
| Avalonia references a CLI executable | Adds only one project | Mixes host and library concerns | Do not recommend |
| Remove CLI behavior from Avalonia | Produces the cleanest host separation | Breaks current commands and path behavior | Possible later |
| Add a new application layer | Gives all hosts one composition owner | Adds a larger architecture change | Use only if more desktop hosts appear |

## OpenSpec Impact

There are no active OpenSpec changes on 2026-07-21.
`openspec list --json` returned an empty change list.

The current specifications permit the extraction.
They require an equivalent shared factory surface and do not require CLI files to stay in Avalonia.

The implementation must create a new OpenSpec change because it changes solution topology and release artifacts.
The change must update these specification areas:

- `command-line-conversion-workflows`
- `supporting-ui-platform-services`
- `tests-build-distribution-assets`

The change must define standalone path behavior and Avalonia compatibility behavior.
It must also define the CLI publish artifact and its version source.

## Migration Plan

### Phase 1: Capture Existing Contracts

1. Add process-level tests for current CLI output and exit codes.
2. Record the current GUI path-routing behavior.
3. Record the current importer fallback diagnostics.
4. Keep output encoding and settings behavior unchanged.

### Phase 2: Move Shared Runtime Composition

1. Move the importer registry and interface to Infrastructure.
2. Move the shared importer factory to Infrastructure.
3. Add one shared settings-directory provider.
4. Move registry tests to `ChapterTool.Infrastructure.Tests`.
5. Keep Avalonia composition identity tests for GUI lifetimes.

### Phase 3: Add the Command-Line Library

1. Create `src/ChapterTool.CommandLine`.
2. Move CLI application and command definitions into the library.
3. Add the host-neutral facade and launch decision.
4. Move CLI unit tests into a dedicated test project.
5. Confirm that the library has no Avalonia assembly reference.

### Phase 4: Add the Standalone Host

1. Create `src/ChapterTool.Cli`.
2. Add its thin process entry point.
3. Define empty, path, `load`, help, and error behavior.
4. Add real-process smoke tests.
5. Verify the CLI on each release runtime identifier.

### Phase 5: Reconnect Avalonia

1. Reference `ChapterTool.CommandLine` from Avalonia.
2. Replace the current CLI implementation with the shared facade.
3. Preserve existing desktop path routing.
4. Remove the direct DotMake package from Avalonia.
5. Verify that CLI commands do not start the Avalonia lifetime.

### Phase 6: Publish and Document

1. Add both new projects to `ChapterTool.Avalonia.slnx`.
2. Publish a distinct CLI artifact for each supported runtime identifier.
3. Verify executable files, runtime configuration, help, and exit codes.
4. Update the release workflow and artifact names.
5. Update the README, packaging strategy, code maps, and test map.

## Test Strategy

| Test project | Required coverage |
| --- | --- |
| `ChapterTool.Infrastructure.Tests` | Registry routing, fallback rules, factory policy, settings path |
| `ChapterTool.CommandLine.Tests` | Commands, validation, inspect, convert, expressions, output routing |
| CLI process tests | Help, version, formats, stdout, stderr, files, exit codes, `load` policy |
| `ChapterTool.Avalonia.Tests` | Desktop launch decisions and compatibility routing |
| `ChapterTool.Avalonia.Headless.Tests` | Existing GUI startup and composition regressions only |

Tests must not read project or source files as text to verify dependencies.
Use compiled assembly metadata for dependency-boundary tests.

Run focused test projects in sequence.
Do not start multiple `dotnet test` processes for this solution in parallel.

The implementation verification sequence should include these commands:

```text
dotnet test tests/ChapterTool.Infrastructure.Tests/ChapterTool.Infrastructure.Tests.csproj --no-restore
dotnet test tests/ChapterTool.CommandLine.Tests/ChapterTool.CommandLine.Tests.csproj --no-restore
dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore
dotnet build src/ChapterTool.Cli/ChapterTool.Cli.csproj --no-restore
dotnet build src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj --no-restore
dotnet test ChapterTool.Avalonia.slnx --no-restore
openspec validate --all
```

## Publish Impact

The current scripts publish only `ChapterTool.Avalonia`.
The current artifact verifier also recognizes only the Avalonia executable and macOS application bundle.

The implementation needs a separate CLI publish path.
Use a distinct output directory to prevent shared dependency file collisions.

Recommended artifact names include:

- `ChapterTool-Avalonia-<runtime>`
- `ChapterTool-Cli-<runtime>`

The CLI artifact must contain the app host or DLL, its runtime configuration, required dependencies, and license files.
The release version must continue to come from `Directory.Build.props`.

For macOS, keep the CLI executable outside the Avalonia `.app` bundle.
A separate archive is simpler for shell installation and automation.

## Risks and Controls

| Risk | Control |
| --- | --- |
| CLI and GUI factories drift | Put importer composition and path policies in Infrastructure |
| Avalonia starts during a CLI command | Test the typed launch decision and the real process |
| The standalone `load` command succeeds without work | Remove it from that command tree or return a tested usage error |
| Import diagnostics change during the move | Capture current GUI and CLI results before refactoring |
| Settings or external-tool lookup changes | Use one Infrastructure path and composition policy |
| Standard output gains log text | Keep diagnostics and host logs on standard error |
| Publish outputs overwrite shared files | Publish each executable into a separate directory |
| CLI gains Avalonia dependencies | Verify compiled assembly references in tests |
| Versions differ between executables | Keep `Directory.Build.props` as the only version source |

## Acceptance Criteria

The extraction is complete when all these statements are true:

- `ChapterTool.Cli` runs `formats`, `inspect`, and `convert` without an Avalonia dependency.
- The standalone executable does not initialize an Avalonia desktop lifetime.
- `ChapterTool.Avalonia` uses the shared command-line facade.
- Both hosts use `DotMake.CommandLine` for definition, parsing, and binding.
- Both hosts use the same importer, exporter, settings, and Lua expression policies.
- Existing Avalonia command-line invocations keep their documented behavior.
- Standard output, standard error, encoding, diagnostics, and exit codes pass parity tests.
- The solution builds and all test projects pass in sequence.
- Each supported runtime has a verified standalone CLI artifact.
- The code maps and OpenSpec specifications identify the new owners and entry points.

## Files That a Future Change Must Update

- `ChapterTool.Avalonia.slnx`
- `src/ChapterTool.Avalonia/Program.cs`
- `src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj`
- `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs`
- `src/ChapterTool.Avalonia/Cli/`
- `src/ChapterTool.Avalonia/Services/IChapterImporterRegistry.cs`
- `src/ChapterTool.Avalonia/Services/RuntimeChapterImporterRegistry.cs`
- `tests/ChapterTool.Avalonia.Tests/Cli/`
- `scripts/publish.sh`
- `scripts/publish.ps1`
- `scripts/verify-publish-artifact.sh`
- `.github/workflows/dotnet-ci.yml`
- `README.md`
- `docs/migrations/packaging-strategy.md`
- `docs/code-map/README.md`
- `docs/code-map/avalonia.md`
- `docs/code-map/infrastructure.md`
- `docs/code-map/program-form-capability-map.md`
- `docs/code-map/testing.md`
- `openspec/specs/command-line-conversion-workflows/spec.md`
- `openspec/specs/supporting-ui-platform-services/spec.md`
- `openspec/specs/tests-build-distribution-assets/spec.md`

## Final Assessment

The extraction is technically straightforward because the CLI already has no UI dependency.
The main work is project topology, shared composition ownership, host semantics, tests, and packaging.

Use the shared-library and thin-host design.
It gives ChapterTool a real standalone CLI and preserves the Avalonia compatibility surface without a circular dependency.
