# disc-playlist-media-importers Specification

## Purpose
TBD - created by archiving change rewrite-avalonia-dotnet10. Update Purpose after archive.
## Requirements
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

### Requirement: MPLS import
The system SHALL parse Blu-ray `.mpls` playlists into selectable chapter segments.

#### Scenario: Valid MPLS produces a group
- **WHEN** a valid MPLS file has one or more play items
- **THEN** each play item SHALL produce one `ChapterInfo` with source type `MPLS`

#### Scenario: Chapter marks convert from PTS
- **WHEN** matching chapter marks are found
- **THEN** timestamps SHALL convert from PTS using `pts / 45000` seconds

#### Scenario: MPLS can append
- **WHEN** another `.mpls` is appended to an existing MPLS group
- **THEN** new play items SHALL be added while preserving existing source context

### Requirement: DVD IFO import
The system SHALL import DVD `.ifo` chapter data.

#### Scenario: Valid IFO produces DVD group
- **WHEN** a valid `VTS_nn_0.IFO` is parsed
- **THEN** the importer SHALL return one or more DVD chapter sets with normalized chapter numbers

#### Scenario: PAL and NTSC times convert
- **WHEN** DVD playback time is converted
- **THEN** PAL SHALL use 25 fps and NTSC SHALL use `30000/1001`

### Requirement: XPL import
The system SHALL parse HD-DVD `.xpl` playlist XML.

#### Scenario: Valid XPL title imports
- **WHEN** an XPL file uses the documented namespace and contains title chapter lists
- **THEN** each title with chapters SHALL produce a chapter set with source type `HD-DVD`

#### Scenario: Invalid XPL is diagnosed
- **WHEN** namespace, duration, or timestamp structure is malformed
- **THEN** the importer SHALL return a parse diagnostic instead of a null-reference failure

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

### Requirement: Segment selection and combine
The system SHALL preserve multi-segment selection, combine, and related-media behavior with structured models.

#### Scenario: Combine is limited to MPLS and IFO
- **WHEN** combine is requested for a non-MPLS/IFO source group
- **THEN** the command SHALL be disabled or return a validation diagnostic

#### Scenario: Combined segments offset times
- **WHEN** MPLS or IFO segments are combined
- **THEN** chapter times SHALL be offset by accumulated segment duration and names SHALL be renumbered

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

### Requirement: Disc binary parsers bound untrusted allocations
MPLS and shared disc binary helpers SHALL reject untrusted length or count fields that would allocate or iterate beyond documented finite limits. Each declared-length container SHALL validate its mandatory header, entries, and final position before allocation, iteration, or skip. Parsers MUST NOT allocate multi-gigabyte buffers, preallocate unchecked collections, or seek backwards solely because a corrupted or malicious field claims that size or count.

#### Scenario: Oversized exact-read length fails
- **WHEN** a binary playlist field instructs the parser to read a byte length above the configured maximum for that read helper or structure
- **THEN** the parser SHALL throw a structured parse failure (`InvalidDataException` or equivalent import failure path)
- **AND** it SHALL NOT allocate a buffer of that oversized length

#### Scenario: Valid MPLS within bounds still imports
- **WHEN** a valid MPLS playlist within normal Blu-ray structural sizes is imported
- **THEN** chapter segments SHALL continue to import according to existing MPLS requirements

#### Scenario: Extension data and mark tables respect limits
- **WHEN** MPLS extension-data block lengths or mark/table counts exceed their configured limits
- **THEN** parsing SHALL fail closed with an invalid-data diagnostic rather than continuing with unbounded allocation

#### Scenario: Playlist and subpath counts respect limits
- **WHEN** MPLS play-item, subpath, stream-table, or nested-entry counts exceed their configured limits
- **THEN** the parser SHALL fail with an invalid-data diagnostic before preallocating or iterating an oversized collection

#### Scenario: Declared container length cannot contain consumed entries
- **WHEN** a MPLS container declares a length smaller than its mandatory header or entries already consumed
- **THEN** parsing SHALL fail with an invalid-data diagnostic
- **AND** it SHALL NOT seek backwards or continue by skipping a negative remainder

### Requirement: XPL XML import uses secure XML loading
XPL playlist import SHALL load XML with the same class of secure settings as Matroska XML import: DTD processing prohibited and no external entity resolution.

#### Scenario: XPL with hostile external entity declarations fails closed
- **WHEN** an XPL document relies on external entity resolution to inject content
- **THEN** import SHALL fail closed or ignore unresolved external entities without fetching them
- **AND** a normal valid XPL without external entities SHALL continue to import per existing XPL requirements

