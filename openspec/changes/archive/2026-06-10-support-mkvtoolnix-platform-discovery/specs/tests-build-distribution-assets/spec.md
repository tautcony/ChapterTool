## MODIFIED Requirements

### Requirement: xUnit test migration
The rewrite SHALL migrate and strengthen existing MSTest coverage into .NET 10 tests.

#### Scenario: Existing parser tests are preserved
- **WHEN** tests are migrated
- **THEN** equivalent assertions SHALL exist for Expression, ToolKits, OGM, WebVTT, CUE, MPLS, IFO, MP4, and SharpDvdInfo behavior

#### Scenario: MP4 reader tests cover managed adapter behavior
- **WHEN** MP4 import tests run
- **THEN** they SHALL cover successful reader output, reader exception diagnostics, unsupported or malformed metadata, Unicode chapter names, and empty chapter output without requiring `libmp4v2` or a real installed MP4 command-line tool

#### Scenario: MKVToolNix discovery tests avoid machine installation dependencies
- **WHEN** mkvextract discovery tests run
- **THEN** they SHALL cover configured path, PATH search, Windows registry installation data, macOS app bundle discovery, and missing-tool diagnostics using fake filesystem/platform probes rather than requiring a real MKVToolNix installation

#### Scenario: External process encoding tests preserve non-ASCII output
- **WHEN** process runner tests exercise redirected stdout and stderr
- **THEN** they SHALL include non-ASCII text and verify it is decoded without platform terminal mojibake

#### Scenario: Core remains free of platform implementation details
- **WHEN** Core tests verify project and type dependencies
- **THEN** Core SHALL NOT reference registry access, filesystem discovery, MKVToolNix app-bundle probing, or process encoding implementation types

#### Scenario: Tests are split by responsibility
- **WHEN** tests are organized
- **THEN** Core behavior SHALL live in Core tests, process/native/filesystem behavior SHALL live in Infrastructure tests, and ViewModel commands SHALL live in Avalonia or ViewModel tests
