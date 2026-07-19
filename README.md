# ChapterTool

[![License: GPL v3](https://img.shields.io/github/license/tautcony/chaptertool.svg)](LICENSE)
[![.NET 10 CI](https://github.com/tautcony/ChapterTool/actions/workflows/dotnet-ci.yml/badge.svg)](https://github.com/tautcony/ChapterTool/actions/workflows/dotnet-ci.yml)
[![NuGet](https://img.shields.io/nuget/v/ChapterTool.Core?logo=nuget)](https://www.nuget.org/packages/ChapterTool.Core/)
[![NuGet downloads](https://img.shields.io/nuget/dt/ChapterTool.Core?logo=nuget)](https://www.nuget.org/packages/ChapterTool.Core/)
[![GitHub Pages](https://github.com/tautcony/ChapterTool/actions/workflows/github-pages.yml/badge.svg)](https://github.com/tautcony/ChapterTool/actions/workflows/github-pages.yml)
[![GitHub downloads](https://img.shields.io/github/downloads/tautcony/chaptertool/total.svg)](https://github.com/tautcony/ChapterTool/releases)
[![WASM Demo](https://img.shields.io/badge/demo-GitHub%20Pages-blue)](https://tautcony.github.io/ChapterTool/)

ChapterTool is a cross-platform Avalonia desktop chapter editor for importing, adjusting, combining, and exporting chapter lists from text, disc playlist, and media container sources.

## Features

- Import chapter data from text files, disc playlist formats, BDMV folders, and media containers.
- Edit chapter names and timestamps in a cross-platform Avalonia UI.
- Apply time adjustments with a Lua expression/script editor that supports diagnostics, completion, and syntax highlighting.
- Calculate frame information from chapter times and frame rate settings.
- Combine supported multi-segment sources such as MPLS and IFO.
- Export chapters as `.txt`, `.xml`, `.qpf`, `.TimeCodes.txt`, `.TsMuxeR_Meta.txt`, `.cue`, `.json`, `.vtt`, and Celltimes output.
- Use the CLI to list supported formats, inspect selectable chapter groups, and convert chapter sources without launching the desktop UI.

## NuGet Package

The reusable chapter parsing and transformation library is published as [`ChapterTool.Core`](https://www.nuget.org/packages/ChapterTool.Core/) for .NET 8, .NET 9, and .NET 10 applications, including browser WebAssembly hosts.

Install it from NuGet.org:

```bash
dotnet add package ChapterTool.Core
```

Tagged releases are also published to [GitHub Packages](https://github.com/tautcony/ChapterTool/pkgs/nuget/ChapterTool.Core) by the [package workflow](.github/workflows/nuget-publish.yml). The workflow publishes a version when a `v*` tag is pushed, and can also be started manually from the Actions tab.

## Supported Import Sources

- OGM-style text chapters: `.txt`
- Adobe Premiere Pro chapter marker lists: `.csv`, and detected marker tables in `.txt`
- Matroska chapter XML: `.xml`
- WebVTT chapter cues: `.vtt`
- Cue sheets and embedded cues: `.cue`, `.flac`, `.tak`
- Blu-ray playlists: `.mpls`
- Blu-ray `BDMV` folders through `eac3to`
- DVD IFO files: `.ifo`
- HD-DVD playlists: `.xpl`
- Matroska containers through `mkvextract`, with ffprobe fallback when the tool cannot be invoked: `.mkv`, `.mka`, `.mks`, `.webm`
- MP4/QuickTime/media files through ffprobe: `.mp4`, `.m4a`, `.m4v`, `.mov`, `.qt`, `.3gp`, `.3g2`, `.asf`, `.wmv`, `.wma`, `.mp3`, `.aac`, `.ogg`, `.oga`, `.ogv`, `.opus`, `.wav`, `.nut`, `.aa`, `.aax`, `.ffmetadata`, `.ffmeta`

## Requirements

- .NET 10 runtime for framework-dependent release builds.
- .NET 10 SDK for building from source.
- `ffprobe` from FFmpeg for media-container chapter import.
- `mkvextract` from MKVToolNix for primary Matroska chapter extraction.
- `eac3to` for importing Blu-ray `BDMV` folders.

External tool paths can be configured in the app settings. ChapterTool also searches common configured paths and platform tool locations where supported.

## Command Line

The Avalonia executable also exposes maintained CLI workflows. Run these from a published artifact or from the project with `dotnet run --project src/ChapterTool.Avalonia --`.

```powershell
ChapterTool.Avalonia formats
ChapterTool.Avalonia inspect input.mpls
ChapterTool.Avalonia convert input.xml --format txt --output chapters.txt
ChapterTool.Avalonia convert input.xml --format vtt --stdout
ChapterTool.Avalonia load input.xml
```

`formats` lists the stable CLI import/export surface. `inspect` reports imported groups, selectable options, and diagnostics. `convert` supports file output, stdout output, explicit group/option selection, XML language, CUE source file name, and frame-rate override. GUI-only expression transforms are intentionally not applied by CLI conversion.

## Build And Test

Restore, build, and test the current solution:

```powershell
dotnet restore ChapterTool.Avalonia.slnx
dotnet build ChapterTool.Avalonia.slnx --no-restore
dotnet test ChapterTool.Avalonia.slnx --no-restore
```

The CI workflow is `.github/workflows/dotnet-ci.yml` and runs on Linux with .NET 10, FFmpeg, and MKVToolNix.

To collect test coverage and generate a browsable HTML report:

```bash
./scripts/test-coverage.sh
```

The script runs the four test projects sequentially, excludes generated `*.g.cs` files through `scripts/coverage.runsettings`, then writes Cobertura XML and the HTML report under `artifacts/coverage`. HTML generation requires the ReportGenerator global tool:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

Use `./scripts/test-coverage.sh -SkipHtml` when only the XML coverage files are needed.

### Browser WASM demo (GitHub Pages)

`samples/ChapterTool.Core.WasmDemo` is a Blazor WebAssembly host for `ChapterTool.Core` (load / chapter grid / save). It is published by `.github/workflows/github-pages.yml` to:

**https://tautcony.github.io/ChapterTool/**

Enable once: **Settings → Pages → Source → GitHub Actions**, then run the **Deploy WASM Demo (GitHub Pages)** workflow (or push to `master` under the watched paths).

## Publish

Use the publish helpers for local artifacts:

```bash
./scripts/publish.sh -Runtime linux-x64
./scripts/publish.sh -Runtime osx-arm64
./scripts/publish.sh -Runtime win-x64 -SelfContained
```

`scripts/publish.ps1` is available for Windows publishing only:

```powershell
./scripts/publish.ps1 -Runtime win-x64
./scripts/publish.ps1 -Runtime win-x64 -SelfContained
```

Framework-dependent artifacts are written under `artifacts/publish/framework-dependent/<runtime>`. Self-contained artifacts are written under `artifacts/publish/self-contained/<runtime>`.

The GitHub Actions publish job currently builds single-file framework-dependent artifacts for `win-x64`, `linux-x64`, and `osx-arm64`; the macOS job also packages the app as a `.dmg`.

To publish a GitHub Release from the artifacts produced by a tag's successful CI run, open the `Publish GitHub Release` workflow, choose `Run workflow`, and enter the tag. The workflow packages each desktop runtime, extracts the corresponding `ChangeLog.md` section, and creates or updates the release automatically.

## Project Layout

- `src/ChapterTool.Core`: chapter models, transformations, import contracts, and exporters.
- `src/ChapterTool.Infrastructure`: external tool discovery, process execution, settings, and infrastructure-backed importers.
- `src/ChapterTool.Avalonia`: desktop UI and runtime composition.
- `tests/`: Core, Infrastructure, and Avalonia test projects.
- `openspec/specs/`: current behavior specifications.
- `openspec/changes/`: active and archived OpenSpec changes.

## Thanks

- [Chapters file time Editor](https://www.nmm-hd.org/newbbs/viewtopic.php?f=16&t=24)
- [BD Chapters MOD](https://www.nmm-hd.org/newbbs/viewtopic.php?f=16&t=517)
- [gMKVExtractGUI](http://sourceforge.net/projects/gmkvextractgui/)
- [Chapter Grabber](http://jvance.com/pages/ChapterGrabber.xhtml)
- [MKVToolNix](https://mkvtoolnix.download/)
- [libbluray](https://www.videolan.org/developers/libbluray.html)
- [BDedit](http://pel.hu/bdedit/)
- [Knuckleball](https://github.com/jimevans/knuckleball)
- [BluRay](https://github.com/lerks/BluRay)
- [IfoEdit](http://www.ifoedit.com/index.html)

## License

Distributed under the GPLv3+ license. See [LICENSE](LICENSE) for details.
