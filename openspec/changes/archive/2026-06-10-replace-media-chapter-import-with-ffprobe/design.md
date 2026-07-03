## Context

ChapterTool currently has two production media-container chapter paths:

- `.mp4`, `.m4a`, and `.m4v` route to `Mp4ChapterImporter`, which depends on an `IMp4ChapterReader` implementation backed by ATL.NET.
- `.mkv` and `.mka` route to `MatroskaChapterImporter`, which locates `mkvextract`, reads Matroska XML from stdout, and delegates to `XmlChapterImporter`.

FFmpeg exposes chapters from many demuxers as `AVChapter` entries and `ffprobe -v quiet -print_format json -show_chapters <input>` returns those entries in a container-neutral JSON shape. The FFmpeg call sites identified by `avpriv_new_chapter` cover ordinary file containers such as MOV/MP4, Matroska/WebM, ASF, FLAC, ID3v2-bearing audio, NUT, Ogg/Vorbis, WAV, Audible AA, and ffmetadata, plus non-file or specialized demuxers such as concat and DVD video. ChapterTool should use one file-container import path for the ordinary multimedia cases while keeping existing playlist, disc, XML, text, and embedded CUE importers intact.

## Goals / Non-Goals

**Goals:**

- Provide one ffprobe-backed media chapter importer for ordinary multimedia files.
- Prioritize ffprobe as the default import path for MP4-family chapter extraction, with automatic fallback to ATL.NET only for legacy ATL-supported MP4 extensions when ffprobe cannot be located or started.
- Keep mkvextract as the primary import path for Matroska-family chapter extraction (preserves multi-edition structure natively), with ffprobe as automatic fallback only when mkvextract cannot be located or started.
- Support an explicit extension allow-list based on FFmpeg demuxers that publish chapters: `.mp4`, `.m4a`, `.m4v`, `.mov`, `.qt`, `.3gp`, `.3g2`, `.mkv`, `.mka`, `.mks`, `.webm`, `.asf`, `.wmv`, `.wma`, `.flac`, `.mp3`, `.aac`, `.ogg`, `.oga`, `.ogv`, `.opus`, `.wav`, `.nut`, `.aa`, `.aax`, `.ffmetadata`, and `.ffmeta`.
- Convert ffprobe chapter start/end timestamps into ChapterTool `Chapter` entries without relying on MP4 duration accumulation or Matroska XML.
- Preserve start and end as distinct chapter data so Matroska and other interval-capable formats do not lose chapter range information.
- Preserve structured diagnostics and UTF-8 behavior for dependency, process, JSON, timestamp, and no-chapters failures.
- Keep Core UI-independent and keep process/tool discovery in Infrastructure.

**Non-Goals:**

- Do not replace `.cue`, `.txt`, `.xml`, `.vtt`, `.mpls`, `.ifo`, `.xpl`, BDMV directory, FLAC embedded CUE, or TAK embedded CUE import behavior in this change.
- Do not introduce ffmpeg-based chapter writing or media file mutation.
- Do not support FFmpeg concat scripts or DVD pseudo-inputs as generic media files in the initial route.
- Do not require users to install ATL.NET-specific native dependencies for the default MP4 import path (ffprobe is the primary path; ATL.NET is used only when ffprobe cannot be invoked for legacy ATL-supported MP4 extensions).
- Do not require users to install FFmpeg for the default Matroska import path (mkvextract is the primary path; ffprobe is used only when mkvextract cannot be invoked).
- Do not add or redesign settings UI for configuring FFmpeg/ffprobe paths in this change.

## Decisions

### Use `ffprobe`, not `ffmpeg`, for read-only extraction

`ffprobe` is the stable FFmpeg CLI for inspection and can emit JSON without touching media streams. The importer will invoke:

```text
ffprobe -v quiet -print_format json -show_chapters <input>
```

Alternative considered: use `ffmpeg -i` stderr parsing. That output is presentation-oriented, less stable, and harder to parse safely. JSON from ffprobe is a better contract.

### Add a generic media chapter reader abstraction

Core should replace MP4-specific reader concepts in the production route with a container-neutral abstraction, for example `IMediaChapterReader` returning ordered entries with title, start, optional end, time base, source id, and raw metadata where useful. `MediaChapterImporter` can live in Core if it only depends on that abstraction, while `FfprobeMediaChapterReader` lives in Infrastructure because it runs processes and parses external JSON. The domain chapter model should expose an explicit start/end concept; the existing `Chapter.Time` can remain the compatibility/display start field during migration, but imported media chapter end data must not be dropped.

