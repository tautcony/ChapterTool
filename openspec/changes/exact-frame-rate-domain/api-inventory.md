# Frame-rate API inventory

The `remove-v23-compatibility-surfaces` change defines the current 23.x API.
This table records the resulting contracts. A public modifier alone does not
establish a compatibility obligation.

| Boundary | Evidence | Current contract |
| --- | --- | --- |
| `ChapterSet` | Core README and importer/exporter consumers | `FrameRateValue` is the only rate field and constructor input. Numeric projections and compatibility deconstruction are removed. |
| `ChapterEditingService` | Core editing services and exact Node operations | Frame operations accept `FrameRateValue`. Numeric FPS overloads are removed. |
| `ChapterFpsTransformService.ChangeFps` | Core transform service and candidate builder | Source and target rates use `FrameRateValue`. |
| `FrameRateService` | Desktop, WASM, and Node exact package | `FindByRate` accepts `FrameRateValue`. Options and frame results carry canonical rates. Numeric lookup and projection aliases are removed. |
| `IChapterExpressionEngine` | Core README and expression-engine consumers | The documented `Evaluate` extension remains. Lua numeric values are expression scalars and do not establish model-rate authority. |
| `ChapterExpressionService` and expression authoring | Core expression workflow | Internal operations use `FrameRateValue`. Expression-language FPS inputs retain their language contract. |
| `ChapterExportService`, `IChapterExporter`, and `ChapterSerializationOptions` | Core README and serialization consumers | Serialization is pure. Options contain format settings and an optional canonical computation rate. `ChapterExportOptions` and transform-bearing export methods are removed. |
| `ChapterOutputProjectionService` | CLI content conversion and projection tests | Content transformations run before serialization through `ChapterOutputProjectionOptions`. |
| `ChapterConversionService` | Core conversion service and exact Node package | Frame conversions accept `FrameRateValue`. Optional v2 timecode mapping remains supported. |
| `ChapterSegmentService` | Core combine/append feature and exact Node package | Mixed rates must produce a conflict when scalar output cannot represent them. |
| Node `ChapterTool` | Package source, README, and tests | The exact typed API is the package entry point. Tick and rate data use lossless strings. Numeric chapter/rate DTOs and `.exact`, `.project`, and `.export` compatibility methods are removed. Format, import, language, and encoding catalogs remain. |
| CLI rate options | CLI convert command and CLI README | `--frame-rate` remains approximate numeric user input. `--frame-rate-ratio` supplies an exact rational. |

Internal documents, segments, candidates, history, export fingerprints, host
state, frame presentation, and selectors use canonical rates. Internal JSExport
bindings can change with the package wrappers.
