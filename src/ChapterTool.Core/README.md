# ChapterTool.Core

A .NET library for parsing, editing, transforming, and exporting multimedia chapter files.

## Installation

```bash
dotnet add package ChapterTool.Core
```

## Features

- **Import** chapters from common chapter formats and media-container adapters: CUE, FLAC, TAK, IFO, MPLS, XPL, MP4/media containers via `IMediaChapterReader`, OGM, Matroska XML, WebVTT, plain text, Premiere markers
- **Export** chapters to multiple chapter formats: OGM Text, Matroska XML, QPFile, TimeCodes, tsMuxeR Meta, CUE, JSON, WebVTT, Celltimes, Chapter→QPFile
- **Edit** chapter data: time edit, frame edit, rename, delete, insert, reorder, shift, apply name templates
- **Transform** chapter times: Lua expression scripting, frame rate detection and conversion
- **Combine & append** chapter segments from multipart sources (MPLS/DVD)

## Quick Start

### Import Chapters

```csharp
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

var formatter = new ChapterTimeFormatter();

// MPLS playlists can expose one option per play item/angle.
IChapterImporter importer = new MplsChapterImporter();
var request = new ChapterImportRequest("/path/to/BDMV/PLAYLIST/00001.mpls");
var result = await importer.ImportAsync(request, CancellationToken.None);

if (result.Success)
{
    foreach (var group in result.Groups)
    {
        Console.WriteLine($"Source: {group.SourcePath}");
        foreach (var option in group.Options)
        {
            Console.WriteLine($"  {option.DisplayName}: {option.ChapterInfo.Chapters.Count} chapters");
        }
    }
}
```

### Export Chapters

```csharp
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Transform;

var formatter = new ChapterTimeFormatter();
var exportService = new ChapterExportService(formatter);

var options = new ChapterExportOptions(
    Format: ChapterExportFormat.Xml,
    XmlLanguage: "eng"
);

var result = exportService.Export(chapterInfo, options);
if (result.Success)
{
    File.WriteAllText($"output{result.FileExtension}", result.Content);
}
```

### Edit Chapters

```csharp
using ChapterTool.Core.Editing;

var editor = new ChapterEditingService(new ChapterTimeFormatter());

// Rename a chapter
var result = editor.Rename(info, index: 0, name: "Opening");

// Edit chapter time
result = editor.EditTime(info, index: 0, text: "00:05:30.000");

// Delete chapters
result = editor.Delete(info, indexes: new HashSet<int> { 2, 3 });

// Apply name template (one name per line)
result = editor.ApplyTemplate(info, "Opening\nMain Feature\nCredits");
```

### Transform Chapter Times

```csharp
using ChapterTool.Core.Transform;

// Frame rate detection
var fpsService = new FrameRateService();
var detected = fpsService.Detect(info, tolerance: 0.001m);

// Change frame rate
var fpsResult = ChapterFpsTransformService.ChangeFps(info, sourceFps: 23.976m, targetFps: 25m);

var luaService = new LuaExpressionScriptService();
var context = new LuaExpressionContext(chapter, index: 1, count: 10, timeSeconds: 60m, framesPerSecond: 23.976m);
var luaResult = luaService.Evaluate("t + 1.0", context);
```

## Supported Formats

### Import

| Format                           | Importer                                                          | Extensions   |
|----------------------------------|-------------------------------------------------------------------|--------------|
| CUE Sheet                        | `CueChapterImporter`                                              | `.cue`       |
| FLAC CUE                         | `FlacCueImporter`                                                 | `.flac`      |
| TAK CUE                          | `TakCueImporter`                                                  | `.tak`       |
| DVD IFO                          | `IfoChapterImporter`                                              | `.ifo`       |
| Blu-ray MPLS                     | `MplsChapterImporter`                                             | `.mpls`      |
| Blu-ray XPL                      | `XplChapterImporter`                                              | `.xpl`       |
| Media files, including MP4 / M4V | `MediaChapterImporter` with caller-supplied `IMediaChapterReader` | configurable |
| OGM Text                         | `OgmChapterImporter`                                              | `.txt`       |
| Matroska XML                     | `XmlChapterImporter`                                              | `.xml`       |
| WebVTT                           | `WebVttChapterImporter`                                           | `.vtt`       |
| Plain Text                       | `TextChapterImporter`                                             | `.txt`       |
| Premiere Markers                 | `PremiereMarkerListImporter`                                      | `.csv`       |

