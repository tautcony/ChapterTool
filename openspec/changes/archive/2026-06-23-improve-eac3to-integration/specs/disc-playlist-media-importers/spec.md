## MODIFIED Requirements

### Requirement: BDMV eac3to import
The system SHALL import BDMV directories through an eac3to adapter that enumerates title candidates and exports chapter text for parsing.

#### Scenario: Valid BDMV delegates chapter text
- **WHEN** eac3to lists playlists and exports chapter text
- **THEN** the importer SHALL parse exported chapter text through the OGM parser
- **AND** the returned BDMV options SHALL use the exported chapter times rather than direct MPLS chapter parsing

#### Scenario: Candidate metadata is preserved
- **WHEN** an eac3to playlist candidate maps to a readable MPLS file
- **THEN** the importer SHALL preserve available title, source name, source index, source type, duration, frame-rate, and media-reference metadata on the returned chapter option

#### Scenario: Missing eac3to is recoverable
- **WHEN** no valid eac3to path is configured
- **THEN** import SHALL return a missing-dependency result and Core SHALL NOT prompt directly

#### Scenario: eac3to export failure is diagnosed
- **WHEN** eac3to lists one or more chapter-bearing candidates but chapter export fails, times out, is cancelled, produces no chapter file, or produces unparseable chapter text for a candidate
- **THEN** import SHALL fail that candidate with a structured diagnostic
- **AND** BDMV directory import SHALL NOT fall back to direct MPLS-derived chapter times
