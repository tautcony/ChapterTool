## Context

`LogToolViewModel` reads a bounded snapshot from `IApplicationLogService` and
subscribes to `EntryAdded` and `Cleared`. `ApplicationLogPanelProvider` already
owns the bounded in-memory history. `LogEntryViewModel` already formats the
localized summary, technical detail, exception text, raw JSON, and structured
values. The desktop composition already writes rolling Serilog files below
`settings/logs/`.

The current revision added several independent concerns at once. The rendered
surface still auto-selects the first entry, reserves a large hidden detail
column, exposes empty detail tabs, and does not bind grouping, export-format,
status, or search-highlight state. The new design fixes the information
hierarchy first and keeps the provider contract stable.

## Goals / Non-Goals

**Goals:**

- Make the list the dominant surface when the tool opens.
- Let users inspect one event without losing the list context on wide windows.
- Use an explicit inspector action and predictable selection semantics.
- Show concise rows, useful search, severity filtering, and visible match
  feedback.
- Show only populated inspector sections and keep structured expansion state.
- Keep wide and narrow layouts usable, keyboard navigable, localized, and
  screen-reader friendly.
- Keep copy, clear, and JSON/CSV export available as secondary actions.
- Preserve bounded memory, live updates, and existing Serilog archive behavior.

**Non-Goals:**

- Do not add grouping, time-range filtering, or cursor-based history loading.
- Do not change `IApplicationLogService.Entries`, `EntryAdded`, `Cleared`, or
  the provider capacity contract for this visual change.
- Do not create a log database, remote sink, or archive-management workflow.
- Do not change Core APIs, CLI arguments, browser logging behavior, or settings
  persistence.

## Interaction Model

The inspector has its own state. A selected row and an open inspector are not
the same thing.

```text
Open tool
   |
   v
List only  <----- close / Escape <-----  Inspector
   |                                      ^
   | explicit Details action              |
   +--------------------------------------+
          wide: split view
          narrow: inspector replaces list
```

- Opening the tool starts in `List only` with no selected entry.
- Clicking a row selects it and keeps the inspector closed.
- A row details action, Enter, or Space selects and opens that entry.
- Closing the inspector keeps the selected row and list scroll position.
- The same row can open the inspector again through the explicit action.
- A filter change clears selection only when the selected entry is no longer
  visible. It must not select a replacement entry.
- A live entry must not steal focus or open the inspector. If the selected
  entry is evicted, the selection and inspector close together.

## Decisions

### 1. Use a list-first responsive shell

The root layout must give the list all available width when the inspector is
closed. The inspector column and splitter must have zero effective width in
that state. A wide split view may reserve a fixed inspector width between 400
and 480 pixels while the list uses the remaining star-sized width. The exact
width must come from stable layout resources rather than content measurement.

When the list and inspector minimum widths cannot coexist, the view must switch
to an inspector-only state with a localized back action. The view must not rely
on horizontal clipping or a `WrapPanel` for primary toolbar layout.

### 2. Keep rows concise and information-dense

Each row must have a stable height and one primary summary line. The row must
show a severity marker, localized severity accessible text, local time, and a
truncated summary. Operation, category, and event identity may appear in a
muted secondary line only when they are not already represented by the summary.
The row must expose an explicit details action with a localized automation
name. Empty fields must not reserve layout space.

The row template must render the highlight runs produced by the projection. A
search match that exists only in hidden technical data must still retain the
row and expose an accessible indication of the match.

### 3. Make the inspector progressive

The inspector must use a compact context line for level, time, and identity. It
must not repeat the summary in a large header block. It must show the message,
technical detail, properties, exception, and structured data only when those
values exist. Empty sections and placeholder tabs must not occupy space.

Structured values must use cached recursive nodes. A node with children must
bind its disclosure control two-way to `IsExpanded`. The projection must enforce
a finite depth and display a localized or stable depth-limit marker. Rebinding
the selected entry must not reset expansion state.

Raw JSON and copy actions belong to the inspector's secondary action menu. A
user can still select and copy complete technical data without making raw JSON
the default visual surface.

### 4. Keep filtering discoverable but quiet

The primary toolbar must contain search, the visible result count, and one
severity filter affordance. The filter surface must label each control. An
active-filter indicator must be visible without opening the menu. Search must
be case-insensitive across the summary, category, operation, event identity,
technical detail, exception text, and structured/raw values.

This change has no time-range or grouping control. Those controls must not be
reintroduced as disabled or unbound placeholders.

### 5. Put destructive and infrequent actions in overflow

Copy summary, copy details, clear, and export must be available through one
labeled overflow menu or an equivalent secondary action surface. The primary
toolbar must not give all four actions equal visual weight with search. JSON and
CSV choices must be inside the export action surface. Export uses the current
visible membership and must preserve selection and inspector state.

The ViewModel must use `IApplicationLogExporter` when the host provides it. It
must not access the filesystem. Export status and recoverable failures must be
shown in a compact localized status region that is hidden when empty.

### 6. Preserve boundaries and localization

The existing bounded provider and Serilog rolling sink remain the source of
log data. This change must not add provider cursors or a persistent history
index. All user-facing labels, automation names, empty states, and status text
must come from the shared locale AXAML resources. Generated Wasm JSON must be
updated only through the repository conversion script.

### 7. Test visible behavior

Unit tests must cover projection and state transitions. Headless tests must
drive row selection, explicit inspection, closing, reopening, filtering,
search, keyboard focus, and secondary actions. Screenshot captures must cover
default, wide, narrow, and localized layouts. Tests must assert bounds and
visibility outcomes, not only control existence.

## Risks / Trade-offs

- A list-first default changes the previous auto-selection behavior. Tests must
  prove that explicit inspection remains fast and that selection is preserved.
- A narrow inspector-only mode adds a visual state. Headless tests must verify
  the back action and focus restoration at the tool minimum width.
- Rendering highlight runs and cached structured nodes adds projection state.
  The ViewModel must reuse entry projections and update collections once per
  filter or live-entry change.
- Moving actions into overflow can make them less obvious. The overflow button
  must have a stable accessible name, and each menu item must have a tooltip or
  localized label.
- Export can fail for host or file-system reasons. The exporter boundary must
  return a failure result and leave the list and inspector unchanged.

## Migration Plan

1. Remove the previous draft-only grouping, time-range, and cursor-history
   contracts and bindings. Keep the existing entries and live-event contract.
2. Add explicit selection and inspector state. Stop automatic selection and
   automatic inspector opening. Add a row details command and focus rules.
3. Rework the row projection and inspector templates. Cache structured nodes,
   bind expansion state, and render search matches.
4. Replace the fixed split layout and crowded footer with the responsive shell,
   filter affordance, and secondary overflow actions.
5. Keep or finish the existing exporter behind the overflow action. Do not
   modify the automatic Serilog archive.
6. Update locale resources and generated Wasm JSON. Update code-map ownership
   and test guidance.
7. Run focused Avalonia unit tests, Headless tests in a separate process,
   infrastructure export tests, the application build, and the full solution
   test sequence.

Rollback is file-level. Removing the inspector bindings and commands restores
the existing bounded list and logging sink. No provider data migration is
required.

## Resolved Decisions

- The list starts without a selected entry. Reuse of an existing tool window
  may retain state only after the user has explicitly opened that inspector.
- The inspector is a split surface on wide windows and a replacement surface
  on narrow windows.
- The default inspector view is conditional sections, not four permanently
  visible tabs.
- Export defaults to the visible filtered membership and remains secondary.
