# ChapterTool Code Map

This directory is the maintainer navigation index for the current codebase.

Use it to locate the code behind a feature before you search the full repository.

ChapterTool is a cross-platform chapter editor for desktop, command-line, and browser use. The code map covers the Core library, platform services, command-line host, Avalonia desktop app, browser hosts, Node.js package, and test projects.

## Writing Standard

Use ASD-STE100 principles in every code-map document. Write short, direct sentences. Use one idea per sentence. Use active voice and a specific subject. Use the same term for the same concept. Define abbreviations before use, except for code-defined names. Keep paths, commands, and identifiers exact. Keep Chinese feature-matrix text concise when a Chinese label is required.

## Documents

- `core.md`
  - domain models, import/edit/transform/export logic
- `infrastructure.md`
  - external tools, process execution, settings persistence, platform services
- `avalonia.md`
  - desktop shell, CLI entry points, view/viewmodel/runtime service wiring
- `program-form-capability-map.md`
  - all program forms, shared layers, capability comparison, known boundaries, and change tracking
- `testing.md`
  - which test project and test files verify each code area

## WebAssembly Hosts

- `src/ChapterTool.Wasm`
  - Blazor WebAssembly browser host for `ChapterTool.Core`
- `src/ChapterTool.Node`
  - Pure .NET WebAssembly host for Node.js
- `packages/chaptertool`
  - JavaScript source, build scripts, type declarations, and generated npm distribution

## Command-Line Host

- `src/ChapterTool.CommandLine`
  - DotMake.CommandLine commands, binding, console workflows, and shared host facade
- `src/ChapterTool.Cli`
  - standalone process entry point and `ChapterTool` NuGet Tool package
- `.github/workflows/nuget-publish.yml`
  - version-tag package build and publication

## Use This Map

1. Start with the feature that you need to change or debug.
2. Open the document for the module that owns the behavior.
3. Follow the listed entry points before you search the full repository.
4. Use `testing.md` to select the verification path.

## Maintenance Rule

Update these documents in the same change when feature work changes:

- module ownership
- key entry points
- runtime wiring between modules
- the primary files a maintainer should inspect first
- the primary tests used to verify that area