Alternative considered: put the whole importer in Infrastructure. That would work, but it keeps more mapping behavior outside Core tests. A split abstraction preserves the current test-substitutable importer pattern.

### Map ffprobe chapters by start time

Each ffprobe chapter has `id`, `time_base`, `start`, `start_time`, `end`, `end_time`, and optional `tags`. The importer will:

- Prefer `start_time`/`end_time` decimal seconds when present.
- Fall back to `start * time_base` and `end * time_base` when decimal fields are missing or invalid.
- Sort by start time, then by id/order from JSON for stable output.
- Use `tags.title`, then `tags.TITLE`, then `Chapter NN` as the chapter name.
- Set `Chapter.End` when a valid end is greater than start.
- Do not synthesize `Chapter.End` from the next chapter's start when the source does not provide a valid end.
- Compute `ChapterInfo.Duration` from the greatest valid end time, or zero when no end is available.
- Use source type `MEDIA` for generic file containers and keep display option labels specific enough for the UI, such as `FFprobe Chapters`.

Alternative considered: preserve container-specific source types (`MP4`, `MKV`, `ASF`). That helps display but complicates behavior checks and routing. The source filename and importer id are enough for most workflows; implementation can still expose a descriptive option label without making behavior branch by container.

### Treat Matroska chapter end as explicit interval metadata

Matroska chapters define `ChapterTimeStart` and optional `ChapterTimeEnd`; `ChapterTimeEnd` is the timestamp where the chapter stops applying. It is common for contiguous editions to set one chapter's end equal to the next chapter's start, but the format does not require that. Chapters can omit end times, have gaps, or carry nested semantics. FFmpeg mirrors this model through `AVChapter.start` and `AVChapter.end`, and ffprobe exposes those values as `start_time` and `end_time` when available.

Therefore ChapterTool should preserve the end reported by ffprobe and avoid deriving missing ends from neighbor starts. Default grid display and existing exports continue to use the start time unless a workflow explicitly needs ranges.

Alternative considered: automatically fill missing end with the next start. That creates convenient contiguous ranges but silently invents data and can misrepresent sparse, nested, or intentionally open-ended chapters.

### Preserve existing specific importers ahead of media routing

Extensions already owned by structured importers stay with those importers: `.cue`, `.txt`, `.xml`, `.vtt`, `.mpls`, `.ifo`, `.xpl`, and BDMV directories. `.flac` remains a special case: ChapterTool first attempts the existing embedded CUE importer for legacy behavior; if no embedded CUE is present, the ffprobe media importer can be used as fallback for native FLAC chapter entries.

Alternative considered: route `.flac` directly to ffprobe. That would simplify routing but could break current embedded CUE behavior and tests.

### Discover `ffprobe` through the existing tool locator boundary

`ExternalToolLocator` should support tool id `ffprobe`. It should check an explicit configured executable or FFmpeg directory when settings add that field, then injected search directories/PATH, then return a structured missing dependency diagnostic. Existing MKVToolNIX-specific probing remains only if other code still uses it.

Alternative considered: require ffprobe on PATH only. That is simpler but worse for portable app bundles and user-selected tool directories.

### Per-container import priority with automatic fallback

Each container family has a primary importer chosen for best format fidelity, with automatic fallback only when the primary external tool cannot be located or cannot be started. A primary importer that runs and returns no chapters, invalid metadata, malformed output, timeout, cancellation, or a non-zero process result SHALL report diagnostics directly and SHALL NOT fall back to another importer.

| Container family | Primary | Fallback |
|---|---|---|
| **Legacy ATL-supported MP4** (`.mp4`, `.m4a`, `.m4v`) | ffprobe (authoritative start timestamps) | `Mp4ChapterImporter` (ATL.NET) only when ffprobe cannot be invoked |
| **Other MP4-family** (`.mov`, `.qt`, `.3gp`, `.3g2`) | ffprobe | none |
| **Matroska-family** (`.mkv`, `.mka`, `.mks`, `.webm`) | `MatroskaChapterImporter` (mkvextract → XML, preserves multi-edition) | ffprobe |
| **Other multimedia** (`.asf`, `.ogg`, `.opus`, `.nut`, `.wav`, etc.) | ffprobe | none (return diagnostic) |
| **FLAC** (`.flac`) | `FlacCueImporter` (embedded CUE) | ffprobe (native FLAC chapters) |

**Rationale for Matroska**: mkvextract is the native Matroska tool and preserves the full chapter model: multiple editions (each as a `ChapterSourceOption`), nested chapter atoms, `ChapterTimeEnd`, `ChapterUID`/`EditionUID`, and hidden/enabled flags. ffprobe flattens all editions into one array and does not expose edition grouping in current FFmpeg versions. Using mkvextract as primary avoids data loss.

