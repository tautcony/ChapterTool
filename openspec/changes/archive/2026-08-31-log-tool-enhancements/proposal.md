## Why

The current LogTool opens a technical panel as soon as it creates the first
selection. The panel repeats the message and identity, keeps several empty
tabs visible, and leaves large unused areas for sparse entries. The fixed
master-detail columns also clip content at the tool minimum width. The footer
places diagnostic actions beside the primary search workflow and makes the
common view harder to scan.

Users need a quick answer to two different questions:

1. Which recent operation needs attention?
2. What technical evidence belongs to one selected event?

The first question must be answered by the list. The second question must be
available on demand. The existing bounded log provider, live-entry events, and
Serilog rolling files already supply the required data. A visual redesign must
not expand that provider contract with speculative history or grouping APIs.

## What Changes

- Open the tool in a list-first state with no automatic selection or inspector.
- Render each row as a stable one-line summary with a severity marker and local
  time. Show operation, category, and event identity only as compact secondary
  context when they add information.
- Add an explicit details action for each row. Selecting a row must not open the
  inspector by itself. Enter, Space, or the accessible row action can open it.
- Show a compact inspector only after an explicit request. Use a narrow header
  for the selected event and show only non-empty technical sections.
- Keep the list available beside the inspector at a wide width. Replace the
  list with the inspector and a back action when the two surfaces cannot fit.
- Keep search and severity filtering in the primary toolbar. Put copy, clear,
  and JSON/CSV export in a labeled secondary overflow surface.
- Render search matches in the row instead of exposing highlight metadata that
  the view does not consume.
- Keep structured values expandable with stable, two-way expansion state and a
  finite depth limit.
- Preserve bounded live updates, selection state, keyboard focus, and localized
  resource boundaries.
- Keep the existing automatic Serilog archive unchanged. Manual export, when
  available on the host, must use the existing exporter boundary.

## Deferred From This Change

- Level grouping and grouped virtualized sections.
- Time-range filters.
- Cursor-based history pagination and older-page loading.
- A new persistent log database or a change to the bounded provider capacity.

These features may be proposed separately after the list and inspector
workflow has evidence from real use.

## Capabilities

### New Capabilities

- `log-tool-viewing`: List-first presentation, concise summaries, explicit
  inspection, responsive behavior, search, severity filtering, and accessible
  structured details.
- `log-export-archive`: Secondary JSON/CSV export and preservation of the
  existing automatic archive behavior.

### Modified Capabilities

None.

## Impact

- Avalonia UI: `src/ChapterTool.Avalonia.UI/Views/Tools/LogToolView.axaml`,
  `src/ChapterTool.Avalonia.UI/ViewModels/Tools/LogToolViewModel.cs`, and
  `src/ChapterTool.Avalonia.UI/ViewModels/Tools/LogEntryViewModel.cs`.
- Host composition: `src/ChapterTool.Avalonia/Services/StandardToolCatalogFactory.cs`
  only when the inspector needs a host capability already present in the
  catalog.
- Optional export adapter: `src/ChapterTool.Infrastructure/Platform/ApplicationLogFileExporter.cs`.
- Localization: `src/ChapterTool.Avalonia.UI/Localization/Resources/Locales/*.axaml`
  and generated Wasm resources through `python3 scripts/axaml-to-json.py`.
- Tests: `tests/ChapterTool.Avalonia.Tests`,
  `tests/ChapterTool.Avalonia.Headless.Tests`, and the focused infrastructure
  export tests when export remains enabled.
- Documentation: `docs/code-map/avalonia.md` and `docs/code-map/testing.md`.
- Core, CLI, browser log contracts, settings schema, and automatic archive
  retention are outside the change.

## Success Criteria

- A default screenshot shows the list as the dominant surface and no inspector.
- An explicit details action opens the inspector without losing list context.
- Closing and reopening details does not change the selected row or scroll
  position.
- Default, wide, narrow, and all three supported locales render without
  clipping or overlapping controls.
- Unit and Headless tests prove the state transitions and visible outcomes.
