## Why

Current media chapter import is split across container-specific backends: MP4-family files use ATL.NET, while Matroska files require `mkvextract` and XML parsing. FFmpeg already exposes container chapters through `ffprobe -v quiet -print_format json -show_chapters`, which can provide one common extraction path for MP4, Matroska, ASF, FLAC/Ogg metadata chapters, NUT, WAV cue chunks, ffmetadata, and other demuxers that publish `AVChapter` entries.

## What Changes

- Adopt ffprobe as the primary import path for MP4-family chapter extraction (authoritative absolute timestamps), with automatic fallback to ATL.NET only for `.mp4`, `.m4a`, and `.m4v` when ffprobe cannot be located or started.
- Keep mkvextract as the primary import path for Matroska-family chapter extraction (preserves multi-edition structure natively), with automatic fallback to ffprobe only when mkvextract cannot be located or started.
- Route other FFmpeg-supported multimedia extensions (`.asf`, `.ogg`, `.opus`, `.wav`, `.nut`, etc.) to the ffprobe-backed importer as the sole path.
- Parse `ffprobe` chapter JSON into ChapterTool chapter sets using each chapter `start_time`, `end_time`, `time_base`, and metadata title fields where available.
- Preserve imported chapter start and end as distinct chapter fields; default chapter display and editing workflows continue to use the start time.
- Preserve deterministic diagnostics for missing ffprobe, process failure, timeout, malformed JSON, empty chapter output, and invalid chapter timestamps.
- Keep existing text, XML, CUE, FLAC embedded CUE, TAK embedded CUE, MPLS, IFO, XPL, and BDMV import behavior unless explicitly superseded by the media-container route.

## Capabilities

### New Capabilities

- `ffprobe-media-chapter-import`: Common ffprobe-backed chapter extraction for multimedia container files that expose FFmpeg `AVChapter` entries.

### Modified Capabilities

- `disc-playlist-media-importers`: MP4-family import defaults to ffprobe; `.mp4`, `.m4a`, and `.m4v` retain ATL.NET fallback only when ffprobe cannot be invoked.
- `chapter-importers-text-xml-matroska-vtt`: Matroska-family import keeps mkvextract as primary (unchanged from today), gains ffprobe as automatic fallback only when mkvextract cannot be invoked; XML file import remains unchanged.
- `tests-build-distribution-assets`: Test and packaging expectations add ffprobe/FFmpeg as MP4 primary dependency and Matroska fallback dependency.

## Impact

- Core importing model: add media chapter JSON DTOs/result mapping and a generic media chapter reader abstraction (`IMediaChapterReader`).
- Infrastructure: add an ffprobe process adapter, JSON parser, timeout/error handling, and tool discovery for `ffprobe`.
- Avalonia composition: route MP4-family to ffprobe as primary, with ATL.NET fallback only for `.mp4`, `.m4a`, and `.m4v` when ffprobe cannot be invoked; keep Matroska-family on mkvextract (unchanged) with ffprobe fallback added only when mkvextract cannot be invoked; route other multimedia extensions to ffprobe.
- Configuration/tool location: support ffprobe discovery through configured ffprobe path, configured FFmpeg directory path, PATH/search directories, and platform-aware executable naming. Add `FfprobePath` and `FfmpegPath` settings fields; settings UI is out of scope for this change.
- Tests: update importer registry, process adapter, fallback routing, integration fixtures, dependency diagnostics, Unicode title preservation, multi-edition/metadata behavior, and packaging documentation expectations.
