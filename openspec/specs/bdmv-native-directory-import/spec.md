# BDMV Directory Import Specification

## Purpose
The system imports Blu-ray BDMV sources through bounded managed navigation and playlist parsers.

## Requirements

### Requirement: BDMV directory import

The system SHALL import Blu-ray BDMV sources through a managed C# path. The path SHALL discover navigation data and playlist data through managed parsers.

#### Scenario: BDMV resolves an HDMV title

- **WHEN** an INDEX title references an HDMV MovieObject
- **THEN** the importer SHALL resolve the MovieObject identifier through `MovieObject.bdmv`
- **AND** it SHALL collect playlists from bounded HDMV playback events
- **AND** it SHALL NOT interpret the MovieObject identifier as an MPLS identifier

#### Scenario: BDMV resolves a BD-J title declaration

- **WHEN** an INDEX title references a BD-J Object
- **THEN** the importer SHALL parse the referenced BDJO file
- **AND** it SHALL collect explicitly accessible or autostart playlists
- **AND** it SHALL NOT execute BD-J JAR files or Xlets

#### Scenario: BDMV merges playlist scan evidence

- **WHEN** the BDMV source contains structurally valid MPLS files
- **THEN** the importer SHALL scan the playlist directory with explicit bounds
- **AND** it SHALL apply structural duplicate and repeated-segment filtering
- **AND** it SHALL merge scan evidence with navigation evidence through one deterministic policy

#### Scenario: BDMV diagnoses dynamic BD-J navigation

- **WHEN** a BD-J application can select a playlist that its BDJO declaration does not identify
- **THEN** the importer SHALL report an `UnsupportedDynamicBdJNavigation` diagnostic
- **AND** it SHALL use bounded playlist-scan evidence

#### Scenario: BDMV returns aggregate playlist entries

- **WHEN** a discovered MPLS file contains multiple PlayItems
- **THEN** the importer SHALL return one entry for the complete playlist
- **AND** chapter marks SHALL use the cumulative playlist timeline
- **AND** media references SHALL contain all ordered and distinct PlayItem clips

#### Scenario: BDMV omits a no-chapter entry

- **WHEN** a discovered playlist contains no chapter marks
- **THEN** the parity result SHALL retain the playlist candidate
- **AND** the chapter import result SHALL NOT contain an entry for that playlist

#### Scenario: BDMV uses backup navigation files

- **WHEN** a required primary INDEX, MovieObject, BDJO, or playlist file is absent or unusable under the backup policy
- **THEN** the importer SHALL try the corresponding `BDMV/BACKUP` path
- **AND** it SHALL report the selected source in diagnostics

#### Scenario: BDMV preserves disc metadata

- **WHEN** `BDMV/META/DL/*.xml` contains a disc title
- **THEN** the importer SHALL apply the disc title to imported chapter sets

#### Scenario: BDMV reports bounded progress

- **WHEN** BDMV import resolves navigation and scans playlists
- **THEN** it SHALL report discovery and playlist processing progress
- **AND** cancellation SHALL stop all remaining work

### Requirement: BDMV input normalization

The system SHALL normalize a disc root, a `BDMV` directory, and the primary `index.bdmv` file to one source layout.

#### Scenario: Accepted inputs are equivalent

- **WHEN** a user loads any accepted input form for the same disc
- **THEN** every form SHALL return the same ordered entries and diagnostics

#### Scenario: Arbitrary BDMV file is rejected

- **WHEN** a user selects `MovieObject.bdmv` or another `.bdmv` file as the top-level input
- **THEN** the importer SHALL return an invalid-structure diagnostic

### Requirement: BDMV reference manifest

The system SHALL compare BDMV discovery with committed BDMV reference manifests.

#### Scenario: Standard parity tests use managed input

- **WHEN** the automated test suite runs
- **THEN** it SHALL compare BDMV output with committed title, order, duration, chapter, and clip data
- **AND** it SHALL NOT require an external BDMV tool installation
