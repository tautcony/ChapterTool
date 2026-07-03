## ADDED Requirements

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
The system SHALL import `.mkv` and `.mka` chapters through an mkvextract adapter.

#### Scenario: mkvextract stdout delegates to XML importer
- **WHEN** mkvextract returns valid chapter XML
- **THEN** the Matroska importer SHALL parse stdout through the XML importer and preserve multiple editions

#### Scenario: External process failure is structured
- **WHEN** mkvextract is missing, exits non-zero, writes relevant stderr, times out, or returns empty stdout
- **THEN** the importer SHALL return a dependency/process diagnostic containing process metadata

