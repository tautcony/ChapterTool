# Frame-rate compatibility inventory

The following external boundaries must remain usable through major version 23.
The inventory uses current distribution sources and documented examples.
A public modifier alone does not establish a compatibility obligation.

| Boundary | Evidence | Retained contract |
| --- | --- | --- |
| `ChapterSet` | Core README import, edit, transform, and export examples; importer and exporter extension interfaces | Numeric constructor parameter `FramesPerSecond`, named arguments, positional deconstruction, record `with` assignments, and numeric result getter. The existing `ExactFrameRate` result member must derive from current canonical data. |
| `ChapterEditingService` | Core README editing feature; Node `editFrame`, `shiftFramesForward`, and `createZones` wrappers | Concrete numeric FPS operation parameters and existing result shapes. Repository injection interfaces must use canonical rates. |
| `ChapterFpsTransformService.ChangeFps` | Core README named `sourceFps` and `targetFps` example | Existing decimal signature and result shape. |
| `FrameRateService` | Core README detection example; Node catalog, detection, and frame projection methods | Concrete numeric lookup and existing `FrameRateOption`, `FrameInfoResult`, and detection result projections. Repository injection interfaces must use canonical rates. |
| `IChapterExpressionEngine` | Core README explicitly documents a pluggable engine and recommends implementing this interface | `Evaluate` and the existing `ChapterExpressionContext` construction, deconstruction, and record updates. A canonical internal context must adapt at this extension boundary. |
| `ChapterExpressionService` and expression authoring | Core README transform feature; Node expression and analysis wrappers | Existing numeric context input and evaluation result. Lua numeric FPS globals remain projections. |
| `ChapterExportService`, `IChapterExporter`, `ChapterExportOptions`, and `ChapterSerializationOptions` | Core README export example, extension guidance, and explicit major-version-23 lifetime | Existing constructors, named arguments, records, transform-bearing export, and format-only serialization. Typed overrides are additive. |
| `ChapterConversionService` | Core README conversion feature; Node `toCelltimes` and `chapterTextToQpfile` wrappers | Numeric signatures, optional timecode input, and results. |
| `ChapterSegmentService` | Core README combine/append feature; Node `combine` and `append` wrappers | Scalar input and result shapes. Mixed rates must produce a conflict. |
| Node `ChapterTool` | `packages/chaptertool/src/index.ts`, `types.ts`, and package tests | All existing method signatures and numeric DTO fields. Callers can change returned FPS and seconds before resubmission. Existing DTOs must not gain higher-priority canonical inputs. |
| CLI numeric option | CLI convert command and CLI README | Existing `--frame-rate` input syntax. Internal request types have no external compatibility requirement. |

Internal documents, segments, candidate builders, history, export fingerprints,
workspace state, frame presentation, and host selector contracts must migrate directly.
The session-kernel feature description does not establish a numeric FPS overload contract.
Internal JSExport bindings can change with the package wrappers.

`scripts/ci/verify-nuget.py` currently checks only formatter behavior and CLI help.
It does not yet verify frame-rate model usage or the expression extension boundary.
Task 8.2 must add compiled consumers for those retained contracts.
