# BDMV Native Directory Import Specification

## Purpose
The system imports Blu-ray BDMV sources through bounded managed navigation and playlist parsers.

## Requirements

### Requirement: Native BDMV directory import

The system SHALL import Blu-ray BDMV sources through a native C# path. The path SHALL discover navigation data and playlist data without requiring eac3to.

#### Scenario: Native BDMV resolves an HDMV title

- **WHEN** an INDEX title references an HDMV MovieObject
- **THEN** the importer SHALL resolve the MovieObject identifier through `MovieObject.bdmv`
- **AND** it SHALL collect playlists from bounded HDMV playback events
- **AND** it SHALL NOT interpret the MovieObject identifier as an MPLS identifier

#### Scenario: Native BDMV resolves a BD-J title declaration

- **WHEN** an INDEX title references a BD-J Object
- **THEN** the importer SHALL parse the referenced BDJO file
- **AND** it SHALL collect explicitly accessible or autostart playlists
- **AND** it SHALL NOT execute BD-J JAR files or Xlets

#### Scenario: Native BDMV merges playlist scan evidence

- **WHEN** the BDMV source contains structurally valid MPLS files
- **THEN** the importer SHALL scan the playlist directory with explicit bounds
- **AND** it SHALL apply structural duplicate and repeated-segment filtering
- **AND** it SHALL merge scan evidence with navigation evidence through one deterministic policy

#### Scenario: Native BDMV diagnoses dynamic BD-J navigation

- **WHEN** a BD-J application can select a playlist that its BDJO declaration does not identify
- **THEN** the importer SHALL report an `UnsupportedDynamicBdJNavigation` diagnostic
- **AND** it SHALL use bounded playlist-scan evidence

#### Scenario: Native BDMV returns aggregate playlist entries

- **WHEN** a discovered MPLS file contains multiple PlayItems
- **THEN** the importer SHALL return one entry for the complete playlist
- **AND** chapter marks SHALL use the cumulative playlist timeline
- **AND** media references SHALL contain all ordered and distinct PlayItem clips

#### Scenario: Native BDMV omits a no-chapter entry

- **WHEN** a discovered playlist contains no chapter marks
- **THEN** the parity result SHALL retain the playlist candidate
- **AND** the chapter import result SHALL NOT contain an entry for that playlist

#### Scenario: Native BDMV uses backup navigation files

- **WHEN** a required primary INDEX, MovieObject, BDJO, or playlist file is absent or unusable under the backup policy
- **THEN** the importer SHALL try the corresponding `BDMV/BACKUP` path
- **AND** it SHALL report the selected source in diagnostics

#### Scenario: Native BDMV operates without eac3to

- **WHEN** eac3to is not installed or not configured
- **THEN** native BDMV import SHALL use only managed parsers
- **AND** no missing-eac3to diagnostic SHALL occur unless the user requests explicit eac3to verification

#### Scenario: Native BDMV preserves disc metadata

- **WHEN** `BDMV/META/DL/*.xml` contains a disc title
- **THEN** the importer SHALL apply the disc title to imported chapter sets

#### Scenario: Native BDMV reports bounded progress

- **WHEN** native BDMV import resolves navigation and scans playlists
- **THEN** it SHALL report discovery and playlist processing progress
- **AND** cancellation SHALL stop all remaining work

### Requirement: Native BDMV input normalization

The system SHALL normalize a disc root, a `BDMV` directory, and the primary `index.bdmv` file to one source layout.

#### Scenario: Accepted inputs are equivalent

- **WHEN** a user loads any accepted input form for the same disc
- **THEN** every form SHALL return the same ordered entries and diagnostics

#### Scenario: Arbitrary BDMV file is rejected

- **WHEN** a user selects `MovieObject.bdmv` or another `.bdmv` file as the top-level input
- **THEN** the importer SHALL return an invalid-structure diagnostic

### Requirement: eac3to parity manifest

The system SHALL compare native discovery with committed eac3to reference manifests.

#### Scenario: Standard parity tests do not require eac3to

- **WHEN** the automated test suite runs
- **THEN** it SHALL compare native output with committed title, order, duration, chapter, and clip data
- **AND** it SHALL NOT require a local eac3to installation

#### Scenario: Opt-in parity uses live eac3to output

- **WHEN** the opt-in parity check is enabled
- **THEN** it SHALL run the configured eac3to executable
- **AND** it SHALL compare every manifest field with native output
