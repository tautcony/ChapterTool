## ADDED Requirements

### Requirement: FFprobe media chapter extraction
The system SHALL import chapters from supported multimedia container files by invoking ffprobe with quiet JSON chapter output. For Matroska-family files, ffprobe serves as the fallback only when mkvextract cannot be invoked.

#### Scenario: FFprobe command extracts chapters
- **WHEN** a supported multimedia file is imported through the media importer
- **THEN** the importer SHALL execute `ffprobe -v quiet -print_format json -show_chapters` against the selected file
- **AND** it SHALL parse chapters from the JSON `chapters` array without reading localized console text

#### Scenario: Supported media extensions route to ffprobe
- **WHEN** a file has extension `.mp4`, `.m4a`, `.m4v`, `.mov`, `.qt`, `.3gp`, `.3g2`, `.asf`, `.wmv`, `.wma`, `.flac`, `.mp3`, `.aac`, `.ogg`, `.oga`, `.ogv`, `.opus`, `.wav`, `.nut`, `.aa`, `.aax`, `.ffmetadata`, or `.ffmeta`
- **THEN** runtime import SHALL route the file to the ffprobe-backed media chapter importer as primary unless a more specific ChapterTool importer has precedence for that extension

#### Scenario: Matroska-family extensions route to mkvextract with ffprobe fallback
- **WHEN** a file has extension `.mkv`, `.mka`, `.mks`, or `.webm`
- **THEN** runtime import SHALL route to mkvextract as primary
- **AND** SHALL fall back to the ffprobe-backed media chapter importer when mkvextract cannot be located or started

#### Scenario: Specialized importers keep precedence
- **WHEN** a source is `.cue`, `.txt`, `.xml`, `.vtt`, `.mpls`, `.ifo`, `.xpl`, a BDMV directory, a FLAC file with embedded CUE data, or a TAK file with embedded CUE data
- **THEN** import SHALL keep using the existing specialized importer behavior instead of replacing it with generic ffprobe media import

#### Scenario: FFprobe cannot be invoked falls back for legacy ATL-supported MP4
- **WHEN** ffprobe is not found or cannot be started and the input is `.mp4`, `.m4a`, or `.m4v`
- **THEN** import SHALL fall back to the ATL.NET-backed `Mp4ChapterImporter` automatically
- **AND** the fallback SHALL NOT require user interaction or configuration
- **AND** the final result SHALL include an informational diagnostic naming `ffprobe` as the skipped primary tool and `Mp4ChapterImporter` as the fallback

#### Scenario: FFprobe serves as fallback for Matroska when mkvextract cannot be invoked
- **WHEN** mkvextract is not found or cannot be started for a Matroska-family file
- **THEN** import SHALL fall back to the ffprobe-backed media importer automatically
- **AND** the fallback SHALL NOT require user interaction or configuration
- **AND** the final result SHALL include an informational diagnostic naming `mkvextract` as the skipped primary tool and the ffprobe-backed importer as the fallback

#### Scenario: FFprobe missing for non-legacy extensions returns diagnostic
- **WHEN** ffprobe is not found or cannot be started and the input is a media extension without a legacy container-specific importer (e.g., `.mov`, `.qt`, `.3gp`, `.3g2`, `.asf`, `.wmv`, `.ogg`, `.opus`, `.nut`)
- **THEN** import SHALL fail with a missing-dependency diagnostic that identifies `ffprobe`
- **AND** the diagnostic SHALL suggest installing FFmpeg

#### Scenario: Invoked FFprobe failure does not fall back for MP4
- **WHEN** ffprobe is successfully started for an MP4-family file but times out, is cancelled, exits non-zero, writes malformed JSON, returns no chapter entries, or returns unusable chapter timestamps
- **THEN** import SHALL fail with the corresponding structured ffprobe diagnostic
- **AND** it SHALL NOT fall back to the ATL.NET-backed `Mp4ChapterImporter`

#### Scenario: FFprobe process failure for non-legacy extensions returns diagnostic
- **WHEN** ffprobe process fails for a media extension without a legacy container-specific importer
- **THEN** import SHALL fail with a structured diagnostic containing process metadata

### Requirement: FFprobe chapter JSON mapping
The system SHALL convert ffprobe chapter JSON into ChapterTool chapter models using normalized timestamps and metadata titles.

#### Scenario: Start and end times map to chapters
- **WHEN** ffprobe returns chapters with valid `start_time` and `end_time` values
- **THEN** the importer SHALL create ordered ChapterTool chapters using `start_time` as the chapter start
- **AND** it SHALL preserve `end_time` as the chapter end when `end_time` is greater than `start_time`
- **AND** default chapter display SHALL continue to show the chapter start time

#### Scenario: End time is not inferred from next start
- **WHEN** ffprobe returns a chapter without a valid end timestamp
- **THEN** the importer SHALL keep that chapter end unknown instead of deriving it from the next chapter start

