## ADDED Requirements

### Requirement: Adobe Premiere Pro marker list import
The system SHALL import Adobe Premiere Pro chapter marker lists from tabular text files.

#### Scenario: CSV marker list imports chapter rows
- **WHEN** a `.csv` file has marker-list headers, a recognizable time column, and rows whose marker type is blank or chapter-like
- **THEN** the importer SHALL emit a chapter set with source type `Adobe Premiere Pro`
- **AND** non-chapter marker rows SHALL be ignored when a marker type column is present

#### Scenario: Marker names fall back to comments
- **WHEN** a marker row has no marker name but has a comment or description value
- **THEN** the importer SHALL use the comment or description as the chapter name

#### Scenario: TXT Premiere list is detected before OGM
- **WHEN** a `.txt` file contains a Premiere marker table instead of OGM chapter pairs
- **THEN** the system SHALL import it as Adobe Premiere Pro marker data
- **AND** existing OGM `.txt` files SHALL continue to import as OGM chapters

#### Scenario: Invalid marker list fails without stale chapters
- **WHEN** marker-list text has no recognizable time column or produces no chapter rows
- **THEN** the importer SHALL fail with a structured diagnostic
- **AND** it SHALL NOT return stale chapters
