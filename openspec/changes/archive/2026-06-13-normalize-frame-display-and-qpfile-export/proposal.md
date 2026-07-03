## Why

Frame accuracy markers are currently mixed into `FramesInfo` text as `K` or `*`, which makes a display concern look like persisted chapter data and invites exporters to parse UI strings. QPFile output should use frame numbers derived from chapter time and selected frame-rate settings, while the UI should show frame accuracy as visual state.

## What Changes

- **BREAKING**: Chapter frame state will store numeric frame text only; `K` and `*` will no longer be stored in `Chapter.FramesInfo` or edited row values.
- Add explicit frame accuracy state for exact, inexact, and unrounded frame display.
- Render frame text with a non-offset outer glow: green for sufficiently accurate rounded frames, red for rounded frames with excessive error, and black/no accuracy color when frame rounding is disabled.
- Add a durable frame accuracy tolerance setting in the settings panel and use it for frame display/detection accuracy classification.
- Keep a single QPFile export format and remove duplicate Chapter2Qpfile behavior from the user-facing export surface.
- Make frame-related exporters calculate from chapter time, selected frame rate, and the export rounding configuration rather than reading display markers.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `chapter-core-transform-export`: Normalize frame storage and frame-related export semantics so marker state is separate from numeric frame values.
- `avalonia-ui-shell`: Render frame accuracy as styling instead of embedding `K`/`*` text in the frame cell.

## Impact

- Core frame-rate service, chapter model or row projection models, chapter editing service, expression projection, and QPFile/celltimes export paths.
- Avalonia chapter row ViewModel, frame column binding/template, styles/resources, settings ViewModel, persisted settings, and preview/save format labels.
- Existing tests expecting `FramesInfo` values like `240 K` or `240 *` must be updated to validate numeric storage plus separate accuracy state.
