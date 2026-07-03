## ADDED Requirements

### Requirement: CUE import
The system SHALL import `.cue` files through a UI-independent parser.

#### Scenario: Supported encodings import
- **WHEN** a CUE file is UTF-8, UTF-8 BOM, UTF-16 LE BOM, or UTF-16 BE BOM
- **THEN** the importer SHALL decode it and parse through the shared CUE parser

#### Scenario: CUE fields map to chapter model
- **WHEN** a valid CUE sheet contains global title, file, track title, performer, and `INDEX 01`
- **THEN** source type SHALL be `CUE`, source name SHALL come from the first file, and each track SHALL produce one chapter

#### Scenario: Empty or malformed CUE fails
- **WHEN** a CUE file is empty, has no parsed chapters, or contains unsupported malformed index syntax
- **THEN** the importer SHALL return a structured failure diagnostic

### Requirement: FLAC embedded CUE import
The system SHALL import `.flac` files by reading supported embedded CUE text from Vorbis comments.

#### Scenario: Invalid FLAC header fails
- **WHEN** the file header is not ASCII `fLaC`
- **THEN** import SHALL fail with `InvalidContainerHeader`

#### Scenario: Vorbis cuesheet is parsed
- **WHEN** a FLAC Vorbis comment contains key `cuesheet`
- **THEN** the importer SHALL decode it as UTF-8 and pass the value to the shared CUE parser

### Requirement: TAK embedded CUE import
The system SHALL import `.tak` files by scanning for embedded CUE text.

#### Scenario: Invalid TAK header fails
- **WHEN** the file header is not ASCII `tBaK`
- **THEN** import SHALL fail with `InvalidContainerHeader`

#### Scenario: TAK marker extracts CUE
- **WHEN** the TAK tail scan finds a case-insensitive `cuesheet` marker
- **THEN** the importer SHALL extract CUE text according to the compatibility rule and parse it through the shared CUE parser

### Requirement: CUE export
The system SHALL export chapter data as CUE through the common exporter interface.

#### Scenario: Export CUE content
- **WHEN** CUE export runs
- **THEN** output SHALL include `REM Generate By ChapterTool`, global `TITLE`, `FILE`, and ordered `TRACK`, `TITLE`, `INDEX 01` entries for non-separator chapters

#### Scenario: Track numbering is output order
- **WHEN** chapters have arbitrary internal numbers
- **THEN** exported CUE track numbers SHALL start at 01 and increment by output order

