## Context

The current browser main window exposes Reload and Append only through a context menu. XML language, numbering, and template actions are separated from the compact Avalonia options. Browser history inspection has less detail than the current desktop inspector. Browser settings drafts activate only after Save.

## Goals / Non-Goals

**Goals:** Match portable Avalonia workflows and make the same state transitions visible. Retain Core candidate identity, exact timing, atomic application, and in-memory history. Verify interactions at default, wide, narrow, short, and landscape sizes.

**Non-Goals:** Native browser processes, filesystem paths, system font enumeration, history persistence, and a pixel copy of Avalonia controls.

## Decisions

1. Keep the browser shell in Razor. Expose a load menu through an ordinary named button. Keep one visible `Edit history` action on the main toolbar. Do not add separate Undo or Redo buttons there.
2. Put XML language, numbering, and template input beside the existing format and name controls. Use shared Core candidates for option preview. Candidate presentation shows changed fields in the original rows. Remove redundant visible summaries from the main surface. Put complete metadata in accessible tooltips or a focused dialog.
3. Preserve the original visible frame basis in candidate review. Display preference changes refresh candidate formatting without discarding the candidate. Applying a candidate restores selection and scrolling.
4. Add a browser history projection over Core inspection outcomes. Reuse localized operation and field identities. Separate inspection selection, current cursor, and preferred redo branch. Cancel previous detail requests and reject stale document identities. Provide tree/details navigation at narrow widths.
5. Apply valid browser settings drafts to runtime preferences without writing localStorage. Keep an entry snapshot for cancellation. Save persists one validated aggregate. Persistence failure leaves the dialog and current valid draft available. Do not replay content drafts or change the active session output format while saving its future default.
6. Keep optional export and content tool dialogs. Align useful values, candidate state, accessible actions, and responsive review presentation. Browser activity logs retain browser event data and filtered export; filesystem log folders remain a desktop boundary.
7. Assign independent file ownership to Luna agents. One agent owns Home and main CSS. Another owns history and its projection. Another owns portable tool components. The coordinator owns shared candidate APIs, integration, documentation, and sequential checks.
8. Determine applied expression state from typed operations on the current history branch and track. Naming and numbering changes do not replay the retained expression. Undo and Redo use the actual branch. Keep the script available for explicit expression authoring.
9. Show syntax diagnostics in a floating layer. Keep diagnostics outside normal layout flow so they do not resize the editor, grid, or other workflow zones. Preserve accessible names and keyboard access.

## Difference Checklist

| Area | Current difference | Result |
| --- | --- | --- |
| Load | Reload and Append require right click | Discoverable named load menu |
| Bottom controls | XML language, numbering, template hidden in dialogs | Compact main-window controls with stable localized identities |
| Main candidate | Expression-only inline review and duplicate summaries | Shared options review with compact before/candidate cells |
| Grid | Candidate basis, selection, and read-only behavior need workflow verification | Actual editing, cancellation, application, and export assertions |
| History | Summary-only details and preferred-path redo | Root rows, parameters, change filters, alternate redo choices |
| Main toolbar history | Separate Undo and Redo buttons duplicate the Avalonia history entry | One visible `Edit history` action; keep Undo and Redo in history and shortcut workflows |
| Expression diagnostics | Syntax errors occupy a normal layout row | Accessible floating diagnostics that do not shift workflow layout |
| Settings | Save-only activation | Live valid drafts, Save persistence, Discard rollback |
| Auxiliary tools | Coarse candidate and log detail presentation | Relevant localized values and accessible responsive actions |
| Browser boundaries | File input, downloads, localStorage, JS editor | Retain browser implementations and document intentional differences |

## Risks / Trade-offs

- Live draft changes can update the UI while settings are open. Restore the complete runtime entry snapshot on discard and verify unchanged content/history.
- Independent candidates can become stale after track or history changes. Keep Core base tokens and explicit refresh requirements.
- Narrow layouts need a readable comparison without shrinking timestamps. Allow internal grid scrolling while keeping page controls reachable.
- Parallel builds can lock shared outputs. Run all build and test commands in the coordinator, sequentially.