**Rationale for MP4**: ffprobe provides absolute chapter start timestamps from the container chapter track. ATL.NET reads clip durations and derives starts by accumulation, which can drift when durations are imprecise. ffprobe start times are authoritative.

The fallback logic SHALL live in `RuntimeChapterLoadService`, not in the importer registry, so that individual importers remain single-purpose and testable. The registry resolves one importer per extension for the primary path; the load service orchestrates retry only for explicit cannot-invoke diagnostics such as missing executable, invalid configured executable path, access denied while starting the executable, or process start failure. Diagnostics from a successfully invoked tool are returned to the caller and are not fallback triggers.

`AtlMp4ChapterReader`, `IMp4ChapterReader`, `Mp4ChapterImporter`, `MatroskaChapterImporter`, `IMkvToolNixInstallProbe`, and all related types SHALL be retained as active implementations (primary for Matroska, fallback for MP4).

The `MkvToolnixPath` field in `AppSettings` SHALL be retained to support the mkvextract primary path.

## Risks / Trade-offs

- FFprobe is an external dependency → Provide deterministic missing-tool diagnostics, document dependency expectations, and make tool location test-substitutable.
- Different demuxers expose sparse or inconsistent metadata → Normalize names/times conservatively and test malformed, empty, and Unicode cases.
- Some FFmpeg demuxer chapter producers are not ordinary files → Keep the initial allow-list to normal file extensions and exclude concat/DVD pseudo-inputs.
- End timestamps are not guaranteed to equal the next chapter start → Store source-provided end independently and keep missing ends unknown instead of synthesizing continuous ranges.
- `.flac` already has embedded CUE behavior → Keep existing importer precedence and use ffprobe only when embedded CUE is absent or when routing explicitly chooses media fallback.
- MP4 duration-derived chapters may differ from ffprobe chapter starts → Treat ffprobe start times as authoritative because they are the container chapter model; add fixture tests to capture expected behavior.
- Packaging now depends on FFmpeg tools → Update distribution documentation and tests so release artifacts either include ffprobe or clearly require/probe it.

## Migration Plan

1. Add ffprobe media chapter DTOs, reader result types, and importer mapping tests.
2. Add Infrastructure ffprobe process reader and tool-location support for `ffprobe`.
3. Route MP4-family to ffprobe as primary; only `.mp4`, `.m4a`, and `.m4v` have ATL.NET fallback when ffprobe cannot be invoked. Keep Matroska-family on mkvextract as primary, with ffprobe fallback only when mkvextract cannot be invoked. Route other multimedia extensions to ffprobe as primary.
4. Implement fallback chain in the load service per the container-family priority table.
5. Preserve specialized importers ahead of generic media routing, including FLAC embedded CUE precedence with ffprobe fallback.
6. Retain all existing importer infrastructure (ATL.NET, mkvextract, MKVToolNix probing) — mkvextract is now primary for Matroska, not just a fallback.
7. Add `FfprobePath` and `FfmpegPath` fields to `AppSettings` for tool location.
8. Update tests, fixtures, packaging documentation, and dependency diagnostics.

Rollback is straightforward: for MP4, swap registry back to ATL.NET as primary. For Matroska, the mkvextract primary path is unchanged from current behavior. No persisted data migration is required.

### FLAC fallback through composite import strategy

The registry SHALL resolve `.flac` to a composite strategy: attempt embedded CUE first via `FlacCueImporter`. When `FlacCueImporter` returns a result with a specific `"FlacEmbeddedCueNotFound"` diagnostic code (severity `Error`), the load service SHALL retry with the ffprobe-backed media importer as a fallback. The previous generic `"EmbeddedCueNotFound"` diagnostic is not a compatibility requirement for FLAC in this change. The load service receives this retry logic rather than the registry so that other callers (tests, direct importer consumers) can still exercise the individual importers without retry.

`FlacCueImporter` SHALL be updated to emit `"FlacEmbeddedCueNotFound"` as a structured diagnostic when no Vorbis comment `cuesheet=` entry is found, distinguishing "no embedded cue" from other failures (corrupt file, read error, invalid CUE syntax).

Alternative considered: put the composite logic in the registry. That would require the registry to run importers (breaking its pure-resolver role) or to add a new `ICompositeImporter` concept. Keeping it in the load service preserves existing responsibilities at a lower cost.

### Matroska multi-edition preserved natively by mkvextract

