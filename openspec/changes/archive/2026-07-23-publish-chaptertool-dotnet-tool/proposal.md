## Why

The standalone CLI currently requires a source checkout or an application publish artifact. Users need a standard NuGet installation path that provides one stable `chaptertool` command and follows the ChapterTool release version.

## What Changes

- Package the standalone CLI as the `ChapterTool` .NET Tool package.
- Install the tool command as `chaptertool` without a product or implementation suffix.
- Build and upload the tool package in the .NET packaging job.
- Publish the tool package to NuGet.org and GitHub Packages from the existing version-tag release workflow.
- Document global and local tool installation, update, and removal commands.

## Capabilities

### New Capabilities

- `dotnet-tool-distribution`: Define the package identity, installed command, release version, installation behavior, and package artifact requirements for the ChapterTool CLI.

### Modified Capabilities

- `tests-build-distribution-assets`: Add the CLI tool package to CI artifacts and the NuGet release workflow.

## Impact

- `src/ChapterTool.Cli/ChapterTool.Cli.csproj` becomes the packable .NET Tool host.
- `.github/workflows/dotnet-ci.yml` packages and uploads the CLI with the Avalonia artifacts.
- `.github/workflows/nuget-publish.yml` publishes the package with `ChapterTool.Core`.
- CLI documentation and `docs/code-map/` identify the installed command and release checks.
- The CLI command tree and Core/Infrastructure behavior do not change.
