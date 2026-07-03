## Context

The current rewrite already has an `IMp4ChapterReader` interface and an `Mp4ChapterImporter` that converts chapter clip durations into cumulative chapter starts. The runtime composition still wires `MissingMp4ChapterReader`, so `.mp4`, `.m4a`, and `.m4v` imports always fail with a dependency/native-reader diagnostic.

The legacy WinForms implementation used Knuckleball over `libmp4v2.dll`, including Windows-oriented native DLL packaging and path workaround logic. That approach does not meet the rewrite's cross-platform and testable infrastructure goals. MP4 support should be implemented as a managed adapter over ATL.NET (`z440.atl.core`) that can be tested without native binaries and swapped later if a better ISO BMFF parser becomes preferable.

## Goals / Non-Goals

**Goals:**

- Provide working MP4-family chapter import for `.mp4`, `.m4a`, and `.m4v` on Windows, macOS, and Linux through a managed library backend.
- Keep Core independent of native libraries, process execution, and UI prompts.
- Reuse the existing `Mp4ChapterImporter` and `IMp4ChapterReader` boundary.
- Add deterministic reader tests without depending on a developer machine's installed external tools.
- Document the managed dependency and packaging expectations.

**Non-Goals:**

- Reintroduce Knuckleball or direct P/Invoke to legacy `libmp4v2` DLLs.
- Require users to install a third-party MP4 command-line tool for the default import path.
- Implement MP4 chapter writing or editing inside MP4 containers.
- Build a complete ISO BMFF parser in this change.
- Restore legacy Windows-only hardlink/fsutil behavior.

## Decisions

### Use ATL.NET as the primary reader backend

Implement an `AtlMp4ChapterReader` in Infrastructure that implements `IMp4ChapterReader`. It uses ATL.NET (`z440.atl.core`) to read MP4-family chapter metadata and returns `Mp4ChapterReadResult`.

ATL.NET is the default because it is a managed .NET library, has no platform-specific native DLL loading requirement, and keeps MP4 import usable from a normal .NET publish on Windows, macOS, and Linux. The adapter should remain behind `IMp4ChapterReader` so an optional external-tool reader such as `mp4chaps` or a future maintained ISO BMFF parser can be added later without changing UI or Core importer code.

Alternatives considered:

- **Legacy libmp4v2 P/Invoke:** closest to old behavior, but reintroduces native DLL packaging, architecture-specific loading, and Windows-centric workaround code.
- **External tool first (`mp4chaps`, Bento4, FFmpeg/ffprobe):** avoids writing parser logic, but creates a runtime installation/configuration burden and version-dependent output parsing.
- **Custom pure managed ISO BMFF parser:** best long-term control, but higher implementation risk and scope for this change.

### Parse into a backend-neutral clip model

The ATL reader should convert library chapter metadata into `Mp4ChapterClip` entries containing title and duration. The existing `Mp4ChapterImporter` remains responsible for cumulative start time conversion and `ChapterInfo` creation.

If ATL exposes start/end times instead of durations for a chapter table, the Infrastructure adapter should normalize those times into durations before returning clips. Empty chapter lists must be treated as `NoChaptersFound` by the importer rather than as a successful empty import.

### Remove MP4 import from native dependency lookup

MP4 chapter extraction should not use `INativeDependencyService` or `IExternalToolLocator` in the default runtime path. Missing native `libmp4v2` should no longer be a reason for MP4 import failure once the ATL reader is wired.

The existing `FileSystemNativeDependencyService` can keep `libmp4v2` lookup only as legacy diagnostic scaffolding until implementation cleanup removes unused native dependency paths. It should not be used by the new runtime MP4 importer.

### Make runtime composition explicit

`RuntimeChapterImporterRegistry` should accept `IMp4ChapterReader` as a constructor parameter instead of constructing `MissingMp4ChapterReader` internally. `AppCompositionRoot` should construct `AtlMp4ChapterReader` and pass it to the registry. Runtime registry tests should verify `.mp4`, `.m4a`, and `.m4v` resolve to an ATL-backed importer rather than a placeholder.

`MissingMp4ChapterReader` should be removed from the production source tree. If its diagnostic behavior is still useful for negative-path tests, move it into the test project — but never wire it in `AppCompositionRoot`.

### Remove libmp4v2 from native dependency service

`FileSystemNativeDependencyService` currently resolves `libmp4v2` as a native dependency. Once no production MP4 importer calls into it, remove the `libmp4v2` lookup while keeping `mkvextract` and `eac3to` paths intact. The service should not know about dependencies that nothing uses.

### Test through library seam and fixtures

Reader tests should use small MP4-family fixtures where practical and a narrow ATL abstraction seam where direct fixture construction is too expensive. Tests should cover valid chapters, malformed or unsupported metadata, Unicode titles, empty chapter output, and fractional/millisecond timing. Runtime tests should ensure successful fake reader output imports chapters and ATL failures surface as structured diagnostics.

## Risks / Trade-offs

- ATL.NET may not expose every MP4 chapter representation used in the wild -> keep `IMp4ChapterReader` backend-neutral and report unsupported/empty chapter metadata explicitly.
- MP4 files can contain different chapter representations -> support the representations exposed by ATL first and add fixtures before expanding behavior.
- ATL dependency behavior can change across versions -> pin a tested package version and cover reader behavior with fixtures.
- Some target files may be corrupt or very large -> catch ATL exceptions, return structured diagnostics, and avoid partial UI state updates.
