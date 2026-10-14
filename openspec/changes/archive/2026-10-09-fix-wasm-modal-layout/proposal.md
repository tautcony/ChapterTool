## Why

The browser history panel and expression diagnostics occupy the main editing grid. In short and landscape viewports, the chapter table covers the history close button. Current browser tests verify data workflows but omit these rendered states from PR layout gates.

## What Changes

- Move history, expression authoring and diagnostics, and advanced export options into dialogs outside the main grid.
- Use one active dialog and a shared native dialog lifecycle for new and existing tools.
- Preserve the chapter editing, expression candidate, history navigation, export, and settings persistence contracts.
- Keep compact format and naming options in the main workflow. Correct short-viewport sizing and scrolling.
- Add browser layout, focus, cancellation, and hit-target checks to the default suite. Add dialog visual states and PR visual gates.

## Capabilities

### New Capabilities

- `wasm-modal-workflows`: Browser tool dialogs, responsive workflow zones, draft isolation, focus lifecycle, and state-based browser regression gates.

### Modified Capabilities

None. Core candidate and history requirements remain unchanged. This change builds on the implemented `live-expression-preview` and `add-wasm-browser-e2e` changes without archiving or rewriting them.

## Impact

Affected areas are `src/ChapterTool.Wasm`, shared localization sources, `tests/ChapterTool.Wasm.E2E`, browser CI, and current browser ownership documentation. No new runtime or test dependency is required. The modal workflow changes browser interaction paths while preserving committed chapter data behavior.
