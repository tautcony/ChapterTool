## Why

Expression input is currently a plain text field, so users must know the token set, function signatures, and grammar rules from memory. Syntax failures surface only after evaluation and do not provide focused correction guidance in the editing surface.

## What Changes

- Add first-class expression language metadata in Core for variables, constants, functions, operators, examples, and correction hints.
- Add a reusable expression analyzer that returns syntax classification spans, completion candidates, and diagnostics without applying chapter transformations.
- Replace expression text inputs in the main window and expression tool with a shared Avalonia editor based on AvaloniaEdit.
- Provide syntax highlighting, context-aware completion, Tab acceptance, and localized validation messages with actionable suggestions.
- Keep expression evaluation semantics compatible with the existing `ExpressionService`.

## Capabilities

### New Capabilities
- `expression-authoring`: Expression authoring metadata, validation, highlighting classification, completion, and correction suggestions.

### Modified Capabilities
- `avalonia-ui-shell`: Main and tool expression inputs become full expression editors with highlighting, completion, and validation feedback.

## Impact

- `src/ChapterTool.Core`: new expression authoring analyzer APIs layered beside existing evaluator behavior.
- `src/ChapterTool.Avalonia`: add AvaloniaEdit dependency and a reusable expression editor view/control.
- `tests/ChapterTool.Core.Tests`: analyzer and completion coverage.
- `tests/ChapterTool.Avalonia.Tests`: headless coverage for editor rendering, completion, and diagnostics.
