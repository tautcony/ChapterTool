## Why

The clip selector and XML language selector currently expose terse technical labels, such as raw clip names with chapter counts or bare language codes. These values are accurate but hard to scan during normal chapter-editing work.

## What Changes

- Improve the main-window clip selector display text so each option summarizes the main source content and puts secondary details in a remark-style suffix.
- Improve the XML language selector display text so users see a readable language name while the selected export code remains unchanged.
- Keep existing selection, save, import, export, shortcut, and command behavior unchanged.

## Capabilities

### New Capabilities

### Modified Capabilities
- `avalonia-ui-shell`: Main-window selectors render clearer user-facing display content while preserving existing underlying values.

## Impact

- Affected code: `src/ChapterTool.Avalonia` ViewModel/UI binding surface and focused Avalonia tests.
- No external dependencies, data migrations, import/export format changes, or Core model contract changes are required.
