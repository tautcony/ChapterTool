## ADDED Requirements

### Requirement: Native BDMV directory import
The system SHALL import Blu-ray BDMV directories through a native C# path that discovers `index.bdmv`, `PLAYLIST/*.mpls`, and `CLIPINF/*.clpi` without requiring external tools.

#### Scenario: Native BDMV discovers main playlist via index
- **WHEN** a BDMV directory with a valid `index.bdmv` is loaded
- **THEN** the native importer SHALL identify movie-type Title entries and resolve their associated playlist file names
- **AND** corresponding CLPI metadata SHALL be available to the delegated MPLS parser without changing the MPLS playlist timeline

#### Scenario: Native BDMV falls back to playlist scanning
- **WHEN** `index.bdmv` is missing or unparseable in the loaded BDMV directory
- **THEN** the native importer SHALL enumerate `BDMV/PLAYLIST/*.mpls` as candidate playlists
- **AND** import SHALL proceed for each parsable playlist with available chapter marks
- **AND** an info-level diagnostic SHALL note the index fallback

#### Scenario: Native BDMV preserves disc metadata
- **WHEN** `BDMV/META/DL/*.xml` exists and contains a disc title
- **THEN** the disc title SHALL be applied to the imported chapter sets
- **AND** source names and media references SHALL be preserved from MPLS parsing

#### Scenario: Native BDMV operates without eac3to
- **WHEN** eac3to is not installed or not configured
- **THEN** native BDMV import SHALL complete successfully using only Core's managed binary parsers
- **AND** no missing-dependency diagnostic SHALL be produced for eac3to

#### Scenario: Native BDMV reports progress
- **WHEN** native BDMV import processes multiple playlists
- **THEN** progress SHALL be reported through the importer progress contract for discovering titles and processing each playlist

#### Scenario: Native BDMV validates directory structure
- **WHEN** the loaded path does not contain a `BDMV/PLAYLIST` subdirectory
- **THEN** import SHALL fail with an InvalidStructure diagnostic

#### Scenario: Native BDMV handles missing playlist files gracefully
- **WHEN** an index-referenced playlist file does not exist on disk
- **THEN** that candidate SHALL be skipped with an info diagnostic
- **AND** other valid candidates SHALL continue to be processed

#### Scenario: Native BDMV delegates MPLS parsing to MplsChapterImporter
- **WHEN** processing a playlist candidate
- **THEN** the importer SHALL call MplsChapterImporter.ImportAsync for each playlist file
- **AND** CLPI auto-discovery SHALL happen inside MplsChapterImporter without coordination from NativeBdmvImporter
