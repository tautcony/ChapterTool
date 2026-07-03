## Why

ChapterTool is currently a WinForms/.NET Framework application whose UI event handlers, parsers, exporters, configuration, and platform integration are tightly coupled. The Avalonia + .NET 10 rewrite needs a spec-driven split that preserves the existing Time_Shift behavior while creating testable Core, Infrastructure, and UI boundaries.

## What Changes

- Rebuild the application as an SDK-style .NET 10 solution with Avalonia UI, UI-independent Core services, Infrastructure adapters, and focused test projects.
- Preserve existing chapter workflows: load, inspect, edit, transform, preview, save, and auxiliary tools.
- Port supported importers: OGM/TXT, XML/Matroska XML, Matroska via mkvextract, WebVTT, MPLS, IFO/DVD, XPL, BDMV via eac3to, MP4 via libmp4v2, CUE, FLAC embedded CUE, and TAK embedded CUE.
- Preserve supported exporters: TXT/OGM, Matroska XML, QPF, TimeCodes, tsMuxeR meta, CUE, and JSON.
- Replace WinForms dialogs, controls, resources, registry access, external process execution, and update logic with Avalonia views and injectable services.
- Add TDD coverage for every migrated capability, using existing Time_Shift_Test samples as compatibility fixtures.
- **BREAKING**: The new implementation will not preserve WinForms project structure, Designer resources, .NET Framework runtime configuration, or direct UI control coupling.

## Capabilities

### New Capabilities

- `avalonia-ui-shell`: Main Avalonia window, ViewModel command surface, keyboard shortcuts, drag/drop, chapter grid interaction, clip selection, context menus, status/progress presentation, and auxiliary command entry points.
- `chapter-core-transform-export`: UI-independent chapter models, chapter groups, time and frame-rate conversion, expression transforms, chapter editing operations, naming/template behavior, zones/forward translation, and seven export formats.
- `chapter-importers-text-xml-matroska-vtt`: OGM/TXT, WebVTT, XML/Matroska XML, and Matroska container import through mkvextract with structured diagnostics.
- `disc-playlist-media-importers`: MPLS, IFO/DVD, XPL, BDMV/eac3to, and MP4/libmp4v2 importers, including multi-segment selection, combine, append, dependency handling, and related media references.
- `cue-flac-tak-import-export`: CUE file import, FLAC/TAK embedded CUE extraction, CUE parsing compatibility, and CUE export through the common exporter contract.
- `supporting-ui-platform-services`: Preview/log/color/about/updater windows, settings and legacy migration, localization, logging, dialogs, clipboard, shell, process runner, tool locators, native dependency resolution, Windows-only services, and resources.
- `tests-build-distribution-assets`: .NET 10 solution layout, xUnit test structure, fixture migration, CI, packaging, versioning, assets, native DLL distribution, installer strategy, and license coverage.

### Modified Capabilities

- None. The OpenSpec repository has no existing active specs; this rewrite introduces the initial specification set.

## Impact

- Adds new OpenSpec artifacts under `openspec/changes/rewrite-avalonia-dotnet10`.
- Adds a tracked spec split document under `docs/` for module ownership, dependency ordering, and parallel apply guidance.
- Future implementation will add SDK-style projects such as `ChapterTool.Core`, `ChapterTool.Infrastructure`, `ChapterTool.Avalonia`, and corresponding test projects.
- Existing WinForms sources remain compatibility references during the rewrite and should not be edited unless a deliberate migration task requires it.
- External dependencies and assets affected include Avalonia, .NET 10, xUnit or equivalent test tooling, MKVToolNix/eac3to/libmp4v2 adapters, resource files, native DLLs, and Windows packaging scripts.
