# Avalonia / Infrastructure / WASM Capability Map

This document follows ASD-STE100 principles. Each sentence states one fact. The text uses active voice and explicit subjects. The text uses consistent terms. Code identifiers, paths, commands, and user interface strings remain unchanged.

This document compares capabilities across the Avalonia desktop application, the Infrastructure layer, and the Blazor WebAssembly browser application. Use this document to locate all affected entry points before a capability change. Use it to confirm the expected alignment between the two applications after the change.

Last reviewed: 2026-07-19

## 1. Summary

The current dependencies are as follows:

```text
ChapterTool.Avalonia
  ├─ ChapterTool.Core       Shared chapter models, parsing, editing, transformation, projection, and export
  └─ ChapterTool.Infrastructure
       ├─ External tool discovery and process execution
       ├─ Local file and platform services
       ├─ Settings file persistence
       └─ Media, Matroska, and BDMV import adapters

ChapterTool.Wasm
  └─ ChapterTool.Core       Import through browser byte streams and export through JavaScript downloads
```

Key conclusions:

- Core is the preferred source of shared behavior for both applications. Put changes to time parsing, chapter models, editing, segment merging, frame rate transformation, projection, and export in Core when possible. Do not implement separate fixes in each host.
- Avalonia uses `AppCompositionRoot` to compose all desktop capabilities. WASM uses `WasmChapterService` and `WasmWorkspace` to compose capabilities that are safe for the browser.
- WASM does not reference Infrastructure. It cannot directly use local paths, external processes, desktop settings files, system font enumeration, or shell operations.
- Each application has its own state orchestration and user interface options. A new capability can appear in only one application even when both applications use the same Core logic. Always inspect both host entry points.

## 2. Status Labels

| Label | Meaning |
| --- | --- |
| `[Aligned]` | Both applications provide the capability, and the same Core type provides the principal behavior. |
| `[Partially aligned]` | The principal behavior is aligned, but entry points, options, host services, or interactions differ. |
| `[Desktop only]` | Avalonia and Infrastructure provide the capability, but browser platform constraints prevent WASM from providing it. |
| `[WASM only]` | The browser application provides the capability, but the desktop application has no equivalent entry point. |
| `[Gap]` | Core or one host has the required foundation, but the target host does not use it. |

These labels describe the current code. They do not indicate product priority.

## 3. Runtime Map

### 3.1 Main Workspace

| Responsibility | Avalonia desktop | WASM browser | Maintenance note |
| --- | --- | --- | --- |
| UI shell | `src/ChapterTool.Avalonia/Views/MainWindow.axaml(.cs)` | `src/ChapterTool.Wasm/Pages/Home.razor` | Both applications contain four workspace zones: top actions, central chapter table, bottom output options, and status bar. They do not share UI code. |
| Session state | `src/ChapterTool.Avalonia/Session/ChapterWorkspace.cs`, `ClipSession.cs`, `ProjectionState.cs`, `ExportPreferences.cs` | `src/ChapterTool.Wasm/Services/WasmWorkspace.cs` | Both applications store source, clip, row, projection, and export state. Inspect bindings and refresh logic in both applications when you change state fields. |
| Main workflow | `src/ChapterTool.Avalonia/Workflows/LoadSaveWorkflow.cs`, `ClipEditingCoordinator.cs`, `ProjectionFacade.cs` | `LoadAsync`, `AppendMplsAsync`, `RefreshDisplay`, `Preview`, and `Save` in `WasmWorkspace` | Avalonia uses composed collaborators. WASM still centralizes this work in one workspace service. |
| Localization | `src/ChapterTool.Avalonia/Localization/AppLocalizationManager.cs` and `.resx` files | Dictionaries in `src/ChapterTool.Wasm/Services/WasmLocalizer.cs` | Both applications provide `en-US`, `zh-CN`, and `ja-JP`. They do not share resource files. Add new user-visible text to both locations. |
| Test entry point | Avalonia unit and Headless test projects | `tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs` | Headless tests verify desktop rendering and interaction. WASM tests primarily verify workspace behavior. |

### 3.2 Infrastructure Position

Only the desktop composition root uses Infrastructure:

