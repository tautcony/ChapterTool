## Context

ChapterTool currently transforms chapter timestamps through `ExpressionService`, a synchronous custom evaluator for one-line infix and postfix expressions. The Avalonia shell binds one expression string and one apply flag into export options; preview/save projection uses `ChapterExpressionService` through `ChapterOutputProjectionService`. A later expression editor added metadata, token classification, completion, and diagnostics for that custom grammar.

The revised direction is to replace that expression language as the user-facing feature with Lua, using Lua-CSharp (`LuaCSharp`, from `nuskey8/Lua-CSharp`). The expression surface should become a Lua script surface with presets, external scripts, and Lua-aware editor services rather than preserving the old formula/postfix grammar as a parallel mode.

## Goals / Non-Goals

**Goals:**

- Make Lua the expression language for chapter-time transforms.
- Remove postfix expression authoring/evaluation as a product requirement for the new expression feature.
- Support formula-like Lua expression shorthand (`t + 1`) without requiring users to type `return`, while also supporting explicit `return ...` chunks and reusable `transform(chapter)` functions.
- Add Core-level Lua script presets as structured metadata so UI and tests do not duplicate script text.
- Replace expression authoring metadata/analysis with Lua-oriented symbols, diagnostics, highlighting spans, and completion/LSP-style candidates.
- Allow Avalonia users to load external `.lua` script text through platform picker abstractions.
- Keep preview/save/export projection deterministic: invalid scripts preserve original chapter times and emit warnings.

**Non-Goals:**

- Preserve old infix/postfix grammar semantics as a first-class expression mode.
- Add arbitrary file-system access from Core during export; UI may read selected external script text and pass it into Core.
- Provide a full Lua debugger, package manager, or persistent user script library.
- Expose Lua standard-library OS/file/network capabilities to scripts.

## Decisions

1. Replace expression options with Lua script options and keep only intentional compatibility seams.

   `ChapterExportOptions` will retain `ApplyExpression` for workflow compatibility, and the expression payload becomes Lua expression/script text plus optional preset/source metadata. The only compatibility seams intentionally preserved are: the `ApplyExpression` enable/disable workflow, the `t`/`fps` variable names, and expression shorthand without `return`. The implementation and tests should not preserve postfix expressions, the old custom parser as a parallel mode, or broad old-grammar equivalence.

2. Keep Core script execution source-text based.

   External file selection is a UI/platform concern. The Avalonia tool reads the chosen `.lua` file, stores the script text and display path in the ViewModel, then passes script text through export options. This avoids Core depending on paths, current directories, or UI permissions, and it makes tests deterministic.

3. Define a small Lua contract with expression shorthand, direct `return`, and `transform(context)`.

   For each non-separator chapter, Core sets Lua globals for `t`, `fps`, `index`, `count`, and a `chapter` table. If the input does not contain an explicit Lua `return` or `transform` declaration, Core treats it as a single Lua expression and evaluates `return (<input>)`; this preserves low-friction usage for `t + 1`, `t - 0.5`, `t * fps / fps`, and similar arithmetic. A script may also explicitly `return <number>` or define `function transform(chapter) ... end`. The numeric result is seconds. Returning nil, non-numeric values, NaN, or infinity is a structured failure.

4. Use a restricted Lua-CSharp state per evaluation.

   Core creates a fresh `LuaState` for deterministic, isolated evaluation and exposes only safe helpers needed for transforms, especially math helpers. It must not expose OS/file/process IO. Built-in scripts are small enough that per-chapter execution is acceptable; if performance becomes an issue later, cached script plans can be added behind the same service interface.

5. Model presets as data, not UI literals.

   Core exposes `LuaExpressionScriptPreset` values with stable ids, localization keys/display names, descriptions, and script text. Avalonia binds these presets and copies the selected preset script into the editor/script field. Initial presets: identity (`t` or `return t`), offset seconds, round to nearest frame, and half-frame earlier adjustment.

