## MODIFIED Requirements

### Requirement: MP4 import
The system SHALL import MP4-family chapters through a managed cross-platform MP4 reader adapter.

#### Scenario: Reader chapters become cumulative starts
- **WHEN** MP4 chapter entries are read as titles with durations
- **THEN** the importer SHALL convert them to cumulative chapter start times

#### Scenario: Reader chapter times normalize to durations
- **WHEN** the MP4 reader backend returns chapter start/end times instead of durations
- **THEN** the reader SHALL normalize them into ordered durations before returning `Mp4ChapterClip` entries

#### Scenario: Managed reader failures are diagnosed
- **WHEN** the managed MP4 reader cannot read chapter metadata because the file is invalid, unsupported, inaccessible, or lacks a readable chapter table
- **THEN** import SHALL return structured diagnostics without UI prompts

#### Scenario: Legacy native library is not required
- **WHEN** MP4 import runs on Windows, macOS, or Linux
- **THEN** it SHALL NOT require `libmp4v2` native library resolution for the default import path

#### Scenario: Empty MP4 chapter set is rejected
- **WHEN** the MP4 reader succeeds but returns no chapter entries
- **THEN** import SHALL fail with a no-chapters diagnostic

#### Scenario: MP4 reader is runtime-substitutable
- **WHEN** tests or composition replace the MP4 reader implementation
- **THEN** `.mp4`, `.m4a`, and `.m4v` imports SHALL use the replacement without changing UI code

#### Scenario: Unicode chapter names are preserved
- **WHEN** MP4 chapter metadata contains non-ASCII titles
- **THEN** the reader SHALL return the original Unicode strings without loss or mangling

#### Scenario: Placeholder reader is not wired in production
- **WHEN** the Avalonia application composes the MP4 import path
- **THEN** `MissingMp4ChapterReader` SHALL NOT be used as the production reader implementation
