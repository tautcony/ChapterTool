## Why

The Avalonia rewrite still exposes MP4-family imports, but the runtime implementation only returns missing native-reader diagnostics. MP4 chapter support needs a cross-platform implementation so the new application can replace the legacy WinForms/libmp4v2 path without reintroducing Windows-only native DLL coupling.

## What Changes

- Add a real MP4 chapter reader implementation behind the existing `IMp4ChapterReader` adapter contract.
- Use ATL.NET (`z440.atl.core`) as the primary managed cross-platform MP4 chapter reader backend.
- Keep an external-tool fallback extension point for compatibility experiments, but do not make a user-installed MP4 CLI the default requirement.
- Keep legacy `libmp4v2`/Knuckleball code out of the new runtime unless a later design explicitly chooses a maintained native binding.
- Route `.mp4`, `.m4a`, and `.m4v` imports through the real reader in Avalonia composition instead of `MissingMp4ChapterReader`.
- Add compatibility fixtures and tests for valid chapters, empty files, malformed/unsupported chapter metadata, Unicode chapter names, and runtime dispatch.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `disc-playlist-media-importers`: MP4-family import changes from a placeholder native-reader diagnostic path to a working cross-platform reader adapter requirement.
- `supporting-ui-platform-services`: Platform dependency rules change to treat MP4 chapter reading as a managed library dependency rather than a native `libmp4v2` lookup or required external CLI.
- `tests-build-distribution-assets`: Build and test expectations change to require MP4 reader coverage without packaging legacy `libmp4v2` native DLLs by default.

## Impact

- Affected code: `src/ChapterTool.Core/Importing/Media`, `src/ChapterTool.Infrastructure`, `src/ChapterTool.Avalonia/Composition`, `src/ChapterTool.Avalonia/Services`, tests under `tests/ChapterTool.Core.Tests`, `tests/ChapterTool.Infrastructure.Tests`, and `tests/ChapterTool.Avalonia.Tests`.
- Affected dependencies: the app gains a managed NuGet dependency on ATL.NET (`z440.atl.core`) for MP4/M4A/M4V chapter metadata reading. The proposal intentionally avoids bundling or P/Invoking old `libmp4v2` DLLs.
- Affected packaging: publish artifacts must include the managed dependency through normal .NET publish output and must not silently include legacy `Time_Shift/mp4v2` binaries.
