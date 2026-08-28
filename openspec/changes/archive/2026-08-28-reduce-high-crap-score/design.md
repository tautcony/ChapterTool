## Approach

Use small, local helpers and immutable lookup tables. Keep orchestration methods linear: validate, execute, render. Preserve existing diagnostics, cancellation, ordering, and UI command semantics.

## Decomposition Groups

1. Core format catalogs: replace switch-heavy `Code` and `Description` methods with shared definitions and direct lookup.
2. CLI: isolate request validation, import execution, export serialization/output, and inspection rendering.
3. Avalonia: extract scalar normalization, completion key policy, pointer hit testing, cell-edit commit, key-command dispatch, and related-media selection.
4. Infrastructure: isolate BDJO resolution, importer registration, and registry value enumeration.
5. HDMV: use operation-specific helpers/tables for `ExecuteSet` and `ExecuteSetSystem`.

Each group must retain existing APIs, diagnostics, and cancellation behavior. New helpers should be private/internal unless tests require a public contract. Tests should assert observable results, not source shape.

## Verification

Run focused Core, Infrastructure, Avalonia unit, and Avalonia Headless tests as applicable, then `dotnet test ChapterTool.slnx --no-restore`. Use `openspec validate reduce-high-crap-score --strict` before completion.
