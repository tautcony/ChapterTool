# ChapterTool Program Form Capability Map

This document compares the ChapterTool program forms and the shared implementation layers.

The command-line interface (CLI) is the standalone terminal program.

The WebAssembly (WASM) application is the browser program.

The program forms are the standalone CLI, the Avalonia desktop application, the WASM browser application, and the Node.js package.

The Node.js package provides JavaScript access to portable Core operations.

The shared layers are Core, Infrastructure, and CommandLine.

Use this document to locate capability owners and host entry points before you change a feature.

Use this document to compare the supported functions of every program form.

Keep code identifiers, paths, commands, and user interface strings exact.

Last reviewed: 2026-07-21

## 1. Program Forms

### 1.1 Product Forms

| Form | Output | Main user | Main function | Platform boundary |
| --- | --- | --- | --- | --- |
| `ChapterTool.Cli` | `net10.0` executable | Terminal user or automation | Inspect and convert chapter sources | Uses local files, settings, external tools, and standard streams |
| `ChapterTool.Avalonia` | `net10.0` desktop executable | Desktop user | Edit, inspect, convert, and save chapter sources | Uses Avalonia, local files, settings, external tools, shell services, and Sentry GUI telemetry |
| `ChapterTool.Wasm` | Blazor WebAssembly application | Browser user | Load, edit, preview, and download chapter sources | Uses browser file input, browser memory, `localStorage`, JavaScript downloads, and browser APIs |
| `@chaptertool/node` | npm package backed by pure .NET WebAssembly | Node.js application or automation | Import, edit, transform, and export chapter data | Uses Node.js JavaScript and the packaged .NET WebAssembly runtime |

### 1.2 Shared Layers

| Layer | Type | Function | Direct dependencies | User interface |
| --- | --- | --- | --- | --- |
| `ChapterTool.Core` | Multi-target library | Own chapter models, import contracts, editing, transforms, projection, and export | None in the product graph | None |
| `ChapterTool.Infrastructure` | `net10.0` library | Own local files, settings, processes, external tools, and native import adapters | `ChapterTool.Core` | None |
| `ChapterTool.CommandLine` | `net10.0` library | Own DotMake commands, CLI application workflows, console output, and host decisions | `ChapterTool.Core`, `ChapterTool.Infrastructure`, `DotMake.CommandLine` | Terminal boundary only |

### 1.3 Dependency Graph

The arrows point from a consumer to a dependency:

```text
ChapterTool.CommandLine ---> ChapterTool.Infrastructure ---> ChapterTool.Core
           |
           +---------------> ChapterTool.Core

ChapterTool.Cli -----------> ChapterTool.CommandLine

ChapterTool.Avalonia ------> ChapterTool.CommandLine
       |-------------------> ChapterTool.Infrastructure
       +-------------------> ChapterTool.Core

ChapterTool.Wasm ----------> ChapterTool.Core

packages/chaptertool ------> ChapterTool.Node ------> ChapterTool.Core
```

`ChapterTool.Cli` does not reference Avalonia.

`ChapterTool.Avalonia` uses `ChapterTool.CommandLine` for CLI compatibility.

`ChapterTool.Wasm` does not reference Infrastructure or CommandLine.

`ChapterTool.Node` does not reference `ChapterTool.Wasm`, Blazor, Infrastructure, or CommandLine.

## 2. Host Entry Points

