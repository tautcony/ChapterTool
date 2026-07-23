## Context

The current Avalonia executable owns desktop startup and the DotMake command tree. The CLI workflow classes already use Core exporters, Core expression services, Infrastructure settings, process, and importer adapters. The remaining Avalonia coupling comes from the importer registry namespace and from `AppCompositionRoot` static factories.

The migration must preserve functional commands while changing project ownership. The Avalonia host must continue to accept CLI commands and existing paths. The standalone host must require an explicit CLI command and must not depend on Avalonia assemblies.

## Goals / Non-Goals

**Goals:**

- Put command binding and CLI workflow code in a reusable `ChapterTool.CommandLine` library.
- Put process-independent runtime importer composition in Infrastructure.
- Add a small `ChapterTool.Cli` `net10.0` executable.
- Expose typed product-owned launch and run results to both hosts.
- Preserve command functionality, exit-code categories, output streams, settings lookup, fallbacks, and expression behavior.
- Add behavior-focused tests for the extracted paths.

**Non-Goals:**

- Do not redesign the chapter import or export algorithms.
- Do not unify the GUI and CLI fallback orchestration in this change.
- Do not add cancellation wiring unless it is required by the final host implementation.
- Do not lock down every historical help-text or parser interaction detail. Validate the functional command outcomes and required host semantics.
- Do not move Avalonia views, localization, settings UI, themes, fonts, or shell services.

## Decisions

### Use a library plus executable

Create `ChapterTool.CommandLine` as a class library and `ChapterTool.Cli` as an executable. The library owns DotMake definitions and workflow calls. The executable owns process exit-code assignment and top-level exception reporting. This keeps process startup out of reusable command code and avoids an Avalonia reference from the standalone CLI.

The alternative was to make Avalonia reference the CLI executable. That would preserve fewer project moves but would mix process startup with reusable APIs and keep desktop dependencies in the CLI graph.

### Use a typed facade

Expose a public facade with operations equivalent to `RunAsync` and `AnalyzeDesktopLaunch`. Return product-owned records such as `CliRunResult` and `DesktopLaunchDecision`. Keep `CliRunnableResult`, DotMake parse objects, and parser-specific types inside the library.

The facade will use DotMake to parse and bind arguments. It will not recognize command tokens through raw string dispatch. Desktop-only path handling will be implemented as a launch policy around the parsed command result.

### Move runtime importer composition to Infrastructure

Move `IChapterImporterRegistry`, `RuntimeChapterImporterRegistry`, and a shared runtime factory into an Infrastructure import namespace. The factory will own settings-directory resolution, configured tool paths, `PATH` search directories, `ProcessRunner`, ffprobe, ATL MP4, Matroska, media, and BDMV importer construction.

Keep Avalonia-specific `RuntimeChapterLoadService` and its GUI diagnostics orchestration in Avalonia. It will consume the moved interface.

### Keep one settings-directory policy

Add an Infrastructure settings-path provider that resolves the existing local application data `ChapterTool` directory and the current-directory fallback. Both hosts will use it for `settings.json` and external-tool settings. The GUI composition root will call this provider instead of owning a duplicate policy.

### Make standalone defaults explicit

The standalone executable will pass its full argument array to the command-line facade. With no arguments, the command facade will run root help. A plain existing path will produce a usage failure in the standalone host. The Avalonia host will use desktop analysis so no arguments launch the GUI and existing paths launch the GUI with a startup path.

### Test functionality at the new ownership boundary

Move or adapt CLI application tests to the CommandLine test surface. Add tests for standalone no-argument help, explicit command execution, plain-path rejection, and host exception exit code behavior where practical. Retain focused Avalonia tests for GUI launch decisions and compatibility. Use compiled behavior and public APIs, not source-text assertions.

## Risks / Trade-offs

- [Risk] Moving namespaces can break existing tests and GUI service references. -> Update project references and namespaces together, then run focused unit tests before the full solution.
- [Risk] DotMake parser behavior can differ when the root command no longer includes GUI-only path handling. -> Keep parser definitions in the shared library and test required command outcomes for both host policies.
- [Risk] The standalone process can create settings or log directories unexpectedly. -> Reuse the existing settings-path provider and keep CLI logging behavior limited to the existing workflow services.
- [Risk] An executable may accidentally regain a dependency on Avalonia through a transitive reference. -> Build and inspect the project graph; keep `ChapterTool.CommandLine` references limited to Core and Infrastructure.

## Migration Plan

1. Move runtime importer composition and shared policies into Infrastructure.
2. Create the CommandLine library and move CLI sources into it.
3. Add the standalone executable and update both host entry points.
4. Update tests and code maps.
5. Run focused tests, build the changed applications, and run the full solution tests sequentially.

Rollback consists of reverting the project graph and moved source files together. No persisted settings schema or chapter file format changes are introduced.

## Open Questions

No open design questions remain for this functional migration. Optional Ctrl+C cancellation can be added in a later change.
