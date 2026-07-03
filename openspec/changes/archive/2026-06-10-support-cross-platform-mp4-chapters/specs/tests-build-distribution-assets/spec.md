## MODIFIED Requirements

### Requirement: xUnit test migration
The rewrite SHALL migrate and strengthen existing MSTest coverage into .NET 10 tests.

#### Scenario: Existing parser tests are preserved
- **WHEN** tests are migrated
- **THEN** equivalent assertions SHALL exist for Expression, ToolKits, OGM, WebVTT, CUE, MPLS, IFO, MP4, and SharpDvdInfo behavior

#### Scenario: MP4 reader tests cover managed adapter behavior
- **WHEN** MP4 import tests run
- **THEN** they SHALL cover successful reader output, reader exception diagnostics, unsupported or malformed metadata, Unicode chapter names, and empty chapter output without requiring `libmp4v2` or a real installed MP4 command-line tool

#### Scenario: Tests are split by responsibility
- **WHEN** tests are organized
- **THEN** Core behavior SHALL live in Core tests, process/native/filesystem behavior SHALL live in Infrastructure tests, and ViewModel commands SHALL live in Avalonia or ViewModel tests

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

#### Scenario: MP4 managed dependency is documented
- **WHEN** release artifacts or packaging documentation are produced
- **THEN** they SHALL document ATL.NET as the managed MP4 chapter reader dependency and explain that no separate MP4 CLI installation is required for the default path

#### Scenario: Legacy libmp4v2 binaries are not bundled by default
- **WHEN** the Avalonia app is published
- **THEN** publish output SHALL NOT include `Time_Shift/mp4v2` legacy native DLLs unless a later design explicitly restores a maintained native MP4 backend
