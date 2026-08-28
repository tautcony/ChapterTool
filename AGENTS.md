# AGENTS.md

## Repository Overview

- This repository contains the current .NET 10 ChapterTool codebase.
- Use `ChapterTool.slnx` as the main solution.
- Main projects:
  - `src/ChapterTool.Core` (pure managed and browser WebAssembly-capable through stream and text import APIs)
  - `src/ChapterTool.Wasm` (Blazor WebAssembly browser application for Core)
  - `src/ChapterTool.Node` (Node.js WebAssembly host)
  - `src/ChapterTool.CommandLine` (DotMake.CommandLine host and NuGet tool)
  - `src/ChapterTool.Infrastructure`
  - `src/ChapterTool.Avalonia.UI` (shared Avalonia views and ViewModels)
  - `src/ChapterTool.Avalonia` (desktop Avalonia host)
  - `tests/ChapterTool.Core.Tests`
  - `tests/ChapterTool.Infrastructure.Tests`
  - `tests/ChapterTool.Avalonia.Tests` (ViewModel/CLI/service unit tests)
  - `tests/ChapterTool.Avalonia.Headless.Tests` (Avalonia Headless UI tests in a separate process)
  - `tests/ChapterTool.TestSupport` (shared repository root, fixture paths, and test logger)
- Prefer `rg` for searching files and text.
- Use `docs/code-map/` as the primary navigation index for the current codebase.
- Read current documentation first: `docs/README.md`, `docs/code-map/`, and applicable testing guidance. Treat `docs/archive/` as historical reference unless the task requires it.
- Update the applicable code-map files when feature work changes module ownership, entry points, runtime wiring, or primary tests.
- For WinForms-to-Avalonia work, start with `.agents/skills/README-winforms-to-avalonia.md` and the `winforms-to-avalonia` orchestrator skill. The method has phases A through G.
- Use `reusable-learnings.md` for general rules and `references/execution-corrections.md` for correction patterns.
- Store user-facing Chinese strings as valid UTF-8.
- Validate localization through behavior, rendered UI, or resource-level checks. Do not hard-code incidental mojibake examples.
- Treat `src/ChapterTool.Avalonia.UI/Localization/Resources/Locales/*.axaml` as the shared translation source. After changing a locale, run `python3 scripts/axaml-to-json.py`, then `python3 scripts/axaml-to-json.py --check`. Do not edit generated Wasm JSON files by hand. CLI JSON resources are separate.
- Define, parse, and bind command-line interface (CLI) arguments through `DotMake.CommandLine`.
- Do not write code in `Program.cs` or CLI support files that recognizes or dispatches raw `args`.
- Keep this file focused on durable repository guidance. Do not add one-off implementation notes, completed change records, or transient archive paths here.

## AGENTS.md Maintenance

- This file contains project rules. Keep personal preferences in the user's global `AGENTS.md` or `CLAUDE.md`.
- Keep this file below 2,400 tokens. Measure actual tokens after each update.
- Use transcripts and repository evidence. Require two independent sessions for a new non-safety rule; one confirmed safety, data-loss, or compatibility incident is sufficient.
- Make at most five edits per pass. Each edit needs a verbatim quote or repository path.
- Prefer rewriting or deleting rules. Extract narrow triggered guidance into a skill. Delete narrow guidance without a reliable trigger.
- Review for duplication, stale history, contradictions, and budget impact. Check the budget and run the smallest relevant verification after writing.

## Documentation Language (ASD-STE100)

- Write new or modified documentation in short, direct, active sentences. Use one fact or instruction per sentence and one term per concept.
- Use `must` for requirements, `may` for permission, and `can` for capability. Keep identifiers, paths, commands, product names, and required Chinese or Japanese text unchanged.
- Apply these rules to `docs/code-map/` and review changed documentation before finishing.

## PowerShell Guidance

- On Windows, prefer `pwsh.exe` over `powershell.exe` unless Windows PowerShell 5.1 is explicitly required.
- Pass native PowerShell commands as an executable plus argument array. Store the executable path in a variable, invoke with `&`, and capture `$LASTEXITCODE` immediately.
- Use cmdlets with splatting and `-LiteralPath`. Specify UTF-8 for text I/O.
  - `Get-Content -Raw -Encoding utf8 -LiteralPath $path`
  - `[System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)`
