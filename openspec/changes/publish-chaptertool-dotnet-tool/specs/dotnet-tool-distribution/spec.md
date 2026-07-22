## ADDED Requirements

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

The `pack-dotnet` CI job SHALL pack the `ChapterTool` NuGet package for each declared runtime after the build and test job succeeds. Each runtime artifact SHALL contain the CLI package and the matching Avalonia output.

#### Scenario: .NET packaging job creates the CLI package

- **WHEN** the build and test job succeeds
- **THEN** the `pack-dotnet` job SHALL create `ChapterTool.<version>.nupkg`
- **AND** it SHALL upload the package with the matching Avalonia output as one named runtime artifact

#### Scenario: CLI artifact is independent from Avalonia artifacts

- **WHEN** the `pack-dotnet` job uploads the CLI package
- **THEN** the CLI package SHALL remain a separate file from the Avalonia output
- **AND** both files SHALL be present in the named runtime artifact
