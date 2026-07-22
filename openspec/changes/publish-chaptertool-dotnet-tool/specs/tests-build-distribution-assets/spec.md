## MODIFIED Requirements

### Requirement: CI build test publish
The rewrite SHALL use .NET CLI based CI.

#### Scenario: CI runs tests
- **WHEN** CI runs on push or pull request
- **THEN** it SHALL execute restore, build, and test, and any test failure SHALL fail the workflow

#### Scenario: CI packages Avalonia and CLI artifacts
- **WHEN** CI builds a revision that changes the .NET product
- **THEN** each `pack-dotnet` runtime matrix job SHALL publish the Avalonia application
- **AND** it SHALL pack the `ChapterTool` .NET Tool
- **AND** it SHALL upload both outputs as one named artifact for that runtime

#### Scenario: Version release publishes both NuGet packages
- **WHEN** the NuGet workflow processes a successful version-tag build
- **THEN** it SHALL publish `ChapterTool.Core` and `ChapterTool` to NuGet.org
- **AND** it SHALL publish the same packages to GitHub Packages

#### Scenario: Publish artifacts are explicit
- **WHEN** a release workflow publishes artifacts
- **THEN** artifacts SHALL include declared runtime files, assets, licenses, and native dependencies according to packaging decisions
