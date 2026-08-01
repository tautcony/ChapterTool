# BDMV INDEX Parsing Specification

## Purpose
The system parses bounded Blu-ray index.bdmv title and application metadata.

## Requirements

### Requirement: INDEX.BDMV parsing
The system SHALL parse Blu-ray `index.bdmv` files to extract the title table, including FirstPlaybackTitle, TopMenuTitle, and numbered Title entries with their ObjectType, AccessType, and PlaybackType metadata.

#### Scenario: Valid index extracts title structure
- **WHEN** a valid `index.bdmv` is parsed
- **THEN** the TypeIndicator SHALL be "INDX"
- **AND** the FirstPlaybackTitle, TopMenuTitle, and numbered Title list SHALL be accessible as structured records
- **AND** each Title SHALL expose ObjectType (1=HDMV, 2=BD-J), AccessType, PlaybackType, and a typed object reference

#### Scenario: INDEX preserves distinct reference types
- **WHEN** an HDMV or BD-J title entry is parsed
- **THEN** an HDMV entry SHALL expose a MovieObject identifier
- **AND** a BD-J entry SHALL expose a five-character BDJO name
- **AND** neither reference SHALL be interpreted as an MPLS identifier

#### Scenario: Movie titles are identifiable
- **WHEN** index.bdmv parsing succeeds
- **THEN** Title entries with PlaybackType indicating movie playback (0 or 2) SHALL be distinguishable from interactive titles
- **AND** IsMovieObject and IsMoviePlayback properties SHALL simplify movie-title filtering
- **AND** MovieTitles SHALL return only HDMV-object movie titles

#### Scenario: AppInfoBDMV extracts display metadata
- **WHEN** a valid `index.bdmv` with AppInfoBDMV is parsed
- **THEN** InitialOutputModePreference, SSContentExistFlag, VideoFormat, FrameRate, and UserData SHALL be accessible

#### Scenario: Missing INDEX does not throw
- **WHEN** an `index.bdmv` file does not exist or cannot be opened
- **THEN** IndexFile.TryRead SHALL return null without throwing an exception

#### Scenario: INDEX bounds reject oversized inputs
- **WHEN** an `index.bdmv` declares section lengths exceeding configured finite limits
- **THEN** parsing SHALL fail closed with a structured parse failure