| Program form | Process entry point | Main application entry point | Import entry point | Export entry point | Test entry point |
| --- | --- | --- | --- | --- | --- |
| Standalone CLI | `src/ChapterTool.Cli/Program.cs` | `src/ChapterTool.CommandLine/ChapterToolCliHost.cs` | `ChapterToolCliApplication.ImportAsync` | `ChapterToolCliApplication.ConvertAsync` | `tests/ChapterTool.Avalonia.Tests/Cli/ChapterToolCliApplicationTests.cs` |
| Avalonia desktop | `src/ChapterTool.Avalonia/Program.cs` | `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs` | `src/ChapterTool.Avalonia/Services/RuntimeChapterLoadService.cs` | `src/ChapterTool.Avalonia/Services/RuntimeChapterSaveService.cs` | `tests/ChapterTool.Avalonia.Tests` and `tests/ChapterTool.Avalonia.Headless.Tests` |
| WASM browser | `src/ChapterTool.Wasm/Program.cs` | `src/ChapterTool.Wasm/Pages/Home.razor` | `src/ChapterTool.Wasm/Services/WasmChapterService.cs` | `src/ChapterTool.Wasm/Services/WasmChapterService.cs` and `src/ChapterTool.Wasm/wwwroot/js/download.js` | `tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs` |
| Node.js package | `src/ChapterTool.Node/Program.cs` | `packages/chaptertool/src/index.mjs` | `src/ChapterTool.Node/NodeApi.cs` | `src/ChapterTool.Node/NodeApi.cs` and `NodeCoreApi.cs` | `packages/chaptertool/test/chaptertool.test.mjs` and `core-api.test.mjs` |

### 2.1 Host Defaults

| Input | Standalone CLI | Avalonia desktop | WASM browser |
| --- | --- | --- | --- |
| No arguments | Show CLI help | Start the GUI | Show the workspace page |
| `--help` | Show CLI help | Show CLI help | Not applicable |
| `formats` | List CLI formats | Run the CLI command | Not applicable |
| `inspect <path>` | Inspect the source | Run the CLI command | Load the source and show workspace diagnostics |
| `convert <path>` | Convert the source | Run the CLI command | Load, project, and download through the workspace |
| One existing path | Return a usage error | Start the GUI with the path | Not applicable |
| `load <path>` | Return a usage error | Start the GUI with the path | Not applicable |
| Unknown token | Return a CLI usage error | Return a CLI usage error | Not applicable |

The standalone CLI and the Avalonia executable use the same DotMake command definitions.

The two hosts apply different launch policies through `ChapterToolCliHost`.

## 3. Capability Status

### 3.1 Status Labels

| Label | Meaning |
| --- | --- |
| `[Shared]` | The program forms use the same Core behavior or the same CommandLine behavior. |
| `[Host variant]` | The program forms provide the capability through different host services or workflows. |
| `[Desktop and CLI]` | The standalone CLI and Avalonia provide the capability through local runtime services. |
| `[Desktop only]` | Avalonia provides the capability. Browser constraints prevent the WASM application from providing it. |
| `[Browser only]` | WASM provides the capability through browser APIs. The desktop forms have no matching function. |
| `[CLI only]` | The standalone CLI provides the capability for terminal workflows. |
| `[Gap]` | A shared or host capability exists, but one program form does not expose it. |

These labels describe the current implementation.

They do not describe product priority.

### 3.2 Import and Source Access

