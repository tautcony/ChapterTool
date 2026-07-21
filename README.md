# ChapterTool

[![License: GPL v3](https://img.shields.io/github/license/tautcony/chaptertool.svg)](LICENSE)
[![.NET 10 CI](https://github.com/tautcony/ChapterTool/actions/workflows/dotnet-ci.yml/badge.svg)](https://github.com/tautcony/ChapterTool/actions/workflows/dotnet-ci.yml)
[![NuGet](https://img.shields.io/nuget/v/ChapterTool.Core?logo=nuget)](https://www.nuget.org/packages/ChapterTool.Core/)
[![GitHub downloads](https://img.shields.io/github/downloads/tautcony/chaptertool/total.svg)](https://github.com/tautcony/ChapterTool/releases)
[![WASM](https://img.shields.io/badge/wasm-GitHub%20Pages-blue)](https://tautcony.github.io/ChapterTool/)

ChapterTool is a cross-platform chapter editor for desktop and browser. It imports chapter data, edits names and times, applies time transforms, and exports the result.

## Features

- Import text, XML, WebVTT, CUE, Blu-ray, DVD, HD-DVD, Matroska, and other media chapter sources.
- Combine supported multi-segment sources such as MPLS and IFO.
- Edit chapter names and timestamps.
- Calculate frame numbers from chapter times and frame-rate settings.
- Apply Lua time transforms with diagnostics, completion, and syntax highlighting.
- Export TXT, XML, QPFile, TimeCodes, TsMuxeR, CUE, JSON, WebVTT, and Celltimes files.

The command-line interface (CLI) can list formats, inspect imported groups, and convert sources without opening the desktop app.

## Requirements

- .NET 10 runtime for framework-dependent releases.
- .NET 10 SDK to build from source.
- `ffprobe` from FFmpeg for media-container chapters.
- `mkvextract` from MKVToolNix for Matroska chapters.
- `eac3to` for Blu-ray `BDMV` folders.

Configure external tool paths in the application settings. ChapterTool also searches supported platform locations.

## Install The Core Library

[`ChapterTool.Core`](https://www.nuget.org/packages/ChapterTool.Core/) provides chapter parsing, transformation, and export APIs for .NET 8, .NET 9, .NET 10, and browser WebAssembly applications.

```bash
dotnet add package ChapterTool.Core
```

## CLI Examples

Run the CLI from a published application or with `dotnet run`:

```powershell
dotnet run --project src/ChapterTool.Avalonia -- formats
dotnet run --project src/ChapterTool.Avalonia -- inspect input.mpls
dotnet run --project src/ChapterTool.Avalonia -- convert input.xml --format txt --output chapters.txt
dotnet run --project src/ChapterTool.Avalonia -- convert input.xml --format vtt --stdout
```

`formats` lists supported import and export formats. `inspect` reports groups, selectable options, and diagnostics. `convert` supports file output, standard output, group selection, XML language, CUE source names, and frame-rate overrides.

The CLI can apply the same Lua expression transforms as the GUI with `--expression` or `--expression-preset`. Use `--frame-rate` when the input does not provide a valid frame rate.

## Browser App

The Blazor WebAssembly app runs `ChapterTool.Core` in the browser. It supports byte-based imports, chapter editing, templates, multi-selection, previews, exports, drag and drop, settings, and `en-US`, `zh-CN`, and `ja-JP` localization.

Open the deployed app at [tautcony.github.io/ChapterTool](https://tautcony.github.io/ChapterTool/).

Run it locally:

```bash
dotnet run --project src/ChapterTool.Wasm/ChapterTool.Wasm.csproj --launch-profile ChapterTool.Wasm
```

The browser app does not run desktop tools or access local files after import. See [the WASM README](src/ChapterTool.Wasm/README.md) for its boundaries and deployment details.

## Build And Test

Use the main solution for local development:

```bash
dotnet restore ChapterTool.Avalonia.slnx
dotnet build ChapterTool.Avalonia.slnx --no-restore
dotnet test ChapterTool.Avalonia.slnx --no-restore
```

Run coverage with `./scripts/test-coverage.sh`. The script writes reports to `artifacts/coverage`.

Publish a local desktop artifact:

```bash
./scripts/publish.sh -Runtime linux-x64
./scripts/publish.sh -Runtime osx-arm64
./scripts/publish.sh -Runtime win-x64 -SelfContained
```

Use `scripts/publish.ps1` on Windows.

## Project Layout

| Path | Responsibility |
| --- | --- |
| `src/ChapterTool.Core` | Chapter models, importers, transformations, and exporters |
| `src/ChapterTool.Infrastructure` | External tools, process execution, settings, and platform services |
| `src/ChapterTool.Avalonia` | Desktop UI, CLI, and runtime composition |
| `src/ChapterTool.Wasm` | Browser host for `ChapterTool.Core` |
| `tests/` | Core, Infrastructure, Avalonia, and Headless Avalonia tests |

Use [the code map](docs/code-map/README.md) to find module entry points and test ownership.

## License

ChapterTool is distributed under the GPLv3+ license. See [LICENSE](LICENSE).
