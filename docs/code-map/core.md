# Core Code Map

`src/ChapterTool.Core` owns the chapter domain model and pure business behavior.

This layer is where import normalization, chapter editing, frame/time transforms, and export formatting belong.

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
- `src/ChapterTool.Core/Diagnostics/DiagnosticSeverity.cs`

### Importing

Import contracts and format-specific parsers:

- `src/ChapterTool.Core/Importing/IChapterImporter.cs`
- `src/ChapterTool.Core/Importing/ChapterImportRequest.cs`
- `src/ChapterTool.Core/Importing/ChapterImportResult.cs`
- `src/ChapterTool.Core/Importing/ChapterLoadProgress.cs`

Important format entry points:

- Text dispatcher: `src/ChapterTool.Core/Importing/Text/TextChapterImporter.cs`
- OGM text: `src/ChapterTool.Core/Importing/Text/OgmChapterImporter.cs`
- Premiere marker CSV: `src/ChapterTool.Core/Importing/Text/PremiereMarkerListImporter.cs`
- Matroska XML: `src/ChapterTool.Core/Importing/Text/XmlChapterImporter.cs`
- WebVTT: `src/ChapterTool.Core/Importing/Text/WebVttChapterImporter.cs`
- CUE sheet parsing: `src/ChapterTool.Core/Importing/Cue/CueChapterImporter.cs`
- Embedded FLAC/TAK CUE: `src/ChapterTool.Core/Importing/Cue/FlacCueImporter.cs`, `src/ChapterTool.Core/Importing/Cue/TakCueImporter.cs`
- DVD/Blu-ray playlist parsing: `src/ChapterTool.Core/Importing/Disc/IfoChapterImporter.cs`, `src/ChapterTool.Core/Importing/Disc/MplsChapterImporter.cs`, `src/ChapterTool.Core/Importing/Disc/MplsPlaylistFile.cs`, `src/ChapterTool.Core/Importing/Disc/XplChapterImporter.cs`
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
- `src/ChapterTool.Core/Transform/LuaExpressionScriptService.cs`
- `src/ChapterTool.Core/Transform/ExpressionAuthoringService.cs`
- `src/ChapterTool.Core/Transform/ChapterTimeFormatter.cs`
- `src/ChapterTool.Core/Transform/ChapterRounding.cs`

### Exporting

Output projection and format serialization:

- `src/ChapterTool.Core/Exporting/ChapterExportService.cs`
- `src/ChapterTool.Core/Exporting/ChapterExportOptions.cs`
- `src/ChapterTool.Core/Exporting/ChapterExportFormat.cs`
- `src/ChapterTool.Core/Exporting/ChapterExportFormats.cs`
- `src/ChapterTool.Core/Exporting/ChapterOutputProjectionService.cs`
- `src/ChapterTool.Core/Exporting/ChapterConversionService.cs`
- `src/ChapterTool.Core/Exporting/XmlChapterLanguageCatalog.cs`

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
- Lua evaluation: `src/ChapterTool.Core/Transform/LuaExpressionScriptService.cs`
- time parse/format bugs: `src/ChapterTool.Core/Transform/ChapterTimeFormatter.cs`

### Export behavior

Start with:

- projection before serialization: `src/ChapterTool.Core/Exporting/ChapterOutputProjectionService.cs`
- format-specific serialization: `src/ChapterTool.Core/Exporting/ChapterExportService.cs`
- text-to-QP/celltimes conversion: `src/ChapterTool.Core/Exporting/ChapterConversionService.cs`