| Capability | Core | Standalone CLI | Avalonia desktop | WASM browser | Owner and entry point |
| --- | --- | --- | --- | --- | --- |
| Text and OGM chapters | Provides `TextChapterImporter` | Supports local path input | Supports local path input | Supports browser text input | `src/ChapterTool.Core/Importing/Text/TextChapterImporter.cs` `[Shared]` |
| Premiere marker list | Provides `PremiereMarkerListImporter` | Routes `.csv` and `.txt` through the runtime registry | Routes `.csv` and `.txt` through the runtime registry | Uses text import detection | `RuntimeChapterImporterRegistry` and `WasmChapterService` `[Host variant]` |
| XML chapters | Provides `XmlChapterImporter` | Supports local `.xml` input | Supports local `.xml` input | Supports browser `.xml` input | `src/ChapterTool.Core/Importing/Text/XmlChapterImporter.cs` `[Shared]` |
| WebVTT chapters | Provides `WebVttChapterImporter` | Supports local `.vtt` input | Supports local `.vtt` input | Supports browser `.vtt` input | `src/ChapterTool.Core/Importing/Text/WebVttChapterImporter.cs` `[Shared]` |
| CUE chapters | Provides `CueChapterImporter` | Supports local `.cue` input | Supports local `.cue` input | Supports browser `.cue` input | `src/ChapterTool.Core/Importing/Cue/CueChapterImporter.cs` `[Shared]` |
| MPLS chapters | Provides `MplsChapterImporter` | Supports local `.mpls` input and selection options | Supports local `.mpls` input and clip selection | Supports browser `.mpls` input | `src/ChapterTool.Core/Importing/Disc/MplsChapterImporter.cs` `[Host variant]` |
| DVD IFO chapters | Provides `IfoChapterImporter` | Supports local `.ifo` input and selection options | Supports local `.ifo` input and clip selection | Supports browser `.ifo` input | `src/ChapterTool.Core/Importing/Disc/IfoChapterImporter.cs` `[Host variant]` |
| Embedded FLAC CUE | Provides `FlacCueImporter` | Supports local `.flac` input through Infrastructure | Supports local `.flac` input through Infrastructure | Does not route `.flac` input | `src/ChapterTool.Core/Importing/Cue/FlacCueImporter.cs` `[Desktop and CLI]` |
| Embedded TAK CUE | Provides `TakCueImporter` | Supports local `.tak` input through Infrastructure | Supports local `.tak` input through Infrastructure | Does not route `.tak` input | `src/ChapterTool.Core/Importing/Cue/TakCueImporter.cs` `[Desktop and CLI]` |
| HD-DVD XPL | Provides `XplChapterImporter` | Supports local `.xpl` input through Infrastructure | Supports local `.xpl` input through Infrastructure | Does not route `.xpl` input explicitly | `RuntimeChapterImporterRegistry` `[Desktop and CLI]` and `[Gap]` |
| Matroska media | Provides media contracts | Uses `mkvextract` or ffprobe through Infrastructure | Uses `mkvextract` or ffprobe through Infrastructure | Does not use native media adapters | `src/ChapterTool.Infrastructure/Importing/Matroska/` `[Desktop and CLI]` |
| Other media files | Provides media contracts | Uses ffprobe and the ATL MP4 fallback | Uses ffprobe and the ATL MP4 fallback | Does not use native media adapters | `src/ChapterTool.Infrastructure/Importing/Media/` `[Desktop and CLI]` |
| BDMV directory | Provides disc models and parsers | Reads local `BDMV/PLAYLIST` directories | Reads local `BDMV/PLAYLIST` directories | Cannot read local directories | `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvChapterImporter.cs` `[Desktop and CLI]` |
| Import fallback | Provides import results and diagnostics | Uses CLI fallback diagnostics | Uses GUI fallback diagnostics | Has no native-tool fallback chain | `src/ChapterTool.Infrastructure/Importing/Runtime/RuntimeChapterImporterRegistry.cs` `[Host variant]` |
| Stream and text import | Provides stream and text import APIs | Uses path APIs | Uses path APIs | Uses `ChapterImportRequest.Content` | `src/ChapterTool.Core/Importing/` `[Shared]` |
| Reload | Does not own session reload | Does not retain an interactive session | Reloads the current local source | Reloads the latest browser byte snapshot | `LoadSaveWorkflow` and `WasmWorkspace.ReloadAsync` `[Host variant]` |
| Append MPLS | Provides `ChapterSegmentService` | Does not provide an append command | Appends through the desktop workflow | Appends through `WasmWorkspace.AppendMplsAsync` | `src/ChapterTool.Core/Editing/ChapterSegmentService.cs` `[Host variant]` |

### 3.3 Inspection, Editing, and Session State

