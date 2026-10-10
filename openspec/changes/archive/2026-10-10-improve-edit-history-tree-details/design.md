## Context

`SessionHistoryTree` retains an immutable root and reversible `DomainChangeSet` values. `SessionHistoryNodeSnapshot` exposes parent, children, preferred child, and a description. It does not expose node details, operation parameters, or commit time.

`HistoryEntryViewModel.Create` produces a depth-first list. `HistoryToolView` renders each item as a navigation button. Its template does not use the recorded depth for layout. `HistoryToolViewModel` delegates navigation through `IHistorySessionPort`.

`ChapterContentPreview.Differences` contains formatted strings. It does not cover all segment properties or stable track matching. The new history inspector must use typed history data rather than these preview strings.

The desktop history tool is modal. The embedded Avalonia host also uses the shared view. The browser has a separate `HistoryDialog.razor`; its presentation is outside this change.

## Goals / Non-Goals

**Goals:**

- Make the retained branch topology visible.
- Let users inspect every node without moving the document cursor.
- Explain exact changes from a node's parent, including structural and property-only edits.
- Make restoration explicit and keep alternate futures reachable.
- Preserve responsive layout, keyboard access, localization, session lifetime, and delta-based storage.

**Non-Goals:**

- History export or history persistence across sessions.
- History import, branch deletion, automatic pruning, or saved history checkpoints.
- Comparison between arbitrary nodes.
- Rebuilding the browser history dialog or unrelated preview surfaces.
- A free-form graph editor, node dragging, pan/zoom, or a new graph package.

## Decisions

### 1. Separate inspection state from the document cursor

`HistoryToolViewModel` must own `SelectedNodeId`, stable hierarchical node models, expansion state, detail request state, and the narrow-layout page. Core must continue to own the document cursor and preferred redo edges.

On first open, select the current node and expand its ancestor path. Reopening starts from the current node; it does not persist the previous dialog's selection. Clicking or using arrow keys only changes inspection selection. Double-click and Enter must not restore content. Enter moves focus to the inspector. An explicit restore button invokes the existing session navigation path.

After a successful restore, undo, or redo, select the resulting current node, expand its ancestors, and bring it into view. Preserve other expansion choices in the open dialog. On failure, keep the current node and inspection selection. Display the failure beside the actions.

The restore button is disabled for the current node, an invalid selection, an ended session, or an active navigation command. A detail-query failure must not disable an otherwise valid restore target. Duplicate restore submissions must be blocked while navigation is running.

Alternative considered: retain immediate navigation on node click and add a separate details icon. This makes ordinary browsing change the document and creates small repeated hit targets. Inspection-first rows make the behavior predictable.

### 2. Render the retained topology with the native TreeView

Use Avalonia `TreeView` controls in wide and narrow layouts. Each stable node view model exposes its actual child nodes through a bound children collection. The control supplies the file-tree expansion, indentation, selection, and keyboard behavior. Do not draw a separate lane rail or flatten parent and child nodes into a visual list.

Build the child model iteratively from retained child IDs. Preserve node view model instances by ID across refreshes so selection and expansion remain stable. Expand the current node's ancestor path when the dialog opens and after navigation. Inspection selection and current position remain separate. A selection event with no item during TreeView materialization must not clear the retained selection or inspector.

Keep native collapse behavior. Collapsing an ancestor retains the inspected descendant and its details. Expanding the ancestor makes that row visible again. Locate Current expands and reveals the document cursor without restoring content.

Each row shows the localized operation title and a distinct Current marker. The TreeView selection state marks the inspected node. Show a preferred redo label in the explicit redo choices. Use a tooltip for a truncated title and expose current, selection, and expansion state through accessibility properties.

### 3. Keep the inspector compact and data-led

Show the selected node title, one local timestamp, nonzero effect counts, meaningful captured parameters, and actual before/after changes. Hide empty descriptions, status text, parameter headings, change filters, and columns. Do not show explanatory comparison paragraphs. Do not repeat a field's new value above its before/after row.

For chapter edits, identify the object by its historical track and chapter position. This context must not repeat a changed chapter name. Show the Delta column only when a numeric time or duration change has a real delta. Show the initial document's source and ordered read-only chapter values on the root node.

Render field details in a virtualized list. Keep selectable text for copy operations. The details page keeps Restore in a fixed footer.

### 4. Add a read-only Core detail query

Add a typed query on `SessionState` for one retained node. A result must identify the captured root/session, node, parent, operation metadata, summary, and typed changes. Distinguish available, not found, ended/canceled, and recoverable resource failure results.

