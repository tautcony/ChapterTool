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
- `src/ChapterTool.Core/Models/ReferencedMediaFile.cs`

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
- DVD/Blu-ray playlist parsing uses `IfoChapterImporter.cs`, `MplsChapterImporter.cs`, `MplsPlaylistProjection.cs`, split `Mpls*.cs` playlist types, and `XplChapterImporter.cs` under `src/ChapterTool.Core/Importing/Disc/`.
- `MplsPlaylistProjection` supplies shared chapter, clip-name, frame-rate, duration, and media-reference values to direct MPLS and BDMV import.
- BDMV navigation uses typed INDEX references under `Disc/Index/`, bounded MovieObject parsing and HDMV resolution under `Disc/MovieObject/`, and BDJO accessible-playlist parsing under `Disc/Bdjo/`.
- `HdmvNavigationResolver.ResolveProfileVariants` creates bounded player profiles only for Player Status Registers (PSRs) that MovieObject commands read. It merges playlist events in stable profile order.
- `HdmvNavigationResolver` routes normal `SET` options through `SetOperationResults` and `SetSystem` options through `SetSystemOperations`. Unknown options are explicit no-ops.
- `IndexFile.ExtensionData` exposes validated UHD/HDR extension 3.1 metadata. `IndexTitleEntry` exposes prohibited and hidden access state.
- `ClpiFile.LookupPacket` uses STC and CPI EP Map records for bounded source-packet lookup. The lookup does not change MPLS chapter time.
- `BdjoFile` parses terminal, cache, application, key-interest, file-access, and accessible-playlist records. It never executes BD-J code.
- `src/ChapterTool.Core/Importing/Disc/MplsAggregateProjection.cs` builds one complete chapter projection for each BDMV playlist.
- `src/ChapterTool.Core/Models/ChapterImportDisplay.cs` supplies the semantic display name and chapter count used by desktop and browser selectors.
- `BinaryReadExtensions.cs` defines generic exact-read ceilings.
- `MplsParseLimits.cs` defines semantic MPLS limits.
- `MplsBoundedStream.cs` enforces each declared parent-container byte budget while it parses nested entries.
- Media normalization contract: `src/ChapterTool.Core/Importing/Media/MediaChapterImporter.cs`, `src/ChapterTool.Core/Importing/Media/IMediaChapterReader.cs`

### libbluray compatibility map

`libbluray/` is a vendored C reference snapshot at commit `ea3e318b` (the `hdmv: fix INSN_BC instruction` change). ChapterTool does not link to it at runtime. The managed parsers and resolver reproduce the bounded BDMV behavior that the importer needs.

| libbluray reference | ChapterTool implementation | Boundary or difference |
| --- | --- | --- |
| `libbluray/src/libbluray/bdnav/index_parse.c`, `index_data.h`, `extdata_parse.c` | `src/ChapterTool.Core/Importing/Disc/Index/IndexFile.cs`, `IndexIndexes.cs`, `IndexTitleEntry.cs`, `IndexExtensionData.cs` | Both parse `index.bdmv`, title object references, access flags, and extension records. ChapterTool validates lengths and addresses with managed limits. |
| `libbluray/src/libbluray/bdnav/mpls_parse.c`, `mpls_data.h` | `src/ChapterTool.Core/Importing/Disc/Mpls*.cs`, `MplsBoundedStream.cs`, `MplsParseLimits.cs` | Both parse playlist, play-item, mark, stream, and extension records. ChapterTool projects chapters and media references for import. |
| `libbluray/src/libbluray/bdnav/clpi_parse.c`, `clpi_data.h` | `src/ChapterTool.Core/Importing/Disc/Clpi/` | Both use STC and EP-map data for packet lookup. ChapterTool keeps chapter time based on MPLS marks. |
| `libbluray/src/libbluray/hdmv/mobj_parse.c`, `mobj_data.h` | `src/ChapterTool.Core/Importing/Disc/MovieObject/MovieObjectModels.cs`, `MovieObjectParseLimits.cs` | Both decode the MOBJ header, object flags, 12-byte instructions, and operands. ChapterTool adds bounded stream and count validation. |
| `libbluray/src/libbluray/hdmv/hdmv_vm.c`, `hdmv_insn.h` | `src/ChapterTool.Core/Importing/Disc/MovieObject/HdmvNavigation.cs` | ChapterTool implements bounded playlist/control navigation, PSR/GPR operands, calls, and deterministic profile variants. It emits structured evidence instead of driving playback. |
| `INSN_BC` in `hdmv_vm.c` | `HdmvNavigationResolver.Compare` option `1` | The managed predicate is `(source & ~destination) == 0`. This matches the corrected native condition `!!(src & ~dst)` as used by the native VM's skip-next-instruction convention. |
| `libbluray/src/libbluray/bdj/bdjo_parse.c`, `bdjo_data.h` | `src/ChapterTool.Core/Importing/Disc/Bdjo/BdjoModels.cs` | Both parse BDJO metadata and accessible playlists. ChapterTool reports dynamic BD-J selection as unsupported and never executes Java/Xlets. |
| `libbluray/src/libbluray/bdnav/navigation.c`, `bdmv_parse.c` | `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvImporter.cs`, `BdmvSourceLayout.cs`, `BdmvPlaylistScanner.cs` | Both combine disc metadata, INDEX references, MovieObject/BDJO navigation, and playlist discovery. ChapterTool uses navigation as evidence and falls back to a bounded playlist scan. |