#### Scenario: Non-contiguous chapter ranges are preserved
- **WHEN** ffprobe returns a chapter end timestamp that is earlier or later than the next chapter start
- **THEN** the importer SHALL preserve the source-provided end timestamp without forcing it to equal the next chapter start

#### Scenario: Rational time base fallback is used
- **WHEN** a ffprobe chapter has missing or invalid decimal timestamp fields but has integer `start` or `end` values and a valid `time_base`
- **THEN** the importer SHALL compute the timestamp from the integer value multiplied by the rational time base

#### Scenario: Chapter names preserve metadata
- **WHEN** a ffprobe chapter has `tags.title` or `tags.TITLE`
- **THEN** the importer SHALL use that value as the ChapterTool chapter name and preserve Unicode text

#### Scenario: Missing title gets deterministic fallback
- **WHEN** a ffprobe chapter lacks a usable title tag
- **THEN** the importer SHALL name it `Chapter NN` using the emitted one-based chapter number

#### Scenario: Empty chapter array fails
- **WHEN** ffprobe succeeds but returns no chapters
- **THEN** import SHALL fail with a structured no-chapters diagnostic and SHALL NOT return stale chapter data

### Requirement: Multi-edition chapter grouping
The system SHALL group chapters by edition when `tags.EDITION_UID` is present in ffprobe output, producing one `ChapterSourceOption` per edition following the MPLS multi-option pattern.

#### Scenario: Chapters grouped by EDITION_UID produce multiple options
- **WHEN** ffprobe returns chapters with `tags.EDITION_UID` values `"100"` and `"200"`
- **THEN** the importer SHALL produce two `ChapterSourceOption` entries
- **AND** the first option SHALL contain chapters with EDITION_UID `"100"` sorted by `start_time`
- **AND** the second option SHALL contain chapters with EDITION_UID `"200"` sorted by `start_time`

#### Scenario: Each edition option follows MPLS multi-option pattern
- **WHEN** chapters are grouped into multiple editions
- **THEN** each `ChapterSourceOption` SHALL have `Id = "edition-N"` (N = 0-based index), `DisplayName = "Edition NN"` (NN = 1-based), and `CanCombine = false`
- **AND** each `ChapterInfo` SHALL have `Title = "Edition NN"`, `SourceIndex` = edition index, `SourceType = "MEDIA"`

#### Scenario: Single edition when EDITION_UID is absent
- **WHEN** ffprobe returns chapters without `tags.EDITION_UID` on any chapter
- **THEN** the importer SHALL produce a single `ChapterSourceOption` with display name `"FFprobe Chapters"`
- **AND** all chapters SHALL be sorted by `start_time` then by `id`

#### Scenario: Mixed EDITION_UID presence treated as groups
- **WHEN** some chapters have `tags.EDITION_UID` and others do not
- **THEN** chapters without EDITION_UID SHALL be grouped into a default unnamed edition as the last option

### Requirement: FFprobe UTF-8 output decoding
The system SHALL decode ffprobe stdout/stderr as UTF-8 to ensure non-ASCII chapter metadata is preserved across platforms.

#### Scenario: FFprobe output is decoded as UTF-8
- **WHEN** the ffprobe-backed media importer executes ffprobe
- **THEN** process stdout and stderr SHALL be decoded as UTF-8 by the process runner
- **AND** the ffprobe command SHALL NOT depend on an `-output_encoding` argument

### Requirement: FFprobe dependency and process diagnostics
The system SHALL report ffprobe dependency, process, timeout, cancellation, JSON, and timestamp failures as structured diagnostics.

#### Scenario: Missing ffprobe is reported as diagnostic
- **WHEN** ffprobe cannot be found through configured paths or process search directories
- **THEN** import SHALL fail with a missing-dependency diagnostic that identifies `ffprobe`
- **AND** the diagnostic SHALL be produced BEFORE attempting any fallback so the load service can decide fallback routing

#### Scenario: FFprobe start failure is reported as cannot-invoke diagnostic
- **WHEN** ffprobe is located but cannot be started because the executable path is invalid, access is denied, or process start throws before the tool runs
- **THEN** import SHALL fail with a structured cannot-invoke diagnostic that identifies `ffprobe`
- **AND** the diagnostic SHALL be produced BEFORE attempting any fallback so the load service can decide fallback routing

#### Scenario: FFprobe process failure is structured
- **WHEN** ffprobe exits non-zero, times out, is cancelled, or writes unusable output
- **THEN** import SHALL fail with a structured diagnostic containing process metadata sufficient for troubleshooting

#### Scenario: Malformed JSON is diagnosed
- **WHEN** ffprobe output is not valid JSON or does not match the expected chapter shape
- **THEN** import SHALL fail with a structured parse diagnostic before fallback

#### Scenario: Invalid chapter timestamp is diagnosed
- **WHEN** a chapter entry cannot produce a valid non-negative start timestamp
- **THEN** import SHALL skip or reject the invalid entry according to deterministic importer rules and SHALL emit a structured diagnostic