- `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs` creates `ChapterToolSettingsStore`, `ExternalToolLocator`, `ProcessRunner`, `FfprobeMediaChapterReader`, and `AtlMp4ChapterReader`.
- `src/ChapterTool.Avalonia/Services/RuntimeChapterImporterRegistry.cs` registers Core and Infrastructure importers by path extension or directory structure.
- `src/ChapterTool.Avalonia/Services/RuntimeChapterLoadService.cs` reads real paths and sends them to the registry. `RuntimeChapterSaveService.cs` selects local output directories, allocates file names, and processes export results before it writes files.
- WASM does not use these types. `InputFile` and drop events read browser files into `byte[]`. `src/ChapterTool.Wasm/Services/WasmChapterService.cs` puts the bytes in `ChapterImportRequest.Content`. `src/ChapterTool.Wasm/wwwroot/js/download.js` starts browser downloads for exported content.

## 4. Capability Matrix

### 4.1 Input and Import

| Capability | Avalonia and Infrastructure | WASM | Status | Primary entry point |
| --- | --- | --- | --- | --- |
| OGM/TXT chapters | Core `TextChapterImporter` distinguishes OGM and Premiere content in local `.txt` files. | The same Core `TextChapterImporter` receives byte streams from `.txt` files or text files without an extension. | `[Aligned]` | `src/ChapterTool.Core/Importing/Text/TextChapterImporter.cs` |
| Premiere marker CSV | The desktop registry selects `PremiereMarkerListImporter` directly for `.csv` files. | The WASM text importer receives `.csv` files and detects Premiere content. Explicit format routing is less precise. | `[Partially aligned]` | `RuntimeChapterImporterRegistry.cs`, `WasmChapterService.cs` |
| Matroska XML | Core `XmlChapterImporter` imports `.xml` files. | The same Core importer imports `.xml` files. | `[Aligned]` | `src/ChapterTool.Core/Importing/Text/XmlChapterImporter.cs` |
| WebVTT | Core `WebVttChapterImporter` imports `.vtt` files. | The same Core importer imports `.vtt` files. | `[Aligned]` | `src/ChapterTool.Core/Importing/Text/WebVttChapterImporter.cs` |
| CUE text | Core `CueChapterImporter` imports `.cue` files. | The same Core importer imports `.cue` files. | `[Aligned]` | `src/ChapterTool.Core/Importing/Cue/CueChapterImporter.cs` |
| MPLS | Core `MplsChapterImporter` reads local paths. | The same Core importer reads file bytes. | `[Aligned]` | `src/ChapterTool.Core/Importing/Disc/MplsChapterImporter.cs` |
| DVD IFO | Core `IfoChapterImporter` reads local paths or bytes. | The same Core importer reads file bytes. | `[Aligned]` | `src/ChapterTool.Core/Importing/Disc/IfoChapterImporter.cs` |
| HD-DVD XPL | The desktop registry routes `.xpl` files. | The WASM resolver does not route `.xpl` files explicitly. It normally falls back to the text importer. | `[Desktop only]` / `[Gap]` | `src/ChapterTool.Core/Importing/Disc/XplChapterImporter.cs`, `src/ChapterTool.Avalonia/Services/RuntimeChapterImporterRegistry.cs` |
| Embedded FLAC CUE | The desktop registry routes `.flac` files to Core `FlacCueImporter`. | The WASM resolver does not route `.flac` files. | `[Desktop only]` / `[Gap]` | `src/ChapterTool.Core/Importing/Cue/FlacCueImporter.cs` |
| Embedded TAK CUE | The desktop registry routes `.tak` files to Core `TakCueImporter`. | The WASM resolver does not route `.tak` files. | `[Desktop only]` / `[Gap]` | `src/ChapterTool.Core/Importing/Cue/TakCueImporter.cs` |
| BDMV directory | The desktop application detects `BDMV/PLAYLIST` directories and calls Infrastructure `BdmvChapterImporter`. | The browser can select only files. It cannot read a local directory structure or run eac3to. | `[Desktop only]` | `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvChapterImporter.cs` |
| Matroska and media files | The mkvextract adapter processes `.mkv/.mka/.webm` files. ffprobe processes audio and video formats. ATL provides an MP4 fallback. | WASM does not use Infrastructure and has no browser media metadata adapter. | `[Desktop only]` | `src/ChapterTool.Infrastructure/Importing/Matroska/MatroskaChapterImporter.cs`, `src/ChapterTool.Infrastructure/Importing/Media/` |
| Import fallback policy | The desktop application uses ffprobe -> ATL MP4, Matroska -> ffprobe, and FLAC -> ffprobe fallback chains. | WASM has no external dependency fallback chain. | `[Desktop only]` | `RuntimeChapterImporterRegistry.ResolveFallback` |
| Reload | The desktop workflow stores the source path and reads it again. | WASM stores the latest `FileName + byte[]` snapshot and imports it again. | `[Partially aligned]` | `LoadSaveWorkflow.cs`, `WasmWorkspace.ReloadAsync` |
| Append MPLS | The desktop application appends to an existing MPLS session and uses Core `ChapterSegmentService.Append` to merge the content. | WASM uses the same Core operation, but it accepts only another MPLS file selected in the browser. | `[Partially aligned]` | `Session/ClipSession.cs`, `WasmWorkspace.AppendMplsAsync` |
| File drop | Avalonia sends files dropped on the window to the path loader. | The browser reads dropped files as bytes and applies a 64 MB limit. | `[Partially aligned]` | `Views/MainWindow.axaml.cs`, `Pages/Home.razor` |

