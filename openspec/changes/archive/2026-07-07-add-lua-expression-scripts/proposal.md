## Why

Current expression transforms use a custom infix/postfix grammar that is costly to improve and disconnected from standard tooling. Moving the expression feature to Lua gives users a real scripting language for reusable chapter-time transforms, external project scripts, presets, and editor services such as Lua-aware highlighting and completion.

## What Changes

- Replace the custom expression grammar as the user-facing expression language with Lua scripts powered by the `LuaCSharp` package from `nuskey8/Lua-CSharp`.
- Stop treating postfix expressions as a required or supported expression authoring target in the new implementation.
- Define a deterministic Lua expression/script contract: users may type simple arithmetic such as `t + 1` without `return`, or write full Lua `return ...` / `transform(chapter)` scripts.
- Add built-in Lua script presets for common workflows such as identity, offset seconds, NTSC half-frame style adjustment, and frame rounding.
- Allow users to choose an external `.lua` script from the Avalonia expression tool and apply it through a shared preview/save projection pipeline.
- Replace expression editor language services with Lua-aware highlighting, diagnostics, and completion/LSP-style authoring support while preserving the low-friction arithmetic input style users already expect.
- Report Lua compile/runtime/invalid-return failures as structured diagnostics without crashing or mutating the source chapter list.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `chapter-core-transform-export`: expression transforms and expression authoring metadata/analysis use Lua scripts and Lua diagnostics instead of the custom formula/postfix grammar.
- `avalonia-ui-shell`: expression UI exposes Lua presets, external Lua script picking, and Lua-aware editor highlighting/completion/diagnostics.

## Impact

- `src/ChapterTool.Core`: add Lua script expression models/services, replace custom expression transform integration with Lua script evaluation, expose Lua authoring metadata/analysis, and formalize the shared output projection pipeline used by preview/save/export.
- `src/ChapterTool.Avalonia`: update expression editor/tool/main save flow for Lua script text, presets, external script selection, and Lua completion/highlighting.
- `tests/ChapterTool.Core.Tests`: update expression tests for Lua success, presets, external script text, invalid returns, and failure diagnostics; remove postfix-target expectations from the new behavior.
- `tests/ChapterTool.Avalonia.Tests`: update ViewModel/headless tests for Lua editor behavior, presets, file picker interaction, and diagnostics.
- Dependency: add NuGet package `LuaCSharp` to the Core project.
