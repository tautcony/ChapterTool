# ChapterTool Avalonia Packaging Strategy

## Runtime Model

The Avalonia rewrite publishes from `src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj` through `scripts/publish.ps1`.

- Framework-dependent artifacts are the default and require a .NET 10 runtime.
- Self-contained artifacts are produced by passing `-SelfContained` and an explicit runtime such as `win-x64`.
- Fody and Costura are retired for the .NET 10 rewrite; runtime files remain visible in the publish directory.

## Native And External Dependencies

- Multimedia container chapters use FFmpeg `ffprobe` as the primary reader for MP4-family and other non-Matroska media containers that expose FFmpeg `AVChapter` entries.
  Discovery checks the configured ffprobe path first, then a configured FFmpeg directory, then environment/PATH search directories.
  Missing or unstartable ffprobe reports a structured dependency diagnostic; successfully invoked ffprobe failures such as timeout, non-zero exit, malformed JSON, or no chapter output do not fall back to another importer.
- Matroska chapters use MKVToolNix `mkvextract` as the primary reader; ffprobe is used as fallback only when `mkvextract` cannot be located or started.
  The app reports `MatroskaMissingDependency` when absent.
  Discovery checks the migrated/configured MKVToolNix path first, then environment/PATH search directories, then platform install probes.
  Windows probes MKVToolNix uninstall registry entries for an install directory or display icon; macOS probes app bundles such as `/Applications/MKVToolNix-96.0.app/Contents/MacOS/mkvextract`.
  Linux and other Unix-like platforms use the configured path and PATH search only unless a future probe is explicitly designed.
- BDMV import uses `eac3to`; the app reports `MissingDependency` when absent.
- Legacy `.mp4`, `.m4a`, and `.m4v` fallback import remains isolated behind `IMp4ChapterReader` and uses ATL.NET (`z440.atl.core`) when ffprobe cannot be invoked.
- No separate MP4-only command-line tool or `libmp4v2` DLL is required for the default MP4 import path.
- Legacy `libmp4v2`/Knuckleball integration, external MP4 command-line experiments, and Windows `fsutil` hardlink behavior remain retired unless a future backend is explicitly designed.
- Legacy native MP4 DLLs from `Time_Shift/mp4v2` are not bundled in Avalonia publish output by default.

## Installer Strategy

The legacy NSIS/Costura packaging path is not carried forward directly. The replacement strategy is:

- publish explicit .NET CLI artifacts first;
- keep assets under `src/ChapterTool.Avalonia/Assets`;
- add an installer only after the Avalonia executable layout and native dependency policy stabilize.

## Assets And Licenses

Required app assets are packaged from `src/ChapterTool.Avalonia/Assets/**`.
Third-party licenses remain tracked in the repository and must be included in any future installer or archive artifact.
