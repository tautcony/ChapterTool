## 1. Core Authoring Model

- [x] 1.1 Add expression metadata, classification, completion, and analysis result models.
- [x] 1.2 Implement `ExpressionAuthoringService` using the evaluator for final validation.
- [x] 1.3 Add Core tests for metadata, highlighting spans, completions, diagnostics, and suggestions.

## 2. Avalonia Editor

- [x] 2.1 Add AvaloniaEdit dependency and a reusable expression editor control.
- [x] 2.2 Implement syntax highlighting transformer, completion popup, Tab acceptance, and diagnostic display.
- [x] 2.3 Replace main-window and expression-tool `TextBox` inputs with the shared editor.

## 3. Localization And Tests

- [x] 3.1 Add localized editor labels, diagnostic summaries, and correction suggestion strings.
- [x] 3.2 Add Avalonia headless tests for rendering, diagnostics, and Tab completion behavior.
- [x] 3.3 Run OpenSpec validation plus focused Core and Avalonia tests.
