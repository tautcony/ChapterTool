## Context

`ChapterTool.Cli` is a thin `net10.0` executable that delegates the complete argument array to `ChapterTool.CommandLine`. The project is in the main solution, but it is not a .NET Tool package. The current NuGet workflow packs only `ChapterTool.Core`.

The repository has one version source in `Directory.Build.props`. Version tags can override `PackageVersion` during release. NuGet.org does not currently contain a package with the exact `ChapterTool` ID.

## Goals / Non-Goals

**Goals:**

- Provide `dotnet tool install --global ChapterTool` as the primary CLI installation path.
- Install one command named `chaptertool` on all .NET Tool platforms.
- Publish the CLI package with the same version event as `ChapterTool.Core`.
- Package and upload the CLI in the same .NET packaging job as the Avalonia application.

**Non-Goals:**

- Change the CLI command tree, conversion semantics, or exit codes.
- Bundle the .NET runtime or external multimedia tools in the NuGet package.
- Rename the source project directory or the command-line behavior library.
- Replace desktop release artifacts or the Node.js package.

## Decisions

### Use `ChapterTool` as the package and assembly identity

Set `PackageId` and `AssemblyName` to `ChapterTool`. Set `PackAsTool` to `true` and `ToolCommandName` to `chaptertool`. This gives the public package and executable no `Cli` suffix while the source project can retain its descriptive path.

The alternative was `ChapterTool.Cli` as the package ID. That name exposes an implementation distinction to users and does not satisfy the requested product identity.

### Publish a framework-dependent .NET 10 tool

Keep `TargetFramework` at `net10.0`. The package reuses the existing Core, Infrastructure, and CommandLine projects. It does not reference Avalonia. Users must install a compatible .NET 10 runtime.

The alternative was a runtime-specific self-contained package. A .NET Tool package is platform-neutral and follows the standard `dotnet tool` lifecycle. Self-contained desktop artifacts remain available through the existing release workflow.

### Package Avalonia and CLI in one .NET packaging job

Rename the `publish-app` job to `pack-dotnet`. Keep one matrix entry for each Avalonia runtime. Each runtime entry publishes Avalonia, packs `ChapterTool.nupkg`, and uploads both outputs as one named runtime artifact.

The job will not install the packed CLI. Build and pack success provide the required CI gate. CLI behavior remains covered by the existing unit tests.

### Keep one release version and one publishing workflow

The CLI package inherits `VersionPrefix` from `Directory.Build.props`. The tag workflow passes the same `PackageVersion` override to both pack commands. The workflow pushes both packages in one authenticated NuGet session.

The alternative was a CLI-specific version and workflow. That would create version drift and duplicate release authorization.

## Risks / Trade-offs

- [The `ChapterTool` package ID can be claimed before the first release] -> Reserve or publish the first package promptly. Change the ID only if NuGet.org rejects ownership.
- [Users without .NET 10 cannot start the tool] -> State the runtime requirement next to the install command.
- [A wildcard push can include both packages and symbol packages] -> Keep all expected NuGet artifacts in one release directory and use `--skip-duplicate` for retry safety.
- [The same CLI package is produced once per runtime] -> Keep the package in each runtime artifact as the release contract requires. NuGet publication remains deduplicated by `--skip-duplicate`.

## Migration Plan

1. Add the tool package metadata and package README to `ChapterTool.Cli`.
2. Pack the tool into a dedicated NuGet output directory.
3. Add the CLI to the `pack-dotnet` CI matrix with the Avalonia packages.
4. Extend the tag-triggered NuGet workflow to build and pack both packages.
5. Publish a version tag after the NuGet.org trusted publisher permits the `ChapterTool` package.

Rollback removes or unlists the affected `ChapterTool` package version. `ChapterTool.Core` and desktop releases remain independent artifacts.

## Open Questions

None.
