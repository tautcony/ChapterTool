## MODIFIED Requirements

### Requirement: xUnit test migration
The rewrite SHALL migrate and strengthen existing MSTest coverage into .NET 10 tests.

#### Scenario: Existing parser tests are preserved
- **WHEN** tests are migrated
- **THEN** equivalent assertions SHALL exist for Expression, ToolKits, OGM, WebVTT, CUE, MPLS, IFO, media chapter import, and SharpDvdInfo behavior

#### Scenario: FFprobe media reader tests cover adapter behavior
- **WHEN** media import tests run
- **THEN** they SHALL cover successful ffprobe JSON output, missing ffprobe diagnostics, cannot-start diagnostics, process failure diagnostics, timeout/cancellation handling, malformed JSON, unsupported or chapterless media, Unicode chapter names, timestamp fallback from `time_base`, cannot-invoke fallback routing to legacy importers, multi-edition grouping by EDITION_UID, single-edition fallback without EDITION_UID, mixed EDITION_UID presence, and empty chapter output without requiring a real installed command-line tool

#### Scenario: Fallback routing tests verify automatic degradation
- **WHEN** importer registry/load service tests run
- **THEN** they SHALL cover ffprobe-cannot-invoke-to-ATL fallback for `.mp4`/`.m4a`/`.m4v`, mkvextract-cannot-invoke-to-ffprobe fallback for Matroska-family files, fallback informational diagnostics, invoked-ffprobe-failure-without-ATL-fallback, invoked-mkvextract-failure-without-ffprobe-fallback, and no-fallback diagnostic for extensions without legacy importer support

#### Scenario: External tool discovery tests avoid machine installation dependencies
- **WHEN** ffprobe discovery tests run
- **THEN** they SHALL cover configured executable path, configured FFmpeg directory path, PATH/search-directory discovery, platform executable naming, and missing-tool diagnostics using fake filesystem/platform probes rather than requiring a real FFmpeg installation

#### Scenario: External process encoding tests preserve non-ASCII output
- **WHEN** process runner tests exercise redirected stdout and stderr
- **THEN** they SHALL include non-ASCII text and verify it is decoded without platform terminal mojibake

#### Scenario: Core remains free of platform implementation details
- **WHEN** Core tests verify project and type dependencies
- **THEN** Core SHALL NOT reference registry access, filesystem discovery, FFmpeg probing, MKVToolNix app-bundle probing, or process encoding implementation types

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

#### Scenario: FFprobe dependency is documented
- **WHEN** release artifacts or packaging documentation are produced
- **THEN** they SHALL document ffprobe/FFmpeg as the primary multimedia chapter reader dependency for MP4-family and other non-Matroska containers
- **AND** MKVToolNix/mkvextract SHALL be documented as the primary dependency for Matroska-family files, with ffprobe as fallback only when mkvextract cannot be invoked

#### Scenario: Legacy dependencies retained with appropriate role
- **WHEN** the Avalonia app is published
- **THEN** ATL.NET SHALL be retained as `.mp4`/`.m4a`/`.m4v` fallback (activated only when ffprobe cannot be invoked)
- **AND** MKVToolNix/mkvextract SHALL be retained as Matroska-family primary (unchanged from current)
- **AND** ffprobe/FFmpeg SHALL serve as Matroska-family fallback only when mkvextract cannot be invoked
