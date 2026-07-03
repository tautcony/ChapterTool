## 1. Core Media Import Model

- [x] 1.1 Add container-neutral `IMediaChapterReader` interface in Core.Importing.Media returning `MediaChapterReadResult` with ordered entries (title, start, optional end, time base, source id, raw metadata).
- [x] 1.2 Add `MediaChapterImporter` that maps reader output to `ChapterImportResult`, `ChapterInfoGroup`, `ChapterSourceOption`, `ChapterInfo`, and `Chapter` models, using `SourceType = "MEDIA"` and display name `"FFprobe Chapters"`.
- [x] 1.3 Confirm `Chapter.End` field (already exists as `TimeSpan? End = null`) is correctly populated from reader output; no model changes needed.
- [x] 1.4 Implement timestamp normalization rules: prefer decimal `start_time`/`end_time`, fall back to integer value multiplied by rational `time_base`, reject invalid non-negative starts, keep valid ends only when greater than starts, do not synthesize missing ends from next chapter start.
- [x] 1.5 Implement deterministic chapter naming from `tags.title`, then `tags.TITLE`, then `Chapter NN` fallback while preserving Unicode.
- [x] 1.6 Set `ChapterInfo.Duration` from the greatest valid end time, or `TimeSpan.Zero` when no end is available.
- [x] 1.7 Set `ChapterInfo.FramesPerSecond = 0` (consistent with current MP4/Matroska importers).
- [x] 1.8 Add Core tests for mapping order, title fallback, Unicode preservation, rational time-base fallback, end-time assignment, non-contiguous ranges, missing end preservation, empty chapter output, invalid timestamp diagnostics, and zero end-time Duration behavior.

## 2. FFprobe Infrastructure

- [x] 2.1 Add an ffprobe JSON DTO/parser that reads the `chapters` array without localized console parsing.
- [x] 2.2 Add `FfprobeMediaChapterReader` using `IExternalToolLocator` and `IProcessRunner` with command arguments `-v quiet -print_format json -show_chapters <input>`.
- [x] 2.3 Use a 30-second default timeout for ffprobe process execution.
- [x] 2.4 Map missing ffprobe, cannot-start failure, cancellation, timeout, non-zero exit, empty stdout, malformed JSON, and unusable chapter data to structured diagnostics (include stderr in diagnostics for non-zero exit and malformed JSON cases).
- [x] 2.5 Extend external tool location to support tool id `"ffprobe"`:
  - Check `AppSettings.FfprobePath` (if set to a directory, append `ffprobe`/`ffprobe.exe`; if set to a file path, use directly).
  - Fall back to `AppSettings.FfmpegPath` directory + `ffprobe`/`ffprobe.exe`.
  - Fall back to PATH/search-directory discovery.
  - Platform executable naming (`ffprobe` on macOS/Linux, `ffprobe.exe` on Windows).
- [x] 2.6 Add Infrastructure tests for ffprobe command construction, JSON parsing, process failures, cannot-start failures, missing dependency diagnostics, non-ASCII stdout/stderr decoding, and tool discovery branches without requiring a real FFmpeg installation.
- [x] 2.7 Create test fixture files in `tests/ChapterTool.Core.Tests/Importing/Fixtures/`:
  - `ffprobe_chapters_single_edition.json` — 4 chapters with start/end times and titles
  - `ffprobe_chapters_multi_edition.json` — 2 editions (EDITION_UID "100" with 3 chapters, "200" with 3 chapters)
  - `ffprobe_chapters_mixed_edition.json` — some chapters with EDITION_UID, some without
  - `ffprobe_chapters_empty.json` — empty chapters array
  - `ffprobe_chapters_time_base_fallback.json` — chapters requiring time_base calculation, uppercase TITLE tag, missing title
  - `ffprobe_chapters_missing_end.json` — mixed end times (present, missing, end==start)
  - `ffprobe_chapters_non_contiguous.json` — overlapping and gapped chapter ranges
  - `ffprobe_chapters_unicode.json` — Japanese, Korean, Russian, Arabic titles
  - `ffprobe_chapters_malformed.json` — chapters field is a string, not an array

## 3. Runtime Routing, Fallback, And Composition

