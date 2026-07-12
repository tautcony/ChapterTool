# ChapterTool Code Map

This directory is the maintainer navigation index for the current codebase.

Use it when you need to quickly locate the code behind a feature without repository-wide searching first.

## Documents

- `core.md`
  - domain models, import/edit/transform/export logic
- `infrastructure.md`
  - external tools, process execution, settings persistence, platform services
- `avalonia.md`
  - desktop shell, CLI entrypoints, view/viewmodel/runtime service wiring
- `testing.md`
  - which test project and test files verify each code area

## Samples

- `samples/ChapterTool.Core.WasmDemo`
  - Blazor WebAssembly standalone host for `ChapterTool.Core` (client-side import/export UI)

## How To Use

1. Start from the feature you need to change or debug.
2. Open the module document that owns the behavior.
3. Follow the listed entry points before using repository-wide search.
4. Use `testing.md` to find the fastest verification path.

## Maintenance Rule

Update these documents in the same change when feature work alters:

- module ownership
- key entry points
- runtime wiring between modules
- the primary files a maintainer should inspect first
- the primary tests used to verify that area