Capture one immutable history version. Resolve both sides from that version. Querying must not enter a mutation transaction, change the cursor, update preferred children, invalidate a preview, or execute an operation. The query must link caller cancellation with `LifetimeToken`.

The authoritative difference is the stored edge change set. Project it into public read-only DTOs without exposing the internal mutation types. Resolve missing context, such as unchanged track names, chapter positions, or segment frame rates, from private parent-state reconstruction when needed. Use the existing iterative change-set reconstruction approach. Do not publish reconstructed states. Do not retain a complete document snapshot at every node.

Keep tree summaries lightweight and compute them from committed changes. Load field-level details only for the selected node. The dialog retains at most the selected detail result; cancel and release replaced results. It must not populate details for every retained node.

For the root, return the captured initial document overview and ordered read-only chapter values. It has no parent-relative changes. A single-chapter edit must retain delta-sized history storage even when a user inspects it.

Alternative considered: navigate to the parent and target, then compare the live document. This mutates session state during inspection. Reusing preview string differences would miss typed and structural information.

### 5. Match changes by stable identity and preserve value meaning

Use document, track, chapter, and segment identities for matching. Preserve retained track order and before/after chapter positions for display. Names are labels, never matching keys.

Typed changes must cover all editable document properties; track name, membership, and order; chapter number, name, start/end ticks, frames, accuracy, kind, membership, and order; and segment membership, order, placement, duration, rates, and source/media metadata. Metadata-only changes must produce visible detail rows even when no chapter changes.

Count distinct added, removed, and modified chapters. A retained chapter with several field changes or an order change counts once as modified. Added or removed chapters must not also count as modified. Track replacement may show complete added/removed track groups. When a stable chapter moves between tracks, identify it as a move rather than unrelated deletion and addition. Generated identities from merge/split operations must be shown honestly; do not invent identity correspondence.

Present unchanged values only as contextual labels. Distinguish object absence, null/unknown values, and empty text. Show exact signed time deltas. A rounded visible value must expose precise time in expanded detail so submillisecond edits remain visible. Resolve frame context from the corresponding history state and captured operation parameters. Do not use the current main-window FPS selector to reinterpret a past change. If the rate is unknown, label it unknown.

Group changes by track and chapter. Show a compact count line only for nonzero effects. Show parameters only when they provide information beyond the field rows. Provide All, Added, Removed, and Modified filters when changes exist. Keep the filter available when its current choice has no results. Changes in document and track properties belong to Modified. Use virtualization or incremental realization for large groups. Wide mode uses Field / Before / After columns. Narrow mode stacks labeled before/after values. Text must support selection and copying.

### 6. Capture operation metadata with successful publication

Introduce a small immutable operation descriptor with a stable kind, optional typed parameter payload, and diagnostic fallback description. Core must assign commit time when publishing the history node. Root load time is captured when the session is created.

Keep existing description-based callers source-compatible with an optional descriptor. Missing metadata uses a localized generic edit title and exposes a meaningful original description as secondary detail. Do not parse descriptions such as `Edit Name` to classify nodes. Hide missing time and parameters. Never fabricate metadata.

Avalonia commands that publish content changes must supply descriptors for cell edits, insert/delete, expression application, template/naming/numbering operations, frame shifts, frame-rate changes, merge/split, and append. Propagate them through `ClipEditingCoordinator`, `ChapterContentOperationSession`, and the relevant workspace entry points. Capture script text, scope, and effective parameters with the reviewed candidate. Apply must retain those captured values even if the live draft changes. Capture display FPS where it affects the operation. A display-only frame update must retain its existing history-free behavior.

Publish descriptor, time, changes, and node in the same atomic state swap. Failed, canceled, stale, and no-change requests must not create metadata-only nodes. An idempotent retry must reuse the original node and time. Undo/redo must preserve metadata and must not rerun scripts.

Operation descriptors contain immutable data only. They must not retain views, command delegates, service objects, or live draft references. Do not add a general dictionary-based audit framework. The metadata remains owned by the in-memory session.

Alternative considered: store localized operation titles. This would freeze the language used at commit time. Stable operation kinds let the UI change language without rewriting history.

### 7. Define dialog geometry and action behavior

Use a target default size of 1000 by 700 DIPs and a minimum size of 520 by 420 DIPs, constrained to the available host area. At an available content width of at least 800 DIPs, show tree and inspector with a draggable splitter. Start at 40/60 proportions and protect minimum pane widths of 280 and 360 DIPs. Below that width, show History and Details pages. Selection stays visible on returning to History. Switching layout must preserve selection, expansion, and scroll anchors.