### 4.2 Chapter Session and Editing

| Capability | Avalonia | WASM | Status | Primary entry point |
| --- | --- | --- | --- | --- |
| Multiple group and item selection | `ClipSession` and `DisplayOptionCoordinator` | `WasmWorkspace.ClipOptions` | `[Partially aligned]` | `src/ChapterTool.Avalonia/Session/ClipSession.cs`, `src/ChapterTool.Wasm/Services/WasmWorkspace.cs` |
| MPLS/IFO segment merge and restore | `ChapterSegmentService.Combine` stores state in `ClipSession`. | The same Core service stores state in `WasmWorkspace`. | `[Aligned]` | |
| Time editing | `ChapterEditingService` and DataGrid cell editing | `ChapterEditingService` and HTML input | `[Aligned]` | |
| Name editing | Same as time editing | Same as time editing | `[Aligned]` | |
| Insert and delete | `InsertCommand`, `DeleteCommand` | `InsertBefore`, `DeleteSelectedRows` | `[Aligned]` | |
| Multiple selection | The DataGrid sends selected rows to the ViewModel. | `Home.razor` and `WasmWorkspace.SelectRow` implement Ctrl and Shift selection. | `[Partially aligned]` | |
| Copy rows, names, and zones | Avalonia uses the clipboard service and tool windows. | The browser uses the JavaScript Clipboard API or download and copy actions. | `[Partially aligned]` | |
| `--zones` | `MainWindowViewModel.CreateZonesText` and `TextToolView` | `CreateZonesForSelection` and the context menu | `[Aligned]` | |
| Shift frames forward | Avalonia uses `ForwardShiftToolView` and `IChapterEditPort`. | The browser forward-shift dialog calls `ShiftFramesForward`. | `[Aligned]` | |
| Associated media | Avalonia can use `IShellService` to open a local path. | WASM displays relative or absolute paths but cannot open a local absolute path. | `[Partially aligned]` | |
| Diagnostics and logs | Core diagnostics, `ApplicationLogPanelProvider`, and the UI log window | `WasmWorkspace.Diagnostics` and an in-memory log dialog | `[Partially aligned]` | |
| Progress | The desktop workflow sends Core `IChapterImportProgressReporter` updates to the UI. | WASM reports coarse progress only during file reading and parsing. | `[Partially aligned]` | |

### 4.3 Frame Rate, Projection, and Export

| Capability | Avalonia | WASM | Status | Primary entry point |
| --- | --- | --- | --- | --- |
| Automatic and fixed frame rate detection | Core `FrameRateService` with desktop `ClipEditingCoordinator` orchestration | The same Core `FrameRateService` with `WasmWorkspace` orchestration | `[Aligned]` | |
| Frame data, rounding, and precision flags | Core `FrameRateService.UpdateFrames` | The same Core method | `[Aligned]` | |
| Chapter name: preserve source | `ProjectionState` and `ProjectionFacade` | `ChapterNameModeIndex = 0` | `[Aligned]` | |
| Chapter name: generate automatically | Core `ChapterOutputProjectionService` | The same Core service | `[Aligned]` | |
| Chapter name: template | Desktop `ChapterNameTemplateReader` reads local text. | Browser `InputFile` reads text. | `[Partially aligned]` | |
| Number offset | Both hosts expose the Core projection option. | Same as Avalonia | `[Aligned]` | |
| Lua expression execution | Avalonia uses shared `LuaExpressionScriptService` and provides editing, completion, and diagnostics. | WASM runs supported Core and Lua expressions, but provides only plain text input. | `[Partially aligned]` | |
| Expression authoring assistance | `ExpressionEditor`, `ExpressionAuthoringService`, completion, and diagnostic presentation | WASM provides no editor, completion, or live diagnostics. | `[Desktop only]` / `[Gap]` | |
| Export formats | TXT, XML, QPFile, TimeCodes, tsMuxeR, CUE, JSON, WebVTT, and Celltimes | The same Core `ChapterExportFormats.All` exposes all formats. | `[Aligned]` | |
| XML language | The desktop application localizes the Core catalog display. | WASM displays language codes from the Core catalog. | `[Partially aligned]` | |
| Text encoding and byte order mark (BOM) | Core export options and the desktop file service | Core export options and browser downloads | `[Partially aligned]` | |
| Preview | Desktop `BuildPreview` and export service | `WasmWorkspace.Preview` uses the same projection and export path as Save. | `[Aligned]` | |
| Save | The desktop application allocates a unique local file path and writes the file. | WASM creates a download file name and sends it to JavaScript. | `[Partially aligned]` | |

