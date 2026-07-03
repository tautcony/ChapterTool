## ADDED Requirements

### Requirement: .NET 10 solution topology
The rewrite SHALL provide an SDK-style .NET 10 solution with separated Core, Infrastructure, Avalonia, and test projects.

#### Scenario: Required projects restore
- **WHEN** `dotnet restore` runs on a clean checkout
- **THEN** projects for Core, Infrastructure, Avalonia, and tests SHALL restore successfully

#### Scenario: Core has no UI dependency
- **WHEN** project references are inspected
- **THEN** Core SHALL NOT reference Avalonia, WinForms, System.Drawing UI APIs, or Windows-only platform APIs

### Requirement: xUnit test migration
The rewrite SHALL migrate and strengthen existing MSTest coverage into .NET 10 tests.

#### Scenario: Existing parser tests are preserved
- **WHEN** tests are migrated
- **THEN** equivalent assertions SHALL exist for Expression, ToolKits, OGM, WebVTT, CUE, MPLS, IFO, MP4, and SharpDvdInfo behavior

#### Scenario: Tests are split by responsibility
- **WHEN** tests are organized
- **THEN** Core behavior SHALL live in Core tests, process/native/filesystem behavior SHALL live in Infrastructure tests, and ViewModel commands SHALL live in Avalonia or ViewModel tests

### Requirement: Fixture preservation
The rewrite SHALL preserve existing sample fixtures as deterministic test assets.

#### Scenario: Fixtures are working-directory independent
- **WHEN** tests run from CLI, IDE, or CI
- **THEN** VTT, OGM, CUE, MPLS, IFO, MP4, and expression fixtures SHALL be resolved without fragile current-directory assumptions

#### Scenario: Non-ASCII fixture names work
- **WHEN** the Japanese CUE sample is checked out and tested
- **THEN** filename and content SHALL remain usable

### Requirement: CI build test publish
The rewrite SHALL use .NET CLI based CI.

#### Scenario: CI runs tests
- **WHEN** CI runs on push or pull request
- **THEN** it SHALL execute restore, build, and test, and any test failure SHALL fail the workflow

#### Scenario: Publish artifacts are explicit
- **WHEN** a release workflow publishes artifacts
- **THEN** artifacts SHALL include declared runtime files, assets, licenses, and native dependencies according to packaging decisions

### Requirement: Packaging, assets, licenses, and versioning
The rewrite SHALL account for installer strategy, native dependencies, assets, licenses, and a unified version source.

#### Scenario: Legacy bundling tools are replaced or justified
- **WHEN** the .NET 10 app is packaged
- **THEN** Fody/Costura SHALL NOT be required unless a new design decision explicitly justifies them

#### Scenario: Version source is unified
- **WHEN** assembly, package, installer, or release metadata is generated
- **THEN** all versions SHALL derive from one source and SHALL NOT diverge

#### Scenario: Assets are migrated or retired
- **WHEN** packaging is implemented
- **THEN** icons, UI images, native DLLs, and third-party license files SHALL be migrated, replaced, or explicitly retired with rationale