| Capability | Core | Standalone CLI | Avalonia desktop | WASM browser | Owner and entry point |
| --- | --- | --- | --- | --- | --- |
| Inspect groups and entries | Provides import result groups | Provides `inspect` | Shows groups through the GUI load workflow | Shows groups through the workspace | `ChapterToolCliApplication.InspectAsync` and `WasmWorkspace` `[Host variant]` |
| Select a group or entry | Provides ordered import result entries | Uses `--group-index`, `--entry-index`, and `--entry-id` | Uses clip and entry controls | Uses clip and entry controls | Core import result plus each host selection layer `[Host variant]` |
| Interactive chapter session | Provides models and services only | Does not provide an interactive session | Uses `ChapterWorkspace` and `ClipSession` | Uses `WasmWorkspace` | `src/ChapterTool.Avalonia/Session/` and `src/ChapterTool.Wasm/Services/WasmWorkspace.cs` `[Host variant]` |
| Edit chapter time | Provides `ChapterEditingService` | Does not edit rows interactively | Uses DataGrid editing | Uses browser input editing | `src/ChapterTool.Core/Editing/ChapterEditingService.cs` `[Host variant]` |
| Edit chapter name | Provides `ChapterEditingService` | Does not edit rows interactively | Uses DataGrid editing | Uses browser input editing | `src/ChapterTool.Core/Editing/ChapterEditingService.cs` `[Host variant]` |
| Insert and delete rows | Provides edit operations | Does not provide row commands | Provides insert and delete commands | Provides insert and delete actions | Core editing service plus host command layer `[Host variant]` |
| Select multiple rows | Provides model collections only | Does not provide row selection | Uses DataGrid selection | Uses `WasmWorkspace.SelectRow` | `MainWindowViewModel` and `WasmWorkspace` `[Host variant]` |
| Combine and restore clips | Provides `ChapterSegmentService` | Does not provide interactive clip state | Uses `ClipSession` | Uses `WasmWorkspace` clip state | `src/ChapterTool.Core/Editing/ChapterSegmentService.cs` `[Host variant]` |
| Forward frame shift | Provides frame transform services | Provides frame-rate and export options only | Provides `ForwardShiftToolView` | Provides a browser forward-shift action | `src/ChapterTool.Core/Transform/ChapterFpsTransformService.cs` `[Host variant]` |
| Copy rows and zones | Provides no clipboard boundary | Writes terminal output | Uses desktop clipboard services and tools | Uses browser clipboard or download actions | `IClipboardService`, `Home.razor`, and CLI console `[Host variant]` |

### 3.4 Transform, Expression, and Export

| Capability | Core | Standalone CLI | Avalonia desktop | WASM browser | Owner and entry point |
| --- | --- | --- | --- | --- | --- |
| Frame-rate detection | Provides `FrameRateService` | Uses an explicit `--frame-rate` override when supplied | Provides automatic and fixed frame-rate controls | Provides automatic and fixed frame-rate controls | `src/ChapterTool.Core/Transform/FrameRateService.cs` `[Host variant]` |
| Frame conversion | Provides `ChapterFpsTransformService` | Applies export frame-rate options | Applies frame-rate editing and export options | Applies frame-rate editing and export options | `src/ChapterTool.Core/Transform/ChapterFpsTransformService.cs` `[Shared]` |
| Chapter name preservation | Provides projection options | Preserves names by default | Provides a source-name option | Provides a source-name option | `ChapterOutputProjectionService` `[Shared]` |
| Automatic chapter names | Provides projection logic | Uses export projection | Provides an automatic-name option | Provides an automatic-name option | `src/ChapterTool.Core/Exporting/ChapterOutputProjectionService.cs` `[Shared]` |
| Template names | Provides template projection | Does not expose a template file option | Reads a local template file | Reads a browser text file | `ChapterNameTemplateReader` and `WasmWorkspace` `[Host variant]` |
| Lua expressions | Provides Lua execution | Supports `--expression` and `--expression-preset` | Provides expression editing and application | Applies text expressions without completion | `src/ChapterTool.Core/Transform/Expressions/Lua/` `[Host variant]` |
| Expression authoring | Provides analysis contracts and authoring services | Does not provide completion UI | Provides completion and diagnostics | Does not provide completion UI | `ExpressionEditor` and `ExpressionAuthoringService` `[Desktop only]` and `[Gap]` |
| Export formats | Provides all Core export formats | Lists and writes all CLI formats | Provides all desktop save formats | Provides all browser save formats | `src/ChapterTool.Core/Exporting/ChapterExportFormats.cs` `[Shared]` |
| XML language | Provides the language catalog | Supports `--xml-language` | Provides a localized language selector | Provides a language selector | `XmlChapterLanguageCatalog` `[Host variant]` |
| Text encoding | Provides encoding options | Writes UTF-8 output without a BOM | Uses selected encoding and BOM options | Uses selected encoding and browser download bytes | `src/ChapterTool.Core/Exporting/OutputTextEncoding.cs` `[Host variant]` |
| Preview | Provides export projection and serialization | Does not provide a separate preview command | Provides preview before save | Provides preview before download | `ProjectionFacade` and `WasmWorkspace.Preview` `[Host variant]` |
| Save to a file | Provides export content | Writes a local output path or standard output | Writes a selected or configured local path | Starts a browser download | `ChapterToolCliApplication`, `RuntimeChapterSaveService`, and `download.js` `[Host variant]` |
| Standard output | Does not own streams | Writes exported content to stdout and diagnostics to stderr | Supports the embedded CLI path | Not applicable | `src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.cs` `[CLI only]` |