### 4.4 Settings, Appearance, and Platform Capabilities

| Capability | Avalonia and Infrastructure | WASM | Status | Primary entry point |
| --- | --- | --- | --- | --- |
| Settings aggregate | `ChapterToolSettings` contains `Application`, `Theme`, and `Font`. | `WasmSettings` has a similar structure. | `[Partially aligned]` | |
| Persistence | `ChapterToolSettingsStore` writes versioned `settings.json` data. It provides locking, normalization, migration, and corruption recovery. | `localStorage` stores JSON snapshots. The current implementation is in `Home.razor`. | `[Partially aligned]` | |
| Default export format, XML, encoding, BOM, and precision tolerance | The desktop application persists these options and applies them at startup and in the settings window. | WASM persists these options and exposes them in the browser settings dialog. | `[Partially aligned]` | |
| Save directory | The desktop application provides a directory picker and writes local files. | The browser uses its default download directory. The application cannot select that directory. | `[Desktop only]` | |
| External tool paths and validation | The desktop application configures, discovers, and validates mkvtoolnix, eac3to, ffprobe, and ffmpeg paths. | WASM retains disabled controls with an availability message. It does not validate paths. | `[Desktop only]` | |
| Theme presets | Infrastructure `ThemePresetCatalog` and the Avalonia theme application service | WASM defines separate theme presets and CSS colors. | `[Partially aligned]` | |
| System font enumeration | Avalonia `AvaloniaFontFamilyCatalog` reads system fonts. | WASM provides only CSS font stack options. | `[Partially aligned]` | |
| Language selection | `.resx` files and `AppLocalizationManager` update control resources dynamically. | `WasmLocalizer` dictionaries update workspace state and the page dynamically. | `[Partially aligned]` | |
| Settings directory and window position | The desktop application can open the settings directory and persist the window position. | Not applicable. The browser does not expose the local shell. | `[Desktop only]` | |
| Clipboard | Infrastructure `IClipboardService` and the Avalonia adapter | The JavaScript Clipboard API, subject to browser permissions | `[Partially aligned]` | |
| Shell and associated media | Infrastructure `IShellService` | WASM does not provide a local shell. | `[Desktop only]` | |
| Command-line interface (CLI) | The Avalonia project contains the DotMake.CommandLine entry point. | WASM has no CLI. | `[Desktop only]` | |

## 5. Shared Core Behavior

The following types are the preferred change points for behavior that must remain aligned across hosts:

- Models and diagnostics: `Core/Models/` and `Core/Diagnostics/`.
- Import contracts: `ChapterImportRequest`, `IChapterImporter`, `ChapterImportResult`, and `ChapterImportProgress`.
- Text and disc format import: `Core/Importing/Text/`, `Core/Importing/Cue/`, and `Core/Importing/Disc/`.
- Editing and segments: `Core/Editing/ChapterEditingService.cs` and `ChapterSegmentService.cs`.
- Frame processing: `Core/Transform/FrameRateService.cs`, `ChapterFpsTransformService.cs`, and `ChapterRounding.cs`.
- Projection and expressions: `Core/Exporting/ChapterOutputProjectionService.cs`, `Core/Transform/ChapterExpressionService.cs`, and `Core/Transform/Expressions/`.
- Export: `Core/Exporting/ChapterExportService.cs`, `ChapterExportFormats.cs`, `ChapterExportOptions.cs`, and `OutputTextEncoding.cs`.

When a requirement changes chapter semantics, parsing rules, editing results, or export content, inspect the Core tests first. Confirm that both hosts still call the applicable Core capability. Do not copy an algorithm into `WasmWorkspace` or `MainWindowViewModel`.

## 6. Important Current Differences

