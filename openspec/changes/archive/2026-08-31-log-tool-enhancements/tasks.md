## 1. Scope And Boundary Cleanup

- [x] 1.1 Record the current list, provider, localization, and host composition
  behavior before changing the view.
- [x] 1.2 Remove the previous draft-only grouping, time-range, and cursor-history
  contracts, commands, bindings, and tests from this change.
- [x] 1.3 Keep `IApplicationLogService.Entries`, `EntryAdded`, `Cleared`, and the
  bounded provider capacity contract unchanged.
- [x] 1.4 Keep `IApplicationLogExporter` optional and host-injected when manual
  export is enabled. Keep automatic Serilog archive settings unchanged.

## 2. Projection And ViewModel State

- [x] 2.1 Make the initial state list-first with a null selection and a closed
  inspector. Remove automatic selection on load, filtering, and live updates.
- [x] 2.2 Add explicit open, close, and narrow back commands with selection,
  focus, and scroll-preservation rules.
- [x] 2.3 Build concise summary and optional context projections. Omit empty or
  duplicated operation, category, and event identity fields.
- [x] 2.4 Cache structured nodes per entry, bind mutable expansion state two-way,
  and keep the finite depth-limit behavior.
- [x] 2.5 Implement case-insensitive search across visible and technical fields,
  severity filtering, visible counts, and rendered highlight runs.
- [x] 2.6 Keep live additions and capacity eviction incremental. Do not steal
  focus or open the inspector when a new entry arrives.
- [x] 2.7 Keep copy, clear, and export commands secondary. Preserve all view state
  after export success or recoverable failure and expose localized status.

## 3. Responsive Avalonia View

- [x] 3.1 Make the list fill the content width when the inspector is closed. Do
  not reserve a hidden detail column or splitter.
- [x] 3.2 Render stable one-line rows with severity marker, local time, concise
  summary, optional context, and an accessible explicit details action.
- [x] 3.3 Render the inspector with a compact context line and populated sections
  only. Keep raw data and copy actions in the inspector or action overflow.
- [x] 3.4 Bind structured disclosure controls to `IsExpanded` and provide
  keyboard-accessible names and focus behavior.
- [x] 3.5 Add a wide split state and a narrow inspector-only state with a back or
  close action. Verify no horizontal clipping at supported widths.
- [x] 3.6 Replace the crowded footer with search, count, severity filter, and a
  labeled secondary overflow surface. Use stable Grid sizing for the toolbar.

## 4. Localization And Host Wiring

- [x] 4.1 Add or update concise row, inspector, filter, overflow, status, and
  accessibility strings in `en-US`, `zh-CN`, and `ja-JP` locale AXAML files.
- [x] 4.2 Run `python3 scripts/axaml-to-json.py` and
  `python3 scripts/axaml-to-json.py --check`; do not edit generated Wasm JSON
  by hand.
- [x] 4.3 Register the optional exporter through the existing desktop
  composition without adding filesystem access to the ViewModel.
- [x] 4.4 Verify that `LoggingModule` still creates `settings/logs/` and keeps
  the existing rolling Serilog sink and retention behavior.

## 5. Tests And Documentation

- [x] 5.1 Add Avalonia unit tests for list-first state, explicit inspection,
  close/reopen, selection preservation, filtering, search highlights, live
  updates, eviction, structured expansion, and secondary command state.
- [x] 5.2 Add infrastructure exporter tests for UTF-8 JSON/CSV output, quoting,
  visible scope, and recoverable failures when export remains enabled.
- [x] 5.3 Add Headless workflow tests for row selection, details action, keyboard
  and Escape behavior, populated/empty inspector sections, overflow actions,
  localization, and narrow replacement layout.
- [x] 5.4 Capture default, wide, narrow, and localized screenshots under
  `artifacts/` and review bounds, clipping, focus surfaces, and information
  hierarchy.
- [x] 5.5 Update `docs/code-map/avalonia.md` and `docs/code-map/testing.md` with
  the final ownership and test boundaries.
- [x] 5.6 Run the focused Avalonia unit project, the Headless project in a
  separate process, focused infrastructure tests, the Avalonia build, and the
  full solution tests sequentially.
- [x] 5.7 Run `openspec validate "log-tool-enhancements" --strict` and review all
  changed artifacts before archive.
