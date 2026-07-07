## 1. Core Lua expression support

- [x] 1.1 Add `LuaCSharp` dependency and Lua expression script option models while keeping `ApplyExpression` workflow compatibility.
- [x] 1.2 Add Core Lua script preset metadata for identity, offset seconds, frame rounding, and half-frame earlier adjustment.
- [x] 1.3 Implement a Lua expression script service that evaluates shorthand arithmetic expressions, direct `return` scripts, and `transform(chapter)` scripts with `t`, `fps`, `index`, `count`, and `chapter` context.
- [x] 1.4 Replace user-facing custom formula/postfix transform integration in projection/export options with Lua expression/script evaluation while preserving only `ApplyExpression`, `t`/`fps`, and shorthand compatibility seams.
- [x] 1.5 Formalize the shared preview/save projection pipeline: source snapshot, Lua transform, normalization/frame refresh, separator preservation, order shift, naming, projected `Info`, `OutputChapters`, and diagnostics.
- [x] 1.6 Add Core tests for Lua shorthand arithmetic (`t+1` without `return`), direct returns, transform functions, presets, invalid returns, compile/runtime failures, normalization, and no-postfix requirement.
- [x] 1.7 Add Core projection tests proving preview/save projection order, non-mutation, separator handling, diagnostics, order shift, and naming consistency.

## 2. Lua authoring services

- [x] 2.1 Replace expression authoring metadata with Lua globals, safe helpers, Lua keywords, snippets, and preset metadata.
- [x] 2.2 Replace expression analysis/classification with Lua-oriented token spans, diagnostics, completion replacement ranges, and shorthand-expression validation.
- [x] 2.3 Add tests for Lua authoring metadata, Lua highlighting spans, Lua completion, and Lua diagnostic suggestions.

## 3. Avalonia Lua expression UI and services

- [x] 3.1 Extend main ViewModel state and current export options with Lua script text, selected Lua preset, external script display path, and one method for building shared projection/export options.
- [x] 3.2 Extend `ExpressionToolViewModel` with preset application, external `.lua` picker/read command, and apply-to-owner behavior.
- [x] 3.3 Update expression tool and main expression UI to use Lua script editor behavior and responsive layout without formula/postfix mode selection.
- [x] 3.4 Add localized user-facing strings for Lua expression presets, external script selection, script load feedback, and Lua diagnostics.
- [x] 3.5 Add Avalonia ViewModel/headless tests for Lua editor rendering, preset selection, external script loading via picker abstraction, simple `t+1` shorthand preview/save behavior, and preview/save projection consistency.

## 4. Validation

- [x] 4.1 Run `openspec validate add-lua-expression-scripts --strict`.
- [x] 4.2 Run focused Core and Avalonia tests affected by expression changes.
- [x] 4.3 Run `dotnet test ChapterTool.Avalonia.slnx --no-restore` after restore/build updates.
