## Context

The current Avalonia shell binds expression input to plain `TextBox` controls in the main window and expression tool. Core already centralizes expression evaluation in `ExpressionService` and emits structured `InvalidExpression.*` diagnostics, but the UI has no editing model for token knowledge, highlighting, completions, or inline correction advice.

## Goals / Non-Goals

**Goals:**
- Provide a complete expression editor experience in one implementation pass.
- Keep expression language knowledge in Core so UI and tests do not duplicate token lists.
- Use a real editor foundation for caret, selection, syntax spans, completion, and keyboard behavior.
- Present specific syntax problems and correction suggestions before save/export.
- Reuse one Avalonia editor control in all expression entry points.

**Non-Goals:**
- Change expression evaluation semantics or add a new expression language.
- Add multi-line scripting or user-defined functions.
- Rebuild a general code editor framework inside ChapterTool.

## Decisions

1. Use AvaloniaEdit as the editor foundation.

   AvaloniaEdit is maintained by the Avalonia project and has a 12.x package matching this app's Avalonia 12 stack. It provides text rendering, caret behavior, highlighting transformers, and completion windows. Building equivalent behavior on `TextBox` would require fragile event and overlay code and still miss editor fundamentals.

2. Add Core expression authoring APIs beside `ExpressionService`.

   `ExpressionAuthoringService` will expose metadata, lexical/classification spans, completions, and validation diagnostics. It will reuse `ExpressionService` evaluation for final grammar checks, keeping runtime behavior aligned with existing transformations. The UI will consume this service rather than parsing source files or hard-coding token lists.

3. Treat syntax feedback as semantic diagnostics with suggestions.

   Diagnostics will include the existing diagnostic code, localized message text from the shell, and a stable suggestion key/text. The UI can show a compact status line and tooltip, while tests assert behavior through public APIs and rendered controls.

4. One reusable Avalonia control for all expression inputs.

   `ExpressionEditor` will wrap AvaloniaEdit, bind `Text`, `DiagnosticText`, `SuggestionText`, and completion items, and own Tab acceptance behavior. Main window and expression tool use the same control to avoid drift.

## Risks / Trade-offs

- [Risk] AvaloniaEdit adds a new UI dependency. → Mitigation: pin the 12.0.0 package and keep usage isolated under `Views/Controls`.
- [Risk] Analyzer and evaluator grammar can diverge. → Mitigation: Core authoring service uses evaluator validation for final correctness and shares a public metadata catalog for known tokens.
- [Risk] Completion popups can be hard to test headlessly. → Mitigation: test Core completion logic directly and cover the Avalonia control's Tab behavior and diagnostic rendering with headless tests.
- [Risk] Revalidating on every keystroke could refresh rows too aggressively. → Mitigation: validation stays in the editor VM/control path; existing `Expression` binding behavior remains, but diagnostics do not apply transformations independently.
