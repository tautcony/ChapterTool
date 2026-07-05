## Why

The main chapter grid currently renders as an empty table before a source is loaded, even though `chapter-empty.svg` already exists as an intended empty-state asset. Showing the asset in the grid center makes the unloaded state clearer without changing the load workflow.

## What Changes

- Render `Assets/Images/chapter-empty.svg` centered over the chapter grid when no chapter rows are loaded.
- Hide the empty-state visual as soon as chapter rows are available.
- Keep the DataGrid, context menu, columns, and command behavior unchanged.
- Cover the behavior with compiled Avalonia/headless UI tests instead of static source assertions.

## Capabilities

### New Capabilities

### Modified Capabilities
- `avalonia-ui-shell`: Add a visual empty state for the main chapter grid when no chapters are loaded.

## Impact

- Affects `src/ChapterTool.Avalonia` main-window UI and ViewModel state exposed to bindings.
- Affects `tests/ChapterTool.Avalonia.Tests` headless UI coverage.
- No Core, Infrastructure, file format, or dependency changes are intended.
