## Context

The current rewrite stores frame display strings such as `240 K` and `240 *` in `Chapter.FramesInfo`. Those suffixes indicate whether the computed frame position is within tolerance, but they are not part of the frame number itself. This conflates domain data, UI display state, and exporter input. It also creates ambiguity for QPFile, where the output should be frame numbers with `I` markers, not copied UI decorations.

The user wants a single QPFile implementation, numeric frame storage, visual accuracy indication, and exports that consistently derive frame numbers from configured rounding behavior.

## Goals / Non-Goals

**Goals:**

- Store frame values as numbers/textual numeric values only, without `K` or `*`.
- Provide separate frame accuracy state: accurate, inexact, and unrounded/unknown.
- Render accurate rounded frames with green non-offset outer glow, inexact rounded frames with red non-offset outer glow, and unrounded frames as black text without accuracy coloring.
- Make frame accuracy tolerance configurable from settings.
- Keep only one QPFile export path and format option.
- Ensure QPFile and celltimes calculate frame numbers from chapter time and selected frame rate using export rounding behavior, not from stored UI marker text.

**Non-Goals:**

- Reintroduce Chapter2Qpfile as a separate tool or compatibility alias.
- Add timecode-file based QPFile conversion in this change.
- Redesign the chapter grid beyond the frame cell presentation needed for accuracy styling.

## Decisions

1. Separate frame accuracy from frame text.

   Core should return frame text and an accuracy enum/value from frame calculation. `Chapter.FramesInfo` can remain as the numeric display text for now, but marker suffixes must move to a separate property used by the UI. This avoids a large model migration while removing the problematic string encoding.

2. Treat unrounded frame display as neutral.

   When rounding is disabled, frame values may be decimal and should not be judged as exact or inexact. The UI will display them in the normal black foreground without green/red glow.

3. Export frame numbers from time and fps.

   Frame-related exporters will not parse `FramesInfo`. They will calculate `chapter.Time.TotalSeconds * fps` and then apply the configured rounding behavior for integer-frame formats. This keeps export output stable even if the UI display text changes.

4. Store frame accuracy tolerance in app settings.

   The tolerance is an application setting because it affects how the chapter grid communicates accuracy. It should default to `0.15`, be normalized to the supported `0.01` through `0.30` range, and snap to recommended `0.05` increments when the configured value is within `0.01` of a recommendation.

5. Use one user-facing QPFile format.

   The single format should be named QPFile in UI text and use `.qpf` as the output extension. The old Chapter2Qpfile path is removed rather than retained as a hidden compatibility mode.

## Risks / Trade-offs

- Existing code may still assume `FramesInfo` contains suffixes -> Update tests and search for suffix parsing, especially editing and export paths.
- Styling frame cells may require a DataGrid template column instead of a plain text column -> Keep the template narrow and preserve current column sizing.
- Export rounding configuration may be ambiguous for unrounded UI mode -> Define exporter behavior explicitly in tests: integer-frame formats always produce integer frame numbers using the selected rounding policy.
- User-provided tolerance could be zero, negative, or extreme -> Normalize settings on load/save and test fallback behavior.
