# chapter-importers-text-xml-matroska-vtt Specification

## Purpose
TBD - created by archiving change rewrite-avalonia-dotnet10. Update Purpose after archive.
## Requirements
### Requirement: Importer contract
The system SHALL expose UI-independent importer metadata, results, and diagnostics.

#### Scenario: Importer metadata is discoverable
- **WHEN** importers are composed
- **THEN** each importer SHALL expose a stable id and supported extensions without UI references

#### Scenario: Import returns diagnostics
- **WHEN** import succeeds, partially succeeds, fails, or is cancelled
- **THEN** the result SHALL expose chapter sets and/or structured diagnostics without showing UI dialogs

### Requirement: OGM/TXT import
The system SHALL import OGM-style `.txt` chapter files.

#### Scenario: Valid OGM text imports chapters
- **WHEN** a text file contains `CHAPTERNN=time` and `CHAPTERNNNAME=name` pairs
- **THEN** the importer SHALL emit chapters in encounter order with source type `OGM`

#### Scenario: First timestamp is normalized
- **WHEN** the first parsed OGM chapter time is not zero
- **THEN** all emitted chapter times SHALL subtract the first parsed time

#### Scenario: Later malformed content is partial
- **WHEN** malformed content appears after at least one emitted chapter
- **THEN** the importer SHALL return parsed chapters with a partial-parse warning diagnostic

### Requirement: WebVTT import
The system SHALL import simple WebVTT cue files as chapter sets.

#### Scenario: Valid WebVTT imports cues
- **WHEN** a `.vtt` file has a `WEBVTT` header and cue timing lines containing `-->`
- **THEN** the importer SHALL use cue start times as chapter times and the first following text line as chapter name

#### Scenario: Invalid WebVTT fails
- **WHEN** the header, timing line, or cue text is missing or malformed
- **THEN** the importer SHALL fail with a structured diagnostic and SHALL NOT return stale chapters

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

### Requirement: XML and Matroska XML import
The system SHALL import Matroska chapter XML documents.

#### Scenario: Editions become selectable chapter sets
- **WHEN** an XML document has root `Chapters` and child `EditionEntry` elements
- **THEN** each edition SHALL produce one chapter set with default edition index zero

#### Scenario: Nested atoms are flattened
- **WHEN** `ChapterAtom` elements are nested
- **THEN** the importer SHALL flatten them into the documented legacy order

#### Scenario: Adjacent duplicate times are removed
- **WHEN** adjacent emitted chapters have equal times
- **THEN** the importer SHALL remove the previous duplicate entry

### Requirement: Matroska container import
The system SHALL import `.mkv`, `.mka`, `.mks`, and `.webm` chapters through mkvextract as the primary path, falling back to ffprobe only when mkvextract cannot be located or started.

#### Scenario: Matroska import uses mkvextract as primary
- **WHEN** a Matroska-family file is imported and mkvextract is available
- **THEN** import SHALL use mkvextract to extract chapter XML and delegate to `XmlChapterImporter`
- **AND** multi-edition structure SHALL be preserved with one `ChapterSourceOption` per edition

#### Scenario: mkvextract cannot be invoked falls back to ffprobe
- **WHEN** mkvextract is not found or cannot be started and the input is a Matroska-family file
- **THEN** import SHALL automatically fall back to the ffprobe-backed media importer
- **AND** the fallback SHALL NOT require user interaction or configuration
- **AND** the final result SHALL include an informational diagnostic naming the fallback

#### Scenario: Invoked mkvextract failure does not fall back
- **WHEN** mkvextract is successfully started for a Matroska-family file but times out, is cancelled, exits non-zero, returns no output, or returns unusable XML
- **THEN** import SHALL fail with the corresponding structured mkvextract diagnostic
- **AND** it SHALL NOT fall back to ffprobe

#### Scenario: ffprobe fallback groups by EDITION_UID when available
- **WHEN** Matroska import falls back to ffprobe and chapters have `tags.EDITION_UID`
- **THEN** the ffprobe-backed importer SHALL group chapters by edition UID into separate `ChapterSourceOption` entries
- **AND** each option SHALL follow the MPLS multi-option pattern: `Id = "edition-N"`, `DisplayName = "Edition NN"`, `CanCombine = false`

#### Scenario: ffprobe fallback without EDITION_UID
- **WHEN** Matroska import falls back to ffprobe and no chapters have `tags.EDITION_UID`
- **THEN** the ffprobe-backed importer SHALL produce a single `ChapterSourceOption` with display name `"FFprobe Chapters"`

#### Scenario: Both tools unavailable
- **WHEN** neither mkvextract nor ffprobe is available
- **THEN** import SHALL fail with structured diagnostics identifying both missing tools

#### Scenario: Matroska chapter end is preserved
- **WHEN** mkvextract returns `ChapterTimeEnd` elements (primary) or ffprobe returns `end_time` (fallback)
- **THEN** the importer SHALL preserve it as the chapter end field without assuming it equals the next chapter start

#### Scenario: Matroska chapter end may be absent
- **WHEN** a Matroska chapter has no effective end timestamp
- **THEN** the importer SHALL keep the chapter end unknown and SHALL NOT synthesize it from neighboring chapters

#### Scenario: External process failure is structured
- **WHEN** mkvextract is missing, cannot be started, exits non-zero, writes relevant stderr, times out, or returns no output
- **THEN** the importer SHALL return a dependency/process diagnostic
- **AND** only missing-tool or cannot-start diagnostics SHALL be eligible for fallback

#### Scenario: Non-ASCII output is decoded
- **WHEN** mkvextract or ffprobe writes XML or JSON containing non-ASCII text
- **THEN** the importer SHALL receive decoded text without mojibake

#### Scenario: XML file import remains independent
- **WHEN** a standalone `.xml` Matroska chapter document is imported
- **THEN** the existing XML importer SHALL continue to parse it without requiring mkvextract or ffprobe

