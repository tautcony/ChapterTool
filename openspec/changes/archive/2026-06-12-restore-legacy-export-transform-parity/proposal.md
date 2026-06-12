## Why

The legacy parity review found several concrete ChapterTool workflows that are still missing or byte/edge-case incompatible in the Avalonia rewrite. Restoring these now closes high-value export and transform gaps before the new implementation becomes the default path for existing Time_Shift users.

## What Changes

- Add a Core celltimes export path equivalent to the legacy `GetCelltimes()` workflow.
- Add a Chapter2Qpfile conversion workflow that can convert chapter text to QPF output and preserve support for timecode-based conversion.
- Adjust XML export compatibility for legacy consumers, including declaration/comment structure, stable Matroska fields, formatted output, and UID behavior.
- Align timestamp rounding with documented Time_Shift compatibility behavior at millisecond/frame boundaries.
- Add ChangeFps behavior that recalculates chapter times and durations while preserving original frame numbers across frame-rate changes.
- Expand XML output language selection beyond the current short list so users can select ISO language codes comparable to the legacy language list.

## Capabilities

### New Capabilities

- `chapter-conversion-tools`: UI-independent conversion tools for legacy auxiliary workflows such as celltimes and Chapter2Qpfile.

### Modified Capabilities

- `chapter-core-transform-export`: export formatting, rounding, and ChangeFps requirements change to restore legacy-compatible behavior.
- `avalonia-ui-shell`: XML language selection and conversion-tool entry points change so restored capabilities are reachable from the Avalonia UI.

## Impact

- Affected Core areas: chapter export services, time formatting/conversion helpers, frame-rate transform services, and new conversion-tool contracts.
- Affected Avalonia areas: save/export option lists, XML language selection ViewModel/UI, and compact access to restored conversion tools.
- Affected tests: Core export/rounding/transform tests and focused Avalonia ViewModel or UI tests for discoverability and language options.
