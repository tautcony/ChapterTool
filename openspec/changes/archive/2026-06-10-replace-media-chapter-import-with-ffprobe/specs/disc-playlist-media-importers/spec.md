## MODIFIED Requirements

### Requirement: Disc and media importer contract
The system SHALL import `.mpls`, `.ifo`, `.xpl`, BDMV directories, and ffprobe-supported multimedia files through UI-independent importers with automatic fallback only when a primary external tool cannot be located or started.

#### Scenario: Supported source is routed
- **WHEN** the import service receives a supported file or directory
- **THEN** it SHALL route to the matching importer and return a chapter set or chapter group with structured diagnostics

#### Scenario: Invalid source is rejected
- **WHEN** the source is missing, unsupported, or structurally invalid
- **THEN** import SHALL fail without overwriting accepted UI state

#### Scenario: FFprobe media source is routed with fallback
- **WHEN** the import service receives a supported multimedia file extension
- **THEN** it SHALL route legacy ATL-supported MP4 files (`.mp4`, `.m4a`, `.m4v`) to ffprobe as primary with ATL.NET fallback only when ffprobe cannot be invoked
- **AND** it SHALL route other MP4-family files (`.mov`, `.qt`, `.3gp`, `.3g2`) and other multimedia extensions to ffprobe as primary with no fallback
- **AND** it SHALL route Matroska-family files to mkvextract as primary with ffprobe fallback only when mkvextract cannot be invoked

### Requirement: MP4 import
The system SHALL import MP4-family chapters through the ffprobe-backed media chapter importer, falling back to ATL.NET only for `.mp4`, `.m4a`, and `.m4v` when ffprobe cannot be located or started.

#### Scenario: FFprobe chapter starts are authoritative
- **WHEN** ffprobe returns MP4-family chapter entries with start timestamps
- **THEN** the importer SHALL use those start timestamps as ChapterTool chapter times instead of deriving starts from cumulative MP4 clip durations

#### Scenario: FFprobe cannot be invoked for legacy ATL-supported MP4 falls back to ATL.NET
- **WHEN** ffprobe cannot be located or started for `.mp4`, `.m4a`, or `.m4v`
- **THEN** import SHALL automatically fall back to the ATL.NET-backed `Mp4ChapterImporter`
- **AND** the final result SHALL include an informational diagnostic naming the fallback

#### Scenario: ATL.NET fallback preserves legacy behavior
- **WHEN** MP4 import falls back to ATL.NET
- **THEN** chapters SHALL be derived from cumulative MP4 clip durations as in the current implementation

#### Scenario: Invoked FFprobe MP4 failure does not fall back
- **WHEN** ffprobe is successfully started for an MP4-family file but times out, is cancelled, exits non-zero, returns malformed JSON, returns no chapter entries, or returns unusable chapter timestamps
- **THEN** import SHALL fail with the corresponding structured ffprobe diagnostic
- **AND** it SHALL NOT fall back to ATL.NET

#### Scenario: Legacy native library is required only for fallback
- **WHEN** MP4 import runs on Windows, macOS, or Linux
- **THEN** `libmp4v2` native library resolution SHALL only be required when the ATL.NET fallback path is activated

#### Scenario: Empty MP4 chapter set is rejected
- **WHEN** ffprobe succeeds for an MP4-family file but returns no chapter entries
- **THEN** import SHALL fail with a no-chapters diagnostic (no fallback to ATL.NET since ffprobe succeeded but found nothing)

#### Scenario: Media reader is runtime-substitutable
- **WHEN** tests or composition replace the media chapter reader implementation
- **THEN** MP4-family imports routed to ffprobe SHALL use the replacement without changing UI code

#### Scenario: Unicode chapter names are preserved
- **WHEN** MP4 chapter metadata contains non-ASCII titles
- **THEN** the ffprobe-backed reader SHALL return the original Unicode strings without loss or mangling

### Requirement: Runtime importer registry
Runtime chapter loading SHALL route supported sources through injected importer registrations or factories.

#### Scenario: Supported source dispatches through registry
- **WHEN** the load service receives `.mpls`, `.ifo`, `.xpl`, BDMV directory, ffprobe-supported multimedia files, or other supported chapter sources
- **THEN** it SHALL select the matching importer through an injected registry or factory model and return the importer result

#### Scenario: Importer infrastructure is injected
- **WHEN** dependency-backed importers require external tool location, process execution, native dependency resolution, filesystem access, or parser adapters
- **THEN** those dependencies SHALL come from registered services rather than being constructed inside each load operation

#### Scenario: Importer dispatch is test-substitutable
- **WHEN** tests replace importer registrations, external tool locators, process runners, media chapter readers, or native dependency services
- **THEN** runtime loading SHALL use the replacements without requiring changes to UI code or concrete runtime service constructors

#### Scenario: Dependencies are not recreated per load by default
- **WHEN** multiple load operations run in one application session
- **THEN** singleton or scoped infrastructure services SHALL follow their registered lifetimes instead of being recreated manually inside `LoadAsync`