When updating BDMV behavior, inspect the native reference and the managed row in this table together. Update the row when ownership, entry points, or an intentional compatibility boundary changes.

### Editing

In-memory chapter mutations:

- `src/ChapterTool.Core/Editing/IChapterEditingService.cs`
- `src/ChapterTool.Core/Editing/ChapterEditingService.cs`
- `src/ChapterTool.Core/Editing/ChapterEditingOptions.cs`
- `src/ChapterTool.Core/Editing/ChapterSegmentService.cs`
- `src/ChapterTool.Core/Editing/ChapterEditResult.cs`

`ChapterEditingOptions` controls delete-rows timing (`Preserve` or `Normalize`) and frame display (`Round` or `DecimalPlaces` with one to six places). `IChapterEditingService.Delete` applies the delete-rows timing mode.

### Session (shared host kernel)

Host-agnostic interactive session state shared by Avalonia and WASM:

- `src/ChapterTool.Core/Session/ClipSession.cs` — split/combined clip sessions and pure transitions
- `src/ChapterTool.Core/Session/ChapterWorkspace.cs` — host-neutral session state, edit buffer, revision and session-token commit rules for Avalonia and WASM
- `src/ChapterTool.Core/Session/ProjectionState.cs` — naming, order shift, expression fields, projection cache
- `src/ChapterTool.Core/Session/ExportPreferences.cs` — export format, language, encoding, BOM, save directory
- `src/ChapterTool.Core/Session/ChapterSourceDocument.cs` — host-neutral chapter source identity (`LocalPathChapterSource`, `BufferedChapterSource`)

Primary tests: `tests/ChapterTool.Core.Tests/Session/`

### Boundaries and localization

- `src/ChapterTool.Core/Boundaries/PortableInputPolicy.cs` — shared 64 MiB byte budget and bounded stream copy for portable hosts
- `src/ChapterTool.Core/Importing/PortableInputReader.cs` — bounded byte reading for stream-based import requests
- `src/ChapterTool.Core/Localization/UiLanguageCode.cs` — supported UI language codes and normalization for every host

### Disc MPLS types

MPLS playlist records are split by type under `src/ChapterTool.Core/Importing/Disc/`:

- `MplsPlaylistFile.cs` — top-level file and `Read`
- `MplsPlayList.cs`, `MplsPlayItem.cs`, `MplsPlayListMark.cs`, `MplsExtensionData.cs`, and related stream tables

### Transform

Frame/time and expression logic:

- `src/ChapterTool.Core/Transform/IFrameRateService.cs`
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
- `src/ChapterTool.Core/Exporting/ChapterSavePath.cs` — deterministic output file names and non-colliding path allocation

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
- `src/ChapterTool.Wasm/Services/WasmBrowserShortcutGuard.cs` blocks browser shortcut keys during text editing.
- `WasmWorkspace` uses Core session and service types such as `ChapterWorkspace`, editing, segment, projection, and export services.
- Browser localization uses embedded JSON resources under `src/ChapterTool.Wasm/Resources/Locales/` through `WasmLocalizer`.
- The JSON resources are generated from the Avalonia AXAML locales by `scripts/axaml-to-json.py`. Use its `--check` mode to detect drift.
- Browser settings use the `WasmSettings` document with `schemaVersion`/`application`/`theme`/`font` fields. `WasmApplicationSettings` mirrors the Contracts `AppSettings` fields, including the delete-rows timing and frame display preferences. The host stores settings in browser storage through the workspace path.
- `tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs` covers workspace load, clip session, template, and export behavior. `tests/ChapterTool.Wasm.Tests/WasmBrowserShortcutGuardTests.cs` covers the shortcut guard.
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

For delete-rows timing or frame display preferences:

- `src/ChapterTool.Core/Editing/ChapterEditingOptions.cs`

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
