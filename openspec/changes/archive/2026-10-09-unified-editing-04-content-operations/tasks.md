## 1. Candidate adapters

- [x] 1.1 Add candidate builders for insert, delete, reorder, cell value, numbering, and metadata operations.
- [x] 1.2 Adapt template naming, regular-expression replacement, offsets, FPS actions, and expressions to calculate from captured snapshots.
- [x] 1.3 Add complete candidate validation for time order policy, ticks overflow, end-before-start, scaling, and per-row failures.

## 2. Command integration

- [x] 2.1 Route each existing content command through session apply and remove its direct document mutation path.
- [x] 2.2 Preserve editor drafts, focus/Enter/Tab/Escape behavior, and block dependent commands on invalid drafts.
- [x] 2.3 Add Core and host behavior tests proving commit/undo/redo identity and whole-batch failure for every operation family.
- [x] 2.4 Run the Core, Avalonia ViewModel, and relevant Headless test projects sequentially with `--no-restore`.
