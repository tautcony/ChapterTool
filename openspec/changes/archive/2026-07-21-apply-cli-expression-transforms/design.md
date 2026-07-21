## Context

The GUI constructs `ChapterExportService` with the shared `LuaExpressionScriptService`. The CLI currently constructs the same service with a null expression engine and always sends `ApplyExpression: false`. Core already applies expressions through `ChapterOutputProjectionService`, so the missing behavior is limited to CLI composition, option binding, and diagnostics.

## Goals / Non-Goals

**Goals:**

- Expose opt-in CLI conversion options for Lua expression text and built-in preset selection.
- Use the same Lua engine and preset identifiers as the GUI.
- Apply expressions inside the existing Core export projection so every output format receives the transformed chapter times.
- Reject conflicting expression options and unknown presets before import or export.
- Preserve existing output, selection, frame-rate, and diagnostic behavior.

**Non-Goals:**

- Add a second expression language or a CLI-only expression evaluator.
- Add expression editing, authoring assistance, or interactive GUI behavior to the CLI.
- Apply expressions to `inspect` or implicit GUI startup paths.
- Change the Core expression semantics.

## Decisions

- Inject `LuaExpressionScriptService` into the default CLI export service through `AppCompositionRoot.CreateSharedExportService`. This keeps the GUI and CLI on one engine and makes built-in presets available without duplicating their definitions.
- Add `--expression` and `--expression-preset` to `ConvertCliCommand`, and carry both values in `CliConvertRequest`. The CLI validates that at most one is selected and resolves a preset to its `ScriptText` before export.
- Pass `ApplyExpression`, `Expression`, `ExpressionPresetId`, and `ExpressionSourceName` into `ChapterExportOptions` while leaving `ProjectOutput: true`. This preserves the existing Core projection order and ensures transformed times are used by all exporters.
- Treat expression diagnostics like other export diagnostics. A failed Lua evaluation leaves the affected chapter unchanged and produces a diagnostic; the CLI will return a non-zero result when the export result is unsuccessful, while warnings remain visible on successful output.
- Keep the options opt-in. A conversion without either option sends `ApplyExpression: false`, preserving current output.
- Update `formats` text and generated option descriptions to state that expression transforms are supported by `convert` through explicit options.

## Risks / Trade-offs

- [Risk] A user can select a preset whose script produces warnings or invalid times. -> [Mitigation] Reuse Core normalization and diagnostic behavior, print diagnostics, and cover valid, invalid, and conflicting CLI inputs in tests.
- [Risk] Preset identifiers can drift between GUI and CLI if each path maintains a separate list. -> [Mitigation] Read presets directly from the injected shared engine.
- [Risk] Expression execution adds Lua runtime work to CLI exports that opt in. -> [Mitigation] Keep the existing per-evaluation timeout and run the expression only when explicitly requested.

## Migration Plan

No data migration is required. Existing invocations remain valid. Update CLI help and tests, then deploy the application with the shared expression engine enabled for CLI exports.
