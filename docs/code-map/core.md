# Core Code Map

`src/ChapterTool.Core` owns the chapter domain model and pure business behavior.

This layer contains import normalization, chapter editing, frame/time transforms, and export formatting.

Use ASD-STE100 principles in this document. Keep each sentence short and direct. Keep code identifiers exact.

## Ownership

### Models

Canonical data contracts shared across the pipeline:

- `src/ChapterTool.Core/Models/Chapter.cs`
- `src/ChapterTool.Core/Models/ChapterSet.cs`
- `src/ChapterTool.Core/Models/ChapterImportFormat.cs`
- `src/ChapterTool.Core/Models/ChapterImportFormats.cs`
- `src/ChapterTool.Core/Models/ChapterImportSource.cs`
- `src/ChapterTool.Core/Models/ChapterImportEntry.cs`
- `src/ChapterTool.Core/Models/MediaFileReference.cs`

`ChapterSet` is the main unit passed between import, edit, transform, and export flows.

### Diagnostics

Shared diagnostic contracts:

- `src/ChapterTool.Core/Diagnostics/ChapterDiagnostic.cs`
- `src/ChapterTool.Core/Diagnostics/ChapterDiagnosticCode.cs`
- `src/ChapterTool.Core/Diagnostics/ChapterDiagnosticSource.cs`
- `src/ChapterTool.Core/Diagnostics/ChapterDiagnosticReason.cs`
- `src/ChapterTool.Core/Diagnostics/ChapterDiagnosticCodeExtensions.cs`
- `src/ChapterTool.Core/Diagnostics/DiagnosticSeverity.cs`

`ChapterDiagnostic.Code` combines `ChapterDiagnosticSource` and `ChapterDiagnosticReason`. `DisplayCode` renders the stable localization and log code as `Source.Reason`.

### Importing

Import contracts and format-specific parsers:

- `src/ChapterTool.Core/Importing/IChapterImporter.cs`
- `src/ChapterTool.Core/Importing/ChapterImportRequest.cs`
- `src/ChapterTool.Core/Importing/ChapterContentService.cs`: byte-based host adapter for import and export
- `src/ChapterTool.Core/Importing/ChapterImportResult.cs`
- `src/ChapterTool.Core/Importing/ChapterImportProgress.cs`

Important format entry points:

- Text dispatcher: `src/ChapterTool.Core/Importing/Text/TextChapterImporter.cs`
- OGM text: `src/ChapterTool.Core/Importing/Text/OgmChapterImporter.cs`
- Premiere marker CSV: `src/ChapterTool.Core/Importing/Text/PremiereMarkerListImporter.cs`
- Matroska XML: `src/ChapterTool.Core/Importing/Text/XmlChapterImporter.cs`
- Secure XML loading policy: `src/ChapterTool.Core/Importing/SecureXmlLoader.cs` (DTD and external entity resolution prohibited for XML importers)
- WebVTT: `src/ChapterTool.Core/Importing/Text/WebVttChapterImporter.cs`
- CUE sheet parsing: `src/ChapterTool.Core/Importing/Cue/CueChapterImporter.cs`
- Embedded FLAC/TAK CUE: `src/ChapterTool.Core/Importing/Cue/FlacCueImporter.cs`, `src/ChapterTool.Core/Importing/Cue/TakCueImporter.cs`
- DVD/Blu-ray playlist parsing uses `IfoChapterImporter.cs`, `MplsChapterImporter.cs`, split `Mpls*.cs` playlist types, and `XplChapterImporter.cs` under `src/ChapterTool.Core/Importing/Disc/`.
- Native BDMV navigation uses typed INDEX references under `Disc/Index/`, bounded MovieObject parsing and HDMV resolution under `Disc/MovieObject/`, and BDJO accessible-playlist parsing under `Disc/Bdjo/`.
- `HdmvNavigationResolver.ResolveProfileVariants` creates bounded player profiles only for Player Status Registers (PSRs) that MovieObject commands read. It merges playlist events in stable profile order.
- `IndexFile.ExtensionData` exposes validated UHD/HDR extension 3.1 metadata. `IndexTitleEntry` exposes prohibited and hidden access state.
- `ClpiFile.LookupPacket` uses STC and CPI EP Map records for bounded source-packet lookup. The lookup does not change MPLS chapter time.
- `BdjoFile` parses terminal, cache, application, key-interest, file-access, and accessible-playlist records. It never executes BD-J code.
- `src/ChapterTool.Core/Importing/Disc/MplsAggregateProjection.cs` builds one complete chapter projection for each BDMV playlist.
- `BinaryReadExtensions.cs` defines generic exact-read ceilings.
- `MplsParseLimits.cs` defines semantic MPLS limits.
- `MplsBoundedStream.cs` enforces each declared parent-container byte budget while it parses nested entries.
- Media normalization contract: `src/ChapterTool.Core/Importing/Media/MediaChapterImporter.cs`, `src/ChapterTool.Core/Importing/Media/IMediaChapterReader.cs`