Matroska-family files use mkvextract as the primary importer, which preserves multi-edition structure natively through the `XmlChapterImporter`. Each `EditionEntry` becomes a separate `ChapterSourceOption`, following the same multi-option pattern used by `MplsChapterImporter`.

When mkvextract cannot be invoked and the Matroska-family import falls back to ffprobe, the ffprobe-backed importer SHALL group chapters by `tags.EDITION_UID` when present. Each edition group SHALL become a separate `ChapterSourceOption`.

Grouping rules:
- When chapters have `tags.EDITION_UID`, group by edition UID value, preserving the order editions first appear in the JSON.
- Within each edition, sort chapters by `start_time`, then by `id` for stable ordering.
- When no chapters have `tags.EDITION_UID`, all chapters belong to a single default edition → one `ChapterSourceOption`.
- Each `ChapterSourceOption` uses `Id = "edition-N"` (N = 0-based index), `DisplayName = "Edition NN"` (NN = 1-based), and `CanCombine = false`.
- `ChapterInfo.Title` = `"Edition NN"`, `SourceIndex` = edition index, `SourceType` = `"MEDIA"`.

Current FFmpeg versions (including 8.1) do NOT consistently populate `EDITION_UID` in chapter metadata for Matroska files — chapters from all editions are flattened. The grouping by tag is a forward-looking design. When `EDITION_UID` is absent, the importer produces a single option (flattened behavior). Users who need edition-aware import today should keep mkvextract available, which preserves the full edition structure through the XML importer.

Alternative considered: always flatten to one option regardless of tags. That is simpler but would require a code change to enable edition grouping if/when FFmpeg adds edition tag support. Checking for the tag now makes the behavior version-adaptive without future code changes.

### Process output decoding SHALL be UTF-8

The ffprobe process invocation SHALL rely on the shared `ProcessRunner` UTF-8 stdout/stderr decoding rather than passing `-output_encoding UTF-8`. Real ffprobe builds such as FFmpeg 8.1 can reject `-output_encoding` as an unknown option, so the importer command must stay on the stable JSON chapter options: `-v quiet -print_format json -show_chapters <input>`.

Alternative considered: pass `-output_encoding UTF-8` to ffprobe. That looked attractive as a tool-local setting, but full-chain testing showed it is not portable across supported FFmpeg builds.

### Default ffprobe timeout is 30 seconds

The ffprobe-backed media importer SHALL use a 30-second default timeout for process execution. This is shorter than the previous 60-second mkvextract timeout because ffprobe chapter extraction reads only container-level metadata and does not require scanning media streams. The timeout is configurable through the `ProcessRunRequest.Timeout` field and can be adjusted if future profiles show large files with many chapters exceeding it.

### Duration fallback when no end times are available

When ffprobe returns no chapter with a valid `end_time`, `ChapterInfo.Duration` SHALL be set to `TimeSpan.Zero`. This is a deliberate change from the current `Mp4ChapterImporter` behavior (which accumulates clip durations). Future enhancements may add an optional ffprobe `-show_format` call to obtain the container duration as a fallback, but that requires a second process invocation and is deferred.

Alternative considered: run `ffprobe -show_format` in the same invocation. That changes the JSON shape and couples chapter extraction to format probing. Keeping them separate preserves single-responsibility parsing.

### New `FfprobePath` / `FfmpegPath` AppSettings fields

`AppSettings` SHALL gain:
- `FfprobePath`: optional path to the ffprobe executable or to a directory containing it. When set to a directory, the locator SHALL append the platform executable name (`ffprobe` or `ffprobe.exe`).
- `FfmpegPath`: optional path to the FFmpeg installation directory. When `FfprobePath` is not set but `FfmpegPath` is, the locator SHALL resolve `ffprobe` relative to the FFmpeg directory.

These supplement the existing tool-specific path fields. `MkvToolnixPath` SHALL be retained because mkvextract remains the Matroska primary path, and `Eac3toPath` SHALL be retained because BDMV import still uses eac3to. This change does not add or redesign a settings configuration page; it only extends persisted settings and tool-location behavior.

- ~~Should `SourceType` be a generic `MEDIA` for all ffprobe imports?~~ **Resolved: Use `"MEDIA"`.** The source filename and option display name `"FFprobe Chapters"` are sufficient for identification.
- ~~Should FLAC ffprobe fallback happen automatically?~~ **Resolved: Yes, after `"FlacEmbeddedCueNotFound"` diagnostic, retry with ffprobe in the load service.**
- ~~Should fallback emit a diagnostic when a legacy importer is used?~~ **Resolved: Yes.** When fallback occurs because the primary tool cannot be invoked, the final result SHALL include an informational diagnostic naming the skipped primary tool and the fallback importer used.
