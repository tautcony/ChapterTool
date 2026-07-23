# dotnet-tool-distribution Specification

## Purpose

Define the standalone ChapterTool .NET Tool package and its release artifacts.

## Requirements

### Requirement: ChapterTool CLI is a .NET Tool package

The standalone CLI SHALL be packed as the `ChapterTool` NuGet package. The package SHALL install the `chaptertool` command without a suffix. The tool assembly SHALL use the `ChapterTool` name.

#### Scenario: Global installation provides the command

- **WHEN** a user installs the `ChapterTool` package with `dotnet tool install --global ChapterTool`
- **THEN** the .NET Tool resolver SHALL install a command named `chaptertool`
- **AND** the installed command SHALL start the standalone CLI host

#### Scenario: Local installation provides the same command

- **WHEN** a user adds the `ChapterTool` package to a local tool manifest
- **THEN** `dotnet tool run chaptertool` SHALL start the same standalone CLI host

#### Scenario: Package has no desktop dependency

- **WHEN** the `ChapterTool` tool package is restored
- **THEN** it SHALL contain the standalone CLI and its Core, Infrastructure, and CommandLine dependencies
- **AND** it SHALL not require `ChapterTool.Avalonia` or Avalonia runtime assemblies

### Requirement: Tool versions follow the ChapterTool release version

The `ChapterTool` package version SHALL derive from the repository version source. A version-tag release SHALL apply the same package version to `ChapterTool` and `ChapterTool.Core`.

#### Scenario: CI creates a development package

- **WHEN** CI packs the tool without a release-tag override
- **THEN** the package version SHALL use `VersionPrefix` from `Directory.Build.props`

#### Scenario: Release tag sets the package version

- **WHEN** the NuGet release workflow processes a valid `v<version>` tag
- **THEN** the `ChapterTool` package version SHALL equal `<version>`
- **AND** the `ChapterTool.Core` package SHALL use the same version

### Requirement: CLI package is a .NET packaging artifact

The CI workflow SHALL pack the platform-neutral `ChapterTool` NuGet package once after the build and test job succeeds. The workflow SHALL upload that package as `ChapterTool-Cli-nuget`. The `pack-dotnet` job SHALL upload one Avalonia artifact for each declared runtime.

#### Scenario: .NET packaging job creates the CLI package

- **WHEN** the build and test job succeeds
- **THEN** the build job SHALL create `ChapterTool.<version>.nupkg`
- **AND** it SHALL upload the package as `ChapterTool-Cli-nuget`

#### Scenario: CLI artifact is independent from Avalonia artifacts

- **WHEN** CI uploads the CLI package and Avalonia runtime outputs
- **THEN** the CLI package SHALL be present in `ChapterTool-Cli-nuget`
- **AND** each `ChapterTool-Avalonia-<runtime>` artifact SHALL contain only the matching Avalonia output