### 3.5 Settings, Platform, and User Interface

| Capability | Core | Standalone CLI | Avalonia desktop | WASM browser | Owner and entry point |
| --- | --- | --- | --- | --- | --- |
| Settings model | Provides no host settings store | Reads shared `ChapterToolSettings` for CLI output defaults | Reads and writes shared `ChapterToolSettings` | Uses a browser settings model | `ChapterToolSettings` and `WasmSettings` `[Host variant]` |
| Settings persistence | Provides no persistence boundary | Uses `settings.json` under the shared ChapterTool settings directory | Uses the same `settings.json` directory | Uses `localStorage` | `ChapterToolRuntimeComposition`, `ChapterToolSettingsStore`, and `Home.razor` `[Host variant]` |
| Save directory | Provides no directory policy | Uses `--output`, settings, or the source directory | Uses a picker, settings, or the source directory | Uses the browser download directory | `ChapterToolCliApplication`, `ChapterSaveDirectory`, and `download.js` `[Host variant]` |
| External tool configuration | Provides no native tool boundary | Reads configured tool paths and `PATH` search directories | Reads and edits configured tool paths | Does not use native tools | `ChapterToolRuntimeComposition` and `ExternalToolLocator` `[Desktop and CLI]` |
| Process execution | Provides no process boundary | Runs external import tools through Infrastructure | Runs external import tools through Infrastructure | Cannot start local processes | `src/ChapterTool.Infrastructure/Processes/ProcessRunner.cs` `[Desktop and CLI]` |
| Theme presets | Provides no UI resource layer | Uses terminal text only | Uses Avalonia theme resources | Uses CSS theme resources | `ThemePresetCatalog`, Avalonia theme service, and WASM styles `[Host variant]` |
| System fonts | Provides no font enumeration | Uses terminal output only | Enumerates and applies system fonts | Uses CSS font stacks | `AvaloniaFontFamilyCatalog` and WASM CSS `[Desktop only]` |
| Language selection | Provides no UI localization | Uses fixed CLI messages | Uses `.resx` resources | Uses `WasmLocalizer` dictionaries | `AppLocalizationManager` and `WasmLocalizer` `[Host variant]` |
| Shell operations | Provides no shell boundary | Does not open paths | Opens paths through `IShellService` | Uses browser links or browser APIs | `src/ChapterTool.Infrastructure/Platform/ShellService.cs` `[Desktop only]` |
| Clipboard | Provides no clipboard boundary | Uses standard streams | Uses the desktop clipboard adapter | Uses browser clipboard APIs | `IClipboardService` and browser JavaScript `[Host variant]` |
| Application telemetry | Provides no telemetry startup | Does not initialize Sentry | Initializes Sentry for GUI startup when configured | Does not use the desktop Sentry startup | `src/ChapterTool.Avalonia/Program.cs` `[Desktop only]` |
| Logs and diagnostics | Provides structured diagnostics | Writes diagnostics to terminal streams | Uses the application log panel and localized UI messages | Uses in-memory logs and localized UI messages | `ChapterDiagnostic`, `ApplicationLogPanelProvider`, and `WasmWorkspace` `[Host variant]` |
| Progress | Provides import progress contracts | Does not render interactive progress | Renders import progress in the desktop workflow | Reports browser load and import progress | `ChapterImportProgress` and host workflows `[Host variant]` |
| Input size limit | Provides parser limits | Has no shared 64 MB browser limit | Has no shared 64 MB browser limit | Enforces `WasmWorkspace.MaxLoadBytes` at 64 MiB | `src/ChapterTool.Wasm/Services/WasmWorkspace.cs` `[Browser only]` |

