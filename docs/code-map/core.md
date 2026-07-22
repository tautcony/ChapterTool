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
- DVD/Blu-ray playlist parsing uses `IfoChapterImporter.cs`, `MplsChapterImporter.cs`, `MplsPlaylistFile.cs`, and `XplChapterImporter.cs` under `src/ChapterTool.Core/Importing/Disc/`.
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

- `src/ChapterTool.Wasm` is a browser WebAssembly workspace. It uses `Microsoft.NET.Sdk.BlazorWebAssembly`.
- `Services/WasmWorkspace` and `Pages/Home.razor` provide the load, grid, options, and save zones.
- `WasmWorkspace` owns byte-based load, reload, and append state. It also owns selection actions, projection and export orchestration, diagnostics, activity logs, and localized status strings.
- Core editing, segment, projection, and export services remain the behavior owners.
- Browser settings use the Avalonia-shaped `schemaVersion`/`application`/`theme`/`font` document with schema `1`. `wwwroot/js/download.js` stores this document in `localStorage`. `Services/WasmLocalizer` stores UI strings.
- `tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs` — focused browser-workspace behavior coverage for reload, append gating, templates, selection/zones/delete, forward shifting, preview/save parity, auto naming, and localization refresh.
- Deployed to GitHub Pages by `.github/workflows/github-pages.yml` (`https://tautcony.github.io/ChapterTool/`).

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