### Media Reader Adapters

`ChapterTool.Core` keeps media container parsing behind `IMediaChapterReader`. This keeps the core package free of native tools, shell process execution, and container-specific libraries.

Use `MediaChapterImporter` when your integration layer reads raw media-container chapter entries, including MP4 chapter metadata or data shaped like `ffprobe -show_chapters` JSON. Construct it with an `IMediaChapterReader`; optionally pass the extensions that adapter supports:

```csharp
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Media;

static ValueTask<ChapterImportResult> ImportMediaChaptersAsync(
    string path,
    IMediaChapterReader reader,
    CancellationToken cancellationToken)
{
    IChapterImporter importer = new MediaChapterImporter(
        reader,
        supportedExtensions: [".mkv", ".mp4", ".webm"]);

    return importer.ImportAsync(
        new ChapterImportRequest(path),
        cancellationToken);
}
```

The reader returns:

```csharp
ValueTask<MediaChapterReadResult> ReadAsync(string path, CancellationToken cancellationToken);
```

`MediaChapterEntry` accepts either decimal-second timestamps (`StartTime` / `EndTime`) or integer timestamps (`Start` / `End`) with a rational `TimeBase`, for example `"1/1000"`. Chapter names are read from `Tags["title"]` when present; otherwise the importer generates fallback chapter names. If the reader cannot load metadata, return `MediaChapterReadResult.Failed(code, message, details)`; the importer converts it to a `ChapterDiagnostic`.

### Export

| Format           | `ChapterExportFormat` |
|------------------|-----------------------|
| OGM Text         | `Txt`                 |
| Matroska XML     | `Xml`                 |
| QPFile           | `Qpfile`              |
| TimeCodes        | `TimeCodes`           |
| tsMuxeR Meta     | `TsMuxerMeta`         |
| CUE Sheet        | `Cue`                 |
| JSON             | `Json`                |
| WebVTT           | `WebVtt`              |
| Celltimes        | `Celltimes`           |
| Chapter → QPFile | `Chapter2Qpfile`      |

## Expression Engine

`LuaExpressionScriptService` evaluates Lua expression scripts for chapter time transforms.
Simple arithmetic can be entered without `return`, while full scripts may use `return` or define `transform(chapter)`.

- **Globals**: `t` (time in seconds), `fps` (frames per second), `index`, `count`, and `chapter`
- **Libraries**: safe Lua `math`, `string`, and `table` libraries
- **Aliases**: common math helpers such as `floor`, `ceil`, `round`, `sin`, `sqrt`, and `sign`
- **Presets**: identity, time offset, frame rounding, and half-frame shift

## Diagnostic System

All operations return diagnostics via `ChapterDiagnostic`:

```csharp
public sealed record ChapterDiagnostic(
    DiagnosticSeverity Severity,  // Info, Warning, Error
    string Code,
    string Message,
    string? Location = null,
    string? Details = null,
    IReadOnlyDictionary<string, object?>? Arguments = null);
```

## Extensibility

Implement `IChapterImporter` to add custom chapter import formats:

```csharp
public interface IChapterImporter
{
    string Id { get; }
    IReadOnlySet<string> SupportedExtensions { get; }
    ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken);
}
```

Implement `IChapterExporter` to add custom export formats, or `IMediaChapterReader` to support reading chapters from additional media container formats.

## License

GPL-3.0-or-later. See the repository `LICENSE` file for the full GPLv3 text.