The header contains Undo, Redo with alternate-choice affordance, and Locate Current. The session-lifetime notice is secondary text. The footer contains status, Restore to This Node, and Close. In narrow mode, show Restore on the Details page. Footer actions must remain reachable independently of scroll position.

In narrow mode, selecting a row keeps the History page open. Enter or the Details tab opens the selected inspector. The History tab returns to the selected row and its scroll anchor.

The wide layout follows this wireframe. The tree uses standard file-tree expanders. The Current marker and selected-row highlight have separate meanings.

```text
+--------------------------------------------------------------------+
| Edit history                                                       |
| Undo | Redo v | Locate current       Session-only history            |
+-------------------------+------------------------------------------+
| History tree            | Selected operation: Shift                |
| v Initial document      | 1 chapter modified                       |
|   v Rename              |                                          |
|     v Expression        | Object / Field / Before / After          |
|       Rename [current]  | Chapter 3 / Start / 00:10 / 00:11         |
|     Shift [selected]    |                                          |
|                         |                                          |
|                         |                                          |
|                         | Shift: +24 frames; effective FPS: 24     |
+-------------------------+------------------------------------------+
| Status                                 Restore to this node | Close |
+--------------------------------------------------------------------+
```

Labels such as `修改章节名称`, `修改前`, `修改后`, `恢复到此节点`, and `当前状态` must come from the shared locale resources. The wireframe illustrates composition only; its example values do not define a required fixture.

Redo runs from the current document node, not the inspection selection. If the current node has multiple direct children, offer those choices with localized summaries and mark the preferred child. Selecting a redo choice is an explicit navigation action. Viewing another node must not change the preferred redo child.

The native TreeView owns arrow-key selection and expansion. Enter focuses details. Tab reaches header, tree, inspector, restore, and close controls. Escape preserves the existing dialog-close behavior. Focus returns to the history button after close.

Inspection leaves pending content previews intact. Before an explicit restore, undo, or redo, show an inline notice that navigation discards the pending preview. Use the existing command invalidation path when the user executes that action. A failure may still leave that preview discarded; document and cursor must remain unchanged. Do not add a confirmation modal to routine reversible navigation.

Alternative considered: stack both panes at all widths. This reduces the tree viewport and makes navigation through long history difficult.

### 8. Keep session and asynchronous ownership explicit

Extend `IHistorySessionPort` with typed history/detail access while preserving command ownership in the main-window session. `HistoryToolViewModel` owns a cancelable detail request and a selection generation. Accept a result only when root identity, selected node, request generation, and view lifetime still match.

Query loading and errors appear only in the inspector. A stale completion must not replace newer details. Session end clears selection, rows, details, and actions. Closing the dialog cancels detail requests and releases subscriptions and detail payloads. Restore/navigation execution state must follow the existing observed command boundary.

Both native and embedded Avalonia hosts must use the same view and port behavior. Shared Core API changes must preserve Wasm callers. No browser UI tasks or new browser persistence behavior are included.

## Risks / Trade-offs

- [Tree refresh can lose selection or expansion] -> Reuse canonical node models and verify native control workflows with deterministic branch fixtures.
- [Old operation descriptions have no structured context] -> Preserve exact diffs and label missing metadata explicitly. Add descriptors at actual command entry points.
- [Structural changes need context beyond the stored changed fields] -> Reconstruct a private parent state on demand and release it after projection.
- [Large histories and root inspections can allocate substantial UI data] -> Build hierarchy iteratively, keep collapsed descendants unrealized, virtualize detail rows, cancel obsolete queries, and measure deep-history and large-batch cases.
- [Shared Core changes can affect browser commands] -> Keep optional metadata additive and run existing Wasm workspace and parity tests.
- [Localized text and enlarged fonts can crowd actions] -> Verify responsive geometry, accessible names, dark theme, and enlarged-font layouts through Headless workflows.

## Migration Plan

1. Add Core DTOs, read-only queries, and optional metadata while preserving existing calls.
2. Route Avalonia operation descriptors and history detail access through the existing session ports.
3. Replace the history row button interaction with tree selection and explicit restoration.
4. Add locales, generate Wasm locale projections, and verify shared-host behavior.
5. Run affected tests sequentially. Capture review images under `artifacts/edit-history-tree-details/` at default, wide, and narrow sizes.
6. Update the code map when implementation owners and primary tests are established.

This change needs no stored-data migration. Rollback restores the previous dialog and removes the new optional query/metadata paths. It does not require a history file conversion.

## Open Questions

No product-scope decision blocks implementation. Geometry values are initial implementation targets. Adjust them only when rendered evidence requires it, while preserving the specified responsive behavior.
