# AGENTS.md

## Repository Overview

- This repository contains the current .NET 10 ChapterTool codebase.
- Use `ChapterTool.slnx` as the main solution.
- Use `docs/code-map/` when you need to locate ownership, entry points, or tests. Read only pages relevant to the task. Small edits do not require a full documentation review.
- `docs/README.md` indexes current documentation. Treat `docs/archive/` as historical reference.
- For an explicit WinForms-to-Avalonia migration, use `.agents/skills/README-winforms-to-avalonia.md` and the `winforms-to-avalonia` orchestrator skill.
- Store user-facing Chinese strings as valid UTF-8.
- Validate localization through behavior, rendered UI, or resource-level checks. Do not hard-code incidental mojibake examples.
- Treat `src/ChapterTool.Avalonia.UI/Localization/Resources/Locales/*.axaml` as the shared translation source. After changing a locale, run `uv run --project scripts scripts/axaml-to-json.py`, then `uv run --project scripts scripts/axaml-to-json.py --check`. Run `uv sync --project scripts` once when the scripts environment is not installed. Do not edit generated Wasm JSON files by hand. CLI JSON resources are separate.
- Define, parse, and bind command-line interface (CLI) arguments through `DotMake.CommandLine`.
- Do not write code in `Program.cs` or CLI support files that recognizes or dispatches raw `args`.
- Keep this file focused on durable repository guidance. Do not add one-off implementation notes, completed change records, or transient archive paths here.

## AGENTS.md Maintenance

- Keep durable project constraints here. Keep personal preferences in global instructions. Omit completed work records and generic agent advice.
- Ground rules in repository evidence or confirmed incidents. Prefer removing or merging rules over adding procedures.
- Put specialized workflows in skills or task-specific documentation with clear triggers. Keep skill descriptions short and precise. Use a small router for skills with multiple workflows.
- Review for stale references, duplication, conflicting boundaries, and unnecessary reading or testing. Keep guidance useful across models.

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
- Apply this workflow when the task uses an OpenSpec change. Routine edits do not require a new change. Before implementing a selected change, discover, inspect, and validate it:
  - `openspec list --json`
  - `openspec status --change "<change-name>" --json`
  - `openspec validate "<change-name>" --strict`
- Before archiving, sync each delta spec into `openspec/specs/`. After archiving, validate all specs:
  - `openspec validate --all`

## Testing And Build

- Select checks from the changed behavior and its consumers. Use `docs/code-map/testing.md` for test ownership, Headless lifecycle rules, and distribution checks. Documentation-only edits normally need no .NET build or test run.
- Run affected tests with `dotnet test <test-project.csproj> --no-restore`. CLI tests are in `tests/ChapterTool.CommandLine.Tests`. Avalonia ViewModel and service tests are in `tests/ChapterTool.Avalonia.Tests`. XAML and UI workflow tests are in `tests/ChapterTool.Avalonia.Headless.Tests`.
- Use `dotnet test ChapterTool.slnx --no-restore` for changes with broad impact across projects. Repeat successful checks only when a new change, failure, or unresolved concern warrants it.
- Restore once when dependencies, target frameworks, or generated project assets change. Then use `--no-restore`. Build `src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj` when its project files change. CI is in `.github/workflows/dotnet-ci.yml`.
- Run test projects sequentially to avoid shared `obj/` file locks. Avalonia Headless uses a process-wide UI session. Keep Headless and non-Headless tests in separate projects and processes. A test collection does not replace process isolation.
- Keep `[AvaloniaFact]` and `[AvaloniaTheory]` in the Headless project, with classes in `AvaloniaHeadlessTestCollection`. Do not disable parallelization assembly-wide in the non-Headless project.
- After a hung run, stop its leftover Avalonia testhosts before retrying. Investigate UI-session isolation rather than deleting tests.
- In Headless tests, use the runner's UI thread, `RunJobs`, and deterministic state. Verify user workflow outcomes. Avoid fixed delays and static-control-only assertions. Use `autoLoad: false` before explicit `SettingsToolViewModel.LoadAsync`.
- Verify behavior through compiled tests, public APIs, runtime checks, or integration checks. Do not test source or configuration by reading files as text.

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

## Completion And Scope

- Complete the requested change, run relevant checks, and fix failures caused by the change before handing back the result. Continue routine local edits and verification without asking for approval at each step.
- Stop when the requested outcome is verified or a concrete blocker needs user input. Report blockers and incomplete checks. Respect an explicit request to stop for review.
- Keep work within the requested scope. Preserve unrelated user and generated changes.
- Update relevant `docs/code-map/` pages when module ownership, entry points, runtime wiring, or primary tests change.
- Report the resulting behavior and primary verification commands. Include screenshot paths for UI changes when available.

<!-- CODEGRAPH_START -->
## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.

If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
<!-- CODEGRAPH_END -->