6. Formalize a shared preview/save projection pipeline.

   Preview, save, and text preview must not each reimplement expression application. They should call one Core projection service that accepts an immutable `ChapterInfo` snapshot and projection options, then returns projected chapters plus diagnostics. The pipeline order is:

   1. Start from the current source `ChapterInfo` snapshot; do not mutate it.
   2. If `ApplyExpression` is false, keep chapter times unchanged. If true, evaluate the Lua expression/script for each non-separator chapter.
   3. Normalize successful expression results to valid time bounds and refresh derived frame display/accuracy from the effective frame rate.
   4. Preserve separator rows as structural separators; do not run Lua on separators.
   5. Apply output-only chapter numbering/order shift.
   6. Apply output-only name generation/template substitution.
   7. Return both `Info` (for projected row preview) and `OutputChapters` (non-separator export rows) with accumulated diagnostics.

   The save service then serializes/export-formats only the projected output; UI preview uses the same projected result. If Lua diagnostics occur, the affected chapter keeps its original time and downstream numbering/naming still proceeds on the projected list.

7. Replace expression authoring with Lua authoring.

   The existing expression editor control should evolve from custom grammar token metadata to Lua tokenization/classification, Lua diagnostics, and completion/LSP-style candidates for provided globals (`t`, `fps`, `index`, `count`, `chapter`), safe math helpers, and preset snippets. If a full LSP server is not embedded in the first pass, the service boundary should still be Lua-oriented and testable so it can be backed by an LSP implementation later without changing ViewModels.

8. Extend diagnostics rather than throwing.

   Lua compile/runtime/return-shape failures produce `InvalidExpression.Lua*` warning diagnostics. Projection continues to normalize negative/too-large successful results with existing `InvalidExpressionTime` diagnostics, and all diagnostics travel with the projection result so preview and save report the same issues.

## Risks / Trade-offs

- [Risk] Removing custom formula/postfix behavior may break users who rely on non-Lua-only syntax such as `M_PI` constants or postfix token lists. → Mitigation: preserve only the agreed low-friction seams (`ApplyExpression`, `t`/`fps`, shorthand like `t + 1`), provide Lua presets/snippets, and avoid rebuilding the old grammar by accident.
- [Risk] Lua-CSharp APIs are asynchronous while export/projection code is synchronous. → Mitigation: isolate blocking inside the Lua expression service with `GetAwaiter().GetResult()` after keeping scripts local and CPU-bound; revisit async projection only if needed.
- [Risk] Opening all Lua standard libraries may expose file or OS operations. → Mitigation: use a restricted standard-library set or explicit safe helper table; tests SHALL verify scripts cannot rely on external file IO through the expression service contract.
- [Risk] Running a script per chapter is slower than the formula evaluator. → Mitigation: scripts are expected to be short; add caching only if measured, and keep preview/save on the same projection path so performance fixes benefit both.
- [Risk] Full Lua LSP integration may be larger than the transform work. → Mitigation: design the Core/UI authoring boundary around Lua completions/diagnostics first and keep any LSP-backed implementation swappable.

## Migration Plan

1. Add Core Lua models/options and the LuaCSharp dependency.
2. Implement `LuaExpressionScriptService` with expression shorthand wrapping and integrate it into a shared output projection service as the only user-facing expression evaluator.
3. Add Lua presets and Core tests for script success/failure behavior.
4. Replace expression authoring metadata/analyzer behavior with Lua-oriented classification, completion, and diagnostics.
5. Extend Avalonia ViewModels/services/resources and editor UI for Lua scripts, presets, file picking, and Lua completion/highlighting.
6. Update preview/save projection tests so one projected result path covers UI preview, text preview, save options, diagnostics, order shift, and naming.
7. Run focused/full solution validation.

Rollback is possible by reverting to the previous custom expression evaluator before archiving this change; once archived, docs and tests should describe Lua as the expression language.