## 4. Ownership and Lookup

### 4.1 Core

Core owns behavior that does not require a host platform.

Start with these paths for shared chapter behavior:

- Models and diagnostics: `src/ChapterTool.Core/Models/` and `src/ChapterTool.Core/Diagnostics/`
- Import contracts: `src/ChapterTool.Core/Importing/`
- Text import: `src/ChapterTool.Core/Importing/Text/`
- CUE import: `src/ChapterTool.Core/Importing/Cue/`
- Disc import: `src/ChapterTool.Core/Importing/Disc/`
- Editing and segments: `src/ChapterTool.Core/Editing/`
- Frame and time transforms: `src/ChapterTool.Core/Transform/`
- Projection and export: `src/ChapterTool.Core/Exporting/`

Core targets `net8.0`, `net9.0`, and `net10.0`.

Core supports browser use through stream and text import APIs.

### 4.2 Infrastructure

Infrastructure owns desktop and CLI runtime boundaries.

Start with these paths for local runtime behavior:

- Runtime registry and factories: `src/ChapterTool.Infrastructure/Importing/Runtime/`
- Native import adapters: `src/ChapterTool.Infrastructure/Importing/`
- Tool lookup: `src/ChapterTool.Infrastructure/Tools/`
- Process execution: `src/ChapterTool.Infrastructure/Processes/`
- Settings persistence: `src/ChapterTool.Infrastructure/Configuration/`
- Shell and native dependency services: `src/ChapterTool.Infrastructure/Platform/`

Infrastructure targets `net10.0`.

WASM does not reference Infrastructure.

### 4.3 CommandLine and Standalone CLI

CommandLine owns the terminal command tree and CLI application workflows.

Start with these paths for CLI behavior:

- Product facade: `src/ChapterTool.CommandLine/ChapterToolCliHost.cs`
- DotMake commands: `src/ChapterTool.CommandLine/Cli/ChapterToolCliCommands.cs`
- CLI workflow application: `src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.cs`
- CLI parsing and launch policy: `src/ChapterTool.CommandLine/Cli/ChapterToolCliSupport.cs`
- Console boundary: `src/ChapterTool.CommandLine/Cli/CliConsole.cs`
- Standalone process host: `src/ChapterTool.Cli/Program.cs`

The standalone CLI uses explicit commands.

The standalone CLI shows help when it receives no arguments.

The standalone CLI rejects GUI-only `load` behavior.

The standalone CLI returns `0` for successful commands, `1` for user or workflow failures, and `2` for unhandled exceptions.

### 4.4 Avalonia Desktop

Avalonia owns the desktop shell, interactive session, localization, theme application, and GUI platform services.

Start with these paths for desktop behavior:

- Process and GUI launch: `src/ChapterTool.Avalonia/Program.cs`
- Composition: `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs`
- Main window: `src/ChapterTool.Avalonia/Views/MainWindow.axaml`
- Main state: `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs`
- Session: `src/ChapterTool.Avalonia/Session/`
- Workflows: `src/ChapterTool.Avalonia/Workflows/`
- Runtime load and save: `src/ChapterTool.Avalonia/Services/RuntimeChapterLoadService.cs` and `RuntimeChapterSaveService.cs`
- CLI compatibility: `src/ChapterTool.CommandLine/ChapterToolCliHost.cs`

The Avalonia executable starts the GUI without arguments.

The Avalonia executable starts the GUI for an existing plain path.

The Avalonia executable runs the shared CLI command tree for CLI commands.

### 4.5 WASM Browser

WASM owns browser file input, browser session state, browser downloads, browser localization, and browser-specific limits.

Start with these paths for browser behavior:

- Application startup: `src/ChapterTool.Wasm/Program.cs`
- Page and browser actions: `src/ChapterTool.Wasm/Pages/Home.razor`
- Core import and export adapter: `src/ChapterTool.Wasm/Services/WasmChapterService.cs`
- Workspace state and workflows: `src/ChapterTool.Wasm/Services/WasmWorkspace.cs`
- Browser settings and localization: `src/ChapterTool.Wasm/Services/WasmModels.cs` and `WasmLocalizer.cs`
- Download bridge: `src/ChapterTool.Wasm/wwwroot/js/download.js`

