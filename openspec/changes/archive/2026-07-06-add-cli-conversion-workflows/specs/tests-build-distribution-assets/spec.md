## MODIFIED Requirements

### Requirement: xUnit test migration
The rewrite SHALL migrate and strengthen existing MSTest coverage into .NET 10 tests that run on xUnit v3.

#### Scenario: Test projects reference xUnit v3
- **WHEN** test project package references are inspected
- **THEN** Core, Infrastructure, and Avalonia test projects SHALL use xUnit v3 framework packages and a compatible xUnit v3 Visual Studio runner
- **AND** they SHALL NOT retain xUnit v2 framework package references

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

#### Scenario: CLI workflows are covered without launching desktop UI
- **WHEN** CLI tests run
- **THEN** they SHALL cover CLI token detection, startup-path routing, formats output, inspect output, file conversion, stdout conversion, and representative CLI validation failures
- **AND** they SHALL invoke CLI services or command definitions without starting the Avalonia desktop lifetime

#### Scenario: xUnit v3 test suite runs through dotnet test
- **WHEN** `dotnet test ChapterTool.Avalonia.slnx --no-restore` runs after restore
- **THEN** Core, Infrastructure, and Avalonia test assemblies SHALL execute under xUnit v3 without framework discovery failures