### Editing

In-memory chapter mutations:

- `src/ChapterTool.Core/Editing/IChapterEditingService.cs`
- `src/ChapterTool.Core/Editing/ChapterEditingService.cs`
- `src/ChapterTool.Core/Editing/ChapterSegmentService.cs`
- `src/ChapterTool.Core/Editing/ChapterEditResult.cs`

### Session (shared host kernel)

Host-agnostic interactive session state shared by Avalonia and WASM:

- `src/ChapterTool.Core/Session/ClipSession.cs` — split/combined clip sessions and pure transitions
- `src/ChapterTool.Core/Session/ChapterWorkspace.cs` — host-neutral session state, edit buffer, revision and session-token commit rules for Avalonia and WASM
- `src/ChapterTool.Core/Session/ProjectionState.cs` — naming, order shift, expression fields, projection cache
- `src/ChapterTool.Core/Session/ExportPreferences.cs` — export format, language, encoding, BOM, save directory

Primary tests: `tests/ChapterTool.Core.Tests/Session/`

### Disc MPLS types

MPLS playlist records are split by type under `src/ChapterTool.Core/Importing/Disc/`:

- `MplsPlaylistFile.cs` — top-level file and `Read`
- `MplsPlayList.cs`, `MplsPlayItem.cs`, `MplsPlayListMark.cs`, `MplsExtensionData.cs`, and related stream tables

### Transform

Frame/time and expression logic:

- `src/ChapterTool.Core/Transform/FrameRateService.cs`
- `src/ChapterTool.Core/Transform/ChapterFpsTransformService.cs`
- `src/ChapterTool.Core/Transform/ChapterExpressionService.cs`
- `src/ChapterTool.Core/Transform/Expressions/ChapterExpressionEngine.cs`
- `src/ChapterTool.Core/Transform/Expressions/Lua/LuaExpressionScriptService.cs`
- `src/ChapterTool.Core/Transform/ExpressionAuthoringService.cs`
- `src/ChapterTool.Core/Transform/ChapterTimeFormatter.cs`
- `src/ChapterTool.Core/Transform/ChapterRounding.cs`

`ChapterExpressionService` evaluates only non-separator chapters. Each expression context contains the ordered non-separator chapter snapshot. The Lua engine exposes this snapshot as the one-based `chapters` array. The `chapter` value equals `chapters[index]`.

### Exporting

Output projection and format serialization:

