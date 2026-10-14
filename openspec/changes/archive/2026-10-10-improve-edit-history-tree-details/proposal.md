## Why

The edit-history dialog renders a retained tree as a flat sequence of buttons. Users cannot see branch relationships or inspect exact changes, and clicking a node immediately restores document content.

## What Changes

- Render the Avalonia history dialog with the native file-tree component and a separate concise detail inspector.
- Distinguish the current document node from the node selected for inspection. Selecting a node must only inspect it.
- Show each node's changes relative to its parent. Include chapter values, additions, deletions, order, track changes, segment changes, and document properties.
- Show the root's initial document overview and read-only chapter values.
- Provide an explicit restore action, preferred redo indication, and alternate redo choices.
- Record structured operation identity, available operation parameters, and commit time with successful history publication. Localize the presentation without parsing English operation descriptions.
- Keep the dialog usable at wide and narrow sizes. Preserve keyboard access, theme integration, modal isolation, and current-session lifetime.
- Exclude history export, history persistence across sessions, arbitrary node-to-node comparisons, branch deletion, and browser history UI redesign.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `session-edit-history`: Add read-only, parent-relative node details and atomic operation metadata. Preserve retained branches, exact restoration, and delta-based storage.
- `avalonia-ui-shell`: Separate history inspection from restoration. Add native TreeView presentation, meaningful node details, responsive layout, and accessible navigation to the existing modal history dialog.

## Impact

- Core history ownership: `src/ChapterTool.Core/Session/SessionEditHistory.cs` and `SessionState.cs`.
- Operation metadata propagation: `ChapterContentOperationSession.cs`, `ChapterWorkspace.cs`, `src/ChapterTool.Avalonia.UI/Workflows/ClipEditingCoordinator.cs`, and the main-window editing and expression command paths.
- Shared Avalonia presentation: `HistoryEntryViewModel.cs`, `ViewModels/Tools/HistoryToolViewModel.cs`, and `Views/Tools/HistoryToolView.axaml` with its code-behind.
- Host boundaries: `PlatformPorts/SessionPorts/WorkspaceToolSession.cs` and the desktop history descriptor in `src/ChapterTool.Avalonia/Services/StandardToolCatalogFactory.cs`. The embedded Avalonia host must use the same inspection behavior.
- Localization: shared AXAML locale resources and generated Wasm locale projections.
- Verification: Core history tests, Avalonia ViewModel tests, Avalonia Headless workflows, and existing Wasm session tests for shared Core compatibility.
- Documentation: update `docs/code-map/avalonia.md` and `docs/code-map/testing.md` during implementation when the new ownership and tests exist.
- No new package, storage format, history export API, or persistence service is required.
