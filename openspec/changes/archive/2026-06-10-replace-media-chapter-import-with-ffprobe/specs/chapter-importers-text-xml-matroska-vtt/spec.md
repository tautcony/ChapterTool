## MODIFIED Requirements

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