- `src/ChapterTool.Core/Exporting/ChapterExportService.cs`
- `src/ChapterTool.Core/Exporting/SaveFormatOption.cs`: host-facing export format metadata
- `src/ChapterTool.Core/Exporting/ChapterExportOptions.cs`
- `src/ChapterTool.Core/Exporting/ChapterExportFormat.cs`
- `src/ChapterTool.Core/Exporting/ChapterExportFormats.cs`
- `src/ChapterTool.Core/Exporting/OutputTextEncoding.cs`
- `src/ChapterTool.Core/Exporting/ChapterOutputProjectionService.cs`
- `src/ChapterTool.Core/Exporting/ChapterConversionService.cs`
- `src/ChapterTool.Core/Exporting/XmlChapterLanguageCatalog.cs`

## Browser / WebAssembly

`ChapterTool.Core` is a pure managed library. `SupportedPlatform` includes `browser`. The library targets `net8.0;net9.0;net10.0`. Desktop hosts and browser WebAssembly hosts can use it.

WASM integration rules:

- Prefer `ChapterImportRequest.Content` (streams) or importer `ImportText` helpers; browser sandboxes do not provide a real filesystem for path-only imports.
- Export is already content-based (`ChapterExportResult.Content`); no disk writes are required.
- Expression evaluation uses managed Lua (`LuaCSharp`) and does not require native runtimes.

Browser host:

- `src/ChapterTool.Wasm` is the Blazor WebAssembly browser app. It uses `Microsoft.NET.Sdk.BlazorWebAssembly`.
- `src/ChapterTool.Wasm/Pages/Home.razor` is the browser workspace page.
- `src/ChapterTool.Wasm/Services/WasmWorkspace.cs` owns buffered load, reload, append, selection, projection, export orchestration, diagnostics, activity logs, and localized status strings.
- `WasmWorkspace` uses Core session and service types such as `ChapterWorkspace`, editing, segment, projection, and export services.
- Browser localization uses embedded JSON resources under `src/ChapterTool.Wasm/Resources/Locales/` through `WasmLocalizer`.
- Browser settings use the `WasmSettings` document with `schemaVersion`/`application`/`theme`/`font` fields. The host stores settings in browser storage through the workspace path.
- `tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs` covers workspace load, clip session, template, and export behavior.
- GitHub Pages deploys the app through `.github/workflows/github-pages.yml` (`https://tautcony.github.io/ChapterTool/`).

## Feature Lookup

### Import behavior

Start in the matching importer under `Importing/`.

Use these shortcuts:

- `.txt` source detection and dispatch: `Importing/Text/TextChapterImporter.cs`
- disc binary parsing: `Importing/Disc/MplsPlaylistFile.cs` or the matching disc importer
- media chapter normalization after raw reader output: `Importing/Media/MediaChapterImporter.cs`

### Chapter row editing

Start with:

- `src/ChapterTool.Core/Editing/ChapterEditingService.cs`

For multi-part behavior, segment combining, or append flows:

- `src/ChapterTool.Core/Editing/ChapterSegmentService.cs`

### Frame rate and time transforms

Start with:

- detection: `src/ChapterTool.Core/Transform/FrameRateService.cs`
- FPS conversion: `src/ChapterTool.Core/Transform/ChapterFpsTransformService.cs`
- expression-driven rewrites: `src/ChapterTool.Core/Transform/ChapterExpressionService.cs`
- expression engine contract: `src/ChapterTool.Core/Transform/Expressions/ChapterExpressionEngine.cs`
- Lua expression engine: `src/ChapterTool.Core/Transform/Expressions/Lua/LuaExpressionScriptService.cs`
- time parse/format bugs: `src/ChapterTool.Core/Transform/ChapterTimeFormatter.cs`

### Export behavior

Start with:

- projection before serialization: `src/ChapterTool.Core/Exporting/ChapterOutputProjectionService.cs`
- format-specific serialization: `src/ChapterTool.Core/Exporting/ChapterExportService.cs`
- supported file encodings, display names, BOM-aware encoders, and XML encoding names: `src/ChapterTool.Core/Exporting/OutputTextEncoding.cs`
- text-to-QP/celltimes conversion: `src/ChapterTool.Core/Exporting/ChapterConversionService.cs`
