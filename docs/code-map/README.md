# ChapterTool Code Map

This directory is the maintainer navigation index for the current codebase.

Use it to locate the code behind a feature before you search the full repository.

Do not search `docs/archive/` as part of normal feature work. That directory contains historical records only.

ChapterTool is a cross-platform chapter editor for desktop, command-line, and browser use. The code map covers the Core library, platform services, command-line host, shared Avalonia UI, desktop and browser hosts, Node.js package, and test projects.

## Writing Standard

Use ASD-STE100 principles in every code-map document. Write short, direct sentences. Use one idea per sentence. Use active voice and a specific subject. Use the same term for the same concept. Define abbreviations before use, except for code-defined names. Keep paths, commands, and identifiers exact. Keep Chinese feature-matrix text concise when a Chinese label is required.

## Documents

- `core.md`
  - domain models, import/edit/transform/export logic
- `contracts.md`
  - host-neutral settings models and platform contracts
- `infrastructure.md`
  - external tools, process execution, settings persistence, platform services
- `avalonia.md`
  - shared UI, desktop shell, and runtime service wiring
- `program-form-capability-map.md`
  - all program forms, shared layers, capability comparison, known boundaries, and change tracking
- `ui-logging.md`
  - UI logging scenarios, triggers, recorded content, and log sinks
- `testing.md`
  - which test project and test files verify each code area

## Structure

Use the module maps to find ownership, entry points, runtime wiring, and primary tests.
Use `program-form-capability-map.md` to compare user-facing capabilities across hosts.

## Contracts

Repository-wide agent constraints live in the root `AGENTS.md`.
Host-neutral API boundaries live in `contracts.md`.
Behavioral requirements live in `openspec/specs/` and in the relevant code and tests.
Use the module maps to find the contract that applies to a code area.

Do not copy these rules into a second checklist. Update the source that owns each rule.

## State

OpenSpec owns feature proposal, implementation, and completion state.
Use `openspec list --json` to discover changes.
Use `openspec status --change "<change-name>" --json` to inspect artifact state.
Use `openspec validate "<change-name>" --strict` before treating a change as ready for implementation.
Git owns the current branch and working-tree state.

Do not record a dated snapshot of active tasks in this map. Read OpenSpec and Git when you need current state.

If OpenSpec lists a change without valid artifacts, report it as an incomplete planning record. Do not infer implementation work from its name.

## WebAssembly Hosts

- `src/ChapterTool.Wasm`
  - Blazor WebAssembly browser app for `ChapterTool.Core`
- `src/ChapterTool.Node`
  - pure .NET WebAssembly host for Node.js
- `packages/chaptertool`
  - JavaScript source, build scripts, type declarations, and generated npm distribution

## Shared Avalonia UI

- `src/ChapterTool.Avalonia.UI`
  - shared Avalonia views, ViewModels, workflows, resources, and semantic platform ports
- `src/ChapterTool.Avalonia`
  - desktop Avalonia host and desktop adapter composition

## Command-Line Host

- `src/ChapterTool.CommandLine`
  - standalone process entry point, DotMake.CommandLine commands, binding, console workflows, and `ChapterTool` NuGet Tool package
- `.github/workflows/nuget-publish.yml`
  - version-tag package build and publication

## Use This Map

1. Start with the feature that you need to change or debug.
2. Open the document for the module that owns the behavior.
3. Follow the listed entry points before you search the full repository.
4. Use `testing.md` to select the verification path.
5. Read the owning contract and current OpenSpec state before changing behavior.

## Maintenance Rule

Update these documents in the same change when feature work changes:

- module ownership
- key entry points
- runtime wiring between modules
- the primary files a maintainer should inspect first
- the primary tests used to verify that area

Keep this index as a router. Keep implementation detail in the module maps, durable rules in `AGENTS.md` or their owning contracts, and changing task state in OpenSpec and Git.