WASM uses `ChapterImportRequest.Content` for browser file content.

WASM does not use local paths or local processes.

WASM stores settings in `localStorage`.

WASM sends exported content to the browser download bridge.

### 4.6 Node.js Package

The Node.js package owns JavaScript input conversion, .NET WebAssembly startup, and JSON mapping for the Core API.

Start with these paths for Node.js package behavior:

- WebAssembly host: `src/ChapterTool.Node/NodeApi.cs`
- Core operation exports: `src/ChapterTool.Node/NodeCoreApi.cs`
- JavaScript API source: `packages/chaptertool/src/index.mjs`
- Type declaration source: `packages/chaptertool/src/index.d.ts`
- Runtime build: `packages/chaptertool/scripts/build.mjs`
- Environment check: `packages/chaptertool/scripts/check-environment.mjs`
- Package tests: `packages/chaptertool/test/chaptertool.test.mjs` and `core-api.test.mjs`

The package accepts UTF-8 strings, `Buffer`, and `Uint8Array` input.

The package exposes portable Core editing, transformation, projection, conversion, and metadata operations.

The package does not expose browser workspace state or UI actions.

The package does not reference Blazor or desktop infrastructure.

The package build copies the pure .NET WebAssembly runtime into the npm package.

## 5. Shared Behavior Rules

Use these rules when you change a capability:

1. Put chapter semantics, parsing rules, edit results, transform rules, projection rules, and serialized output in Core.
2. Put local files, directories, external tools, processes, settings files, and shell operations in Infrastructure.
3. Put DotMake command definitions, CLI options, CLI selection rules, terminal diagnostics, and CLI output paths in CommandLine.
4. Put desktop controls, desktop session state, Avalonia resources, system fonts, and desktop localization in Avalonia.
5. Put browser file input, browser downloads, browser storage, browser limits, and CSS presentation in WASM.
6. Put Node.js API mapping and packaged runtime startup in `ChapterTool.Node` and `packages/chaptertool`.
7. Keep the standalone CLI free of Avalonia references.
8. When a shared Core behavior changes, test the Core path and one regression path in each applicable program form.
9. When a host feature changes, update this matrix and the host code map.

## 6. Verification Matrix

| Change area | Primary test project | Additional verification |
| --- | --- | --- |
| Core import, edit, transform, projection, or export | `tests/ChapterTool.Core.Tests` | Run one applicable path in the CLI, Avalonia, and WASM forms |
| Infrastructure import, process, tool, or settings behavior | `tests/ChapterTool.Infrastructure.Tests` | Verify the Avalonia or CLI composition path |
| CommandLine or standalone CLI behavior | `tests/ChapterTool.Avalonia.Tests/Cli/ChapterToolCliApplicationTests.cs` | Run the standalone CLI for help, `formats`, and a usage error |
| Avalonia ViewModel or service behavior | `tests/ChapterTool.Avalonia.Tests` | Check the GUI workflow when the change affects desktop behavior |
| Avalonia XAML or interaction behavior | `tests/ChapterTool.Avalonia.Headless.Tests` | Check user actions and workflow results |
| WASM workspace or browser behavior | `tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs` | Build `src/ChapterTool.Wasm/ChapterTool.Wasm.csproj` when project assets change |
| Node.js package or npm runtime packaging | `packages/chaptertool/test/chaptertool.test.mjs` | Run `npm test` from `packages/chaptertool` |
| Cross-form behavior | All applicable test projects | Run `dotnet test ChapterTool.Avalonia.slnx --no-restore` sequentially |

Do not use source-text assertions to test code or configuration.

Use compiled behavior, runtime verification, structured APIs, and user-facing workflow tests.

## 7. Maintenance Rules

- Update this document when a program form gains or loses a user-visible capability.
- Update this document when a host entry point or a primary test entry point changes.
- State the browser alternative when a desktop or CLI capability is unavailable in WASM.
- State the desktop and CLI alternative when a browser capability has no desktop equivalent.
- Keep status labels current.
- Keep one technical term for one concept.
- Do not record temporary workarounds or archived change details in this document.
