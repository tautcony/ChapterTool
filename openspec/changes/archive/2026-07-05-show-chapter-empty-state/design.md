## Context

The Avalonia main window already uses a ViewModel-backed `DataGrid` named `ChapterGrid` for chapter rows. The project packages `Assets/**` as content, and `Assets/Images/chapter-empty.svg` is already present but unused. Existing UI guidance requires responsive layout, no WinForms-style absolute positioning, and behavior-level or rendered UI validation for layout changes.

## Goals / Non-Goals

**Goals:**
- Show the existing SVG asset centered in the chapter grid area before chapters are loaded.
- Keep the grid surface available for sizing and context-menu behavior.
- Make visibility driven by observable ViewModel state.
- Validate through headless Avalonia runtime tests.

**Non-Goals:**
- Redesign the chapter grid, columns, or row templates.
- Add new empty-state text or localization strings.
- Change import, export, editing, or command semantics.
- Add a new image-loading dependency.

## Decisions

- Add `MainWindowViewModel.IsChapterGridEmpty` as a read-only binding target derived from `Rows.Count`.
  This keeps XAML simple and avoids converter or expression-binding assumptions around collection count.

- Place the empty-state image in a grid overlay above the DataGrid with `IsHitTestVisible="False"`.
  The DataGrid remains the owned interaction surface for context menus and keyboard behavior, while the image can be visually centered.

- Load the SVG using an Avalonia asset URI in XAML.
  The existing project already copies assets and Avalonia supports `Image` source loading from packaged assets, so no new service or dependency is needed.

## Risks / Trade-offs

- SVG loading behavior can differ by Avalonia version -> Mitigate with a focused app build and headless test that resolves the rendered `Image` control.
- Overlay can accidentally intercept grid input -> Mitigate by setting the overlay non-hit-testable and retaining existing context-menu tests.
- Empty-state visibility can go stale after load/clear -> Mitigate by raising property changed from `Rows.CollectionChanged`.