- Write a temporary `.ps1` file for multiline scripts, complex quoting, JSON, XML, regular expressions, or non-ASCII paths.
- Run the temporary file with `pwsh.exe -NoLogo -NoProfile -NonInteractive -File script.ps1`.
- Do not use `Invoke-Expression` for normal task execution.

## OpenSpec Workflow

- OpenSpec specs are under `openspec/specs/`; archived changes are under `openspec/changes/archive/`.
- Discover active changes. Before implementation, inspect and validate the selected change:
  - `openspec list --json`
  - `openspec status --change "<change-name>" --json`
  - `openspec validate "<change-name>" --strict`
- Before archiving, sync each delta spec into `openspec/specs/`. After archiving, validate all specs:
  - `openspec validate --all`

## Testing And Build

- Run focused Avalonia unit tests after ViewModel/CLI/service changes:
  - `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore`
- Run focused Avalonia Headless tests after XAML or UI shell changes:
  - `dotnet test tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj --no-restore`
- Run the full solution tests before finalizing broader changes:
  - `dotnet test ChapterTool.slnx --no-restore`
- Build the Avalonia app when changing app project files:
  - `dotnet build src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj --no-restore`
- If dependencies, target frameworks, or generated project assets change, run restore or build once.
- Then run commands that use `--no-restore`.
- The CI workflow is in `.github/workflows/dotnet-ci.yml`.
- If `ChapterTool.Avalonia.exe` is locked, close it or run `Get-Process ChapterTool.Avalonia -ErrorAction SilentlyContinue | Stop-Process`.
- Run solution test projects sequentially. Shared `obj/` outputs can cause file locks when test processes run in parallel.
- Keep Avalonia Headless UI tests in `tests/ChapterTool.Avalonia.Headless.Tests`. Run this project in a separate process from non-UI Avalonia tests.
- Keep `[AvaloniaFact]` and `[AvaloniaTheory]` in the Headless project. Put their classes in `AvaloniaHeadlessTestCollection`.
- Do not add assembly-wide `CollectionBehavior(DisableParallelization = true)` to the non-Headless project.
- Avalonia Headless uses a process-wide UI session. Do not merge Headless and non-Headless tests or use a collection as a substitute for process isolation.
- Run the Avalonia unit project alone, then the Headless project alone, then the full solution. If a mixed run hangs, investigate testhost and UI-session isolation instead of deleting tests.
- After a hung or terminated run, stop leftover `ChapterTool.Avalonia.Headless.Tests` and `ChapterTool.Avalonia.Tests` testhosts before retrying.
- In Headless tests, rely on the runner's UI thread, use `RunJobs` and deterministic state, and avoid fixed delays or static-control-only assertions.
- Drive user actions and verify workflow outcomes. Use `autoLoad: false` before explicit `SettingsToolViewModel.LoadAsync`.
- Do not test source or configuration by reading files as text. Use compiled coverage, behavior tests, runtime checks, public APIs, or integration checks.
- Update tests for changed behavior, especially layout, UTF-8, import/export, and platform boundaries.
- Use `docs/code-map/testing.md` for test ownership, Headless lifecycle rules, and distribution verification details.

## Avalonia UI Guidelines

- Use responsive Avalonia layout panels and stable sizing constraints. Do not rely on absolute positioning for normal workflow controls.
- The Avalonia main window must preserve these workflow zones:
  - top load/save and frame controls
  - central chapter grid
  - bottom options area
  - status/progress strip
- Avoid `Canvas`, `Canvas.Left`, and `Canvas.Top` for normal workflow controls.
- Bottom options must remain responsive when the window is resized. Use star-sized Grid columns and inner label/control grids where alignment matters.
- Keep numeric controls wide enough that values are not covered by spinner buttons.
- Keep DataGrid columns protected with sensible `MinWidth` values so headers and content do not overlap when resized.
- Buttons must center content horizontally and vertically.
- Do not expose Windows registry-dependent actions, such as file association, as always-visible primary UI.
- When verifying visual layout changes manually, capture screenshots at default, wide, and narrow sizes and store them under `artifacts/`. Do not treat screenshot generation by itself as an automated test assertion.
- Preserve accessible names, keyboard navigation, focus behavior, and localization boundaries when changing controls.

## Change And PR Expectations

- Keep changes scoped to the current feature or fix.
- Mention the primary test commands run in the PR or final summary.
- For UI changes, include screenshot artifact paths when available.
- When a feature change affects code ownership or lookup paths, update the relevant files under `docs/code-map/` in the same change.
- Do not revert unrelated user or generated changes in a dirty worktree.