- [x] 3.1 Add `FfprobePath` and `FfmpegPath` fields to `AppSettings`.
- [x] 3.2 Update `ExternalToolLocator` to read `FfprobePath` and `FfmpegPath` for `"ffprobe"` tool id resolution; keep existing `MkvToolnixPath` for `"mkvextract"` resolution.
- [x] 3.3 Primary routing:
  - **Legacy ATL-supported MP4** (`.mp4`, `.m4a`, `.m4v`): ffprobe as primary, ATL.NET fallback only when ffprobe cannot be invoked.
  - **Other MP4-family** (`.mov`, `.qt`, `.3gp`, `.3g2`): ffprobe as primary, no fallback.
  - **Matroska-family** (`.mkv`, `.mka`, `.mks`, `.webm`): mkvextract as primary (unchanged from current), ffprobe fallback only when mkvextract cannot be invoked.
  - **Other multimedia** (`.asf`, `.wmv`, `.wma`, `.mp3`, `.aac`, `.ogg`, `.oga`, `.ogv`, `.opus`, `.wav`, `.nut`, `.aa`, `.aax`, `.ffmetadata`, `.ffmeta`): ffprobe as primary, no fallback.
- [x] 3.4 Preserve specialized importer precedence for `.cue`, `.txt`, `.xml`, `.vtt`, `.mpls`, `.ifo`, `.xpl`, BDMV directories, and TAK embedded CUE (these are not routed to ffprobe).
- [x] 3.5 Implement fallback chain in `RuntimeChapterLoadService`:
  - `.mp4`/`.m4a`/`.m4v`: ffprobe missing or cannot-start diagnostic → `Mp4ChapterImporter` (ATL.NET), with informational fallback diagnostic.
  - Matroska-family: mkvextract missing or cannot-start diagnostic → ffprobe-backed media importer, with informational fallback diagnostic.
  - Successfully invoked primary tools that time out, are cancelled, exit non-zero, return malformed output, return no chapters, or return unusable timestamps → return diagnostic directly without fallback.
  - Other multimedia: ffprobe diagnostic → return diagnostic directly.
- [x] 3.6 Implement FLAC precedence: `FlacCueImporter` first; on `"FlacEmbeddedCueNotFound"` diagnostic, retry with ffprobe; on ffprobe failure, return diagnostic.
- [x] 3.7 Update `FlacCueImporter` to emit a specific `"FlacEmbeddedCueNotFound"` diagnostic when no Vorbis comment `cuesheet=` entry is found.
- [x] 3.8 Update `AppCompositionRoot` to construct ffprobe reader alongside existing readers (ATL.NET, mkvextract) — mkvextract wiring remains unchanged as primary for Matroska.
- [x] 3.9 Update `RuntimeChapterImporterRegistry` to keep Matroska-family routing to mkvextract (unchanged), route MP4-family to ffprobe (changed from ATL.NET), route other multimedia to ffprobe (new).
- [x] 3.10 Update registry and load service tests: `.mp4`/`.m4a`/`.m4v` ffprobe-primary with ATL cannot-invoke fallback, Matroska mkvextract-primary with ffprobe cannot-invoke fallback, fallback informational diagnostics, no fallback after invoked primary-tool failures, no-fallback diagnostic for non-legacy extensions, FLAC embedded-CUE precedence with ffprobe fallback.

## 4. Matroska Multi-Edition Handling (MPLS multi-option pattern)

- [x] 4.1 Implement edition grouping logic in `MediaChapterImporter`: group chapters by `tags.EDITION_UID` when present; each group becomes a `ChapterSourceOption` following the MPLS multi-option pattern.
- [x] 4.2 Each edition option: `Id = "edition-N"` (0-based), `DisplayName = "Edition NN"` (1-based), `CanCombine = false`, `ChapterInfo.Title = "Edition NN"`, `ChapterInfo.SourceIndex = editionIndex`, `ChapterInfo.SourceType = "MEDIA"`.
- [x] 4.3 When no chapters have `tags.EDITION_UID`, produce a single `ChapterSourceOption` with display name `"FFprobe Chapters"`.
- [x] 4.4 When some chapters have `tags.EDITION_UID` and others do not, group untagged chapters into a default unnamed edition as the last option.
- [x] 4.5 Add Core tests: multi-edition fixture produces 2 options with correct grouping; single-edition fixture (no EDITION_UID) produces 1 option; mixed fixture produces tagged editions + untagged edition; edition chapter ordering by start_time; empty edition handling.

## 5. Documentation And Verification

- [x] 5.1 Update packaging/dependency documentation to describe ffprobe/FFmpeg as the recommended primary multimedia chapter extraction dependency, with ATL.NET fallback for `.mp4`/`.m4a`/`.m4v` only when ffprobe cannot be invoked and MKVToolNix primary for Matroska-family files.
- [x] 5.2 Run `dotnet test tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj --no-restore`.
- [x] 5.3 Run `dotnet test tests/ChapterTool.Infrastructure.Tests/ChapterTool.Infrastructure.Tests.csproj --no-restore`.
- [x] 5.4 Run `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore`.
- [x] 5.5 Run `dotnet test ChapterTool.Avalonia.slnx --no-restore`.
- [x] 5.6 Run `openspec validate replace-media-chapter-import-with-ffprobe --strict`.
