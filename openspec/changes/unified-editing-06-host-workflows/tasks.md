## 1. Host session wiring

- [x] 1.1 Compose Avalonia ViewModels and WasmWorkspace with the Core document session and remove host-local duplicate document ownership.
- [x] 1.2 Add named undo/redo commands, preferred redo, alternate branch selection, and virtualized history panel.
- [x] 1.3 Add platform-specific shortcut catalog entries and preserve text-editor draft undo scope.

## 2. Workflow parity

- [x] 2.1 Add localized applied/exported/failed/history-lifetime status and show candidate diffs before tool commits.
- [x] 2.2 Add behavior tests for Avalonia focus, Escape, shortcuts, branch choice, history virtualization, and browser parity/tab independence.
- [x] 2.3 Regenerate locale JSON with `uv run --project scripts scripts/axaml-to-json.py` and verify `--check`.
- [x] 2.4 Run affected Avalonia ViewModel and Headless projects sequentially, then Wasm tests with `--no-restore`.
- [x] 2.5 Capture default, wide, and narrow Avalonia layout evidence under `artifacts/`.
