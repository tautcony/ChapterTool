## Why

Frame rates pass through `double`, `decimal`, and optional rational fields. Adapters can create a rational from a rounded numeric FPS value. Exact source rates can then become approximate during editing or export. The current proposal also removes public result fields without a compatibility path and leaves exact API inputs undefined.

Core needs one canonical rate value. Hosts and external callers need explicit rules for source metadata, computation choices, detection suggestions, and output precision.

## What Changes

- Introduce an immutable `FrameRateValue` with rational, approximate, and unknown states.
- Preserve rate kind and payload through import, documents, segments, candidates, history, transforms, and export snapshots.
- Use timestamp ticks and exact integer arithmetic until a declared rounding boundary. A decimal coefficient can participate in integer arithmetic without becoming an authoritative source rational.
- Replace internal models, service signatures, host state, and catalog selections directly with the canonical type. Keep compatibility only for manual numeric input and established external API signatures/result fields.
- Keep the current Node facade and JSON shapes. Add `tool.exact` for typed rate inputs and results with lossless tick strings. Both facades must share Core behavior.
- Add CLI `--frame-rate-ratio N/D`. Keep `--frame-rate` as approximate numeric input. The options must be mutually exclusive and bound through DotMake.CommandLine.
- Keep Auto detection available for suggested display. Unknown or mixed-rate frame operations must require an explicit computation rate. Time-only operations must remain available.
- Preserve original source rates separately from current rates after an explicit FPS conversion. Recompute aggregate rates across all participating segments.
- Cover compatibility, invalid/default values, overflow, mixed exact/approximate calculations, selector lifecycle, and complete desktop, Web, CLI, and Node workflows.

## Capabilities

### New Capabilities

- `public-frame-rate-contracts`: Define typed public rate inputs, lossless Node wire data, legacy compatibility, and error contracts.

### Modified Capabilities

- `editable-chapter-document`: Store canonical rates, source rates, and deterministic aggregate metadata.
- `chapter-core-transform-export`: Use typed rate arithmetic, explicit operation contexts, and documented quantization.
- `disc-playlist-media-importers`: Preserve source authority per play item and distinguish video FPS from source time bases.
- `command-line-conversion-workflows`: Add exact ratio input and explicit rate inspection labels.
- `clip-structure-editing`: Preserve segment rates and reject unsupported scalar-rate operations.
- `avalonia-ui-shell`: Keep Auto as a suggestion and preserve custom rate identity.
- `wasm-content-operation-parity`: Use the same rate resolution, candidate, and display rules in both hosts.

## Impact

Core models, import projections, frame services, expressions, conversion tools, exporters, history comparison, and export fingerprints must change. Avalonia and Web workspace consumers must migrate together. Node C# DTOs, handwritten TypeScript source contracts, generated declarations, package tests, and public documentation must change together.

Implementation must verify the full solution and affected host/distribution consumers. This review changes planning artifacts only. Existing unrelated working-tree changes must remain intact.