1. **Importer registration is asymmetric.** WASM `WasmChapterService.ResolveImporter` explicitly routes `.vtt/.xml/.cue/.mpls/.ifo`. It sends other extensions to the text importer. The desktop registry also routes `.csv/.flac/.tak/.xpl`, media files, Matroska files, and BDMV directories. When you add a Core importer, decide whether the WASM resolver must also route it.
2. **Only expression execution is aligned.** WASM can apply expressions. It does not provide the Avalonia `ExpressionEditor`, completion, diagnostic presentation, or editor keyboard behavior. When you change expression syntax or available functions, verify both execution and authoring assistance.
3. **The settings models are similar, but persistence differs.** The desktop application uses `settings.json`. WASM uses `localStorage`. When you add a settings field, update both snapshots, defaults, normalization rules, version migrations, and UI application logic.
4. **The theme and font rendering layers are separate.** The desktop application uses Avalonia resources and system fonts. WASM uses CSS. When you add theme colors or font semantics, update the visual mapping in both applications.
5. **Associated media capabilities are not equivalent.** The desktop application can resolve a relative path and send it to the shell. WASM can only display information or links. The browser application must not claim that it can open a local absolute path.
6. **Save semantics differ.** The desktop application saves to a selected or configured directory and allocates a real path. WASM can only create a download. When you add file name, extension, or encoding rules, inspect the Core export result, desktop path processing, and JavaScript download.
7. **WASM has a separate 64 MB input limit.** The desktop application does not have the same limit. When you change this limit, review browser memory and denial-of-service risks, error messages, and related tests.

## 7. Capability Change Procedure

### 7.1 Classify the Requirement

Identify the applicable category:

- For chapter semantics, format parsing, editing, frame rates, projection, or export, inspect `ChapterTool.Core` first.
- For local files, directories, external tools, processes, settings files, shell operations, or system fonts, inspect `Infrastructure` and then the Avalonia composition and services.
- For desktop interactions, shortcuts, the DataGrid, tool windows, or resource localization, inspect `ChapterTool.Avalonia`.
- For browser file input, file drop, downloads, `localStorage`, CSS, or browser constraints, inspect `ChapterTool.Wasm`.
- For main workspace commands or output options, inspect both `MainWindowViewModel*.cs` and `WasmWorkspace.cs` or `Home.razor`.

### 7.2 Checks Before a Change

- Can the change reuse an existing Core service or importer?
- Does Avalonia `RuntimeChapterImporterRegistry` require a new extension or fallback?
- Does WASM `WasmChapterService.ResolveImporter` need to route the same Core importer?
- Do both hosts define the applicable export field, default value, and refresh path?
- Do desktop `ChapterToolSettings` and WASM `WasmSettings` require aligned fields and version processing?
- Does the change require updates to English, Chinese, and Japanese text?
- Does the change require updates to ownership, entry points, or test entry points in `docs/code-map/`?

### 7.3 Verification Matrix

| Change type | Minimum verification |
| --- | --- |
| Core parsing, editing, projection, or export | Run `dotnet test tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj --no-restore`. Run at least one host regression path in both WASM and Avalonia. |
| Infrastructure import, tools, or settings | Run `dotnet test tests/ChapterTool.Infrastructure.Tests/ChapterTool.Infrastructure.Tests.csproj --no-restore`. Then verify the Avalonia composition or settings entry point. |
| Avalonia ViewModel, service, or CLI | Run `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore`. |
| Avalonia XAML or interaction | Run `dotnet test tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj --no-restore`. |
| WASM workspace or browser entry point | Run `dotnet test tests/ChapterTool.Wasm.Tests/ChapterTool.Wasm.Tests.csproj --no-restore`. Run `dotnet build src/ChapterTool.Wasm/ChapterTool.Wasm.csproj --no-restore` when necessary. |
| Shared behavior across layers or a pre-release change | Run the complete test suite in the repository-defined sequence. Do not run multiple test projects in parallel. |

For UI changes, also save default, wide-window, and narrow-window screenshots in `artifacts/`. Screenshots provide manual layout evidence. They do not replace behavior tests.

## 8. Maintenance Rules

- When you add or change a Core capability, add its shared behavior and both host entry points to this table.
- When you add a desktop Infrastructure capability, specify whether WASM is unavailable, planned, or uses a browser alternative. Do not write only that WASM does not support the capability.
- When you add a WASM capability, specify whether it is a browser-specific interaction or behavior that belongs in Core or a shared contract.
- After you complete a capability, update the last reviewed date, status labels, and test entry points in this file.
- If an entry point, module owner, composition root, or primary test file changes, update the applicable entries in `docs/code-map/README.md`, `core.md`, `infrastructure.md`, `avalonia.md`, or `testing.md`.
- This document tracks architecture and capabilities. Do not record one-time implementation procedures, temporary workarounds, or archived changes.
