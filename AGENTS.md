# AGENTS.md

## Repository Overview

- This repository contains the current .NET 10 ChapterTool codebase.
- Use `ChapterTool.Avalonia.slnx` as the main solution.
- Main projects:
  - `src/ChapterTool.Core` (pure managed and browser WebAssembly-capable through stream and text import APIs)
  - `src/ChapterTool.Wasm` (Blazor WebAssembly browser application for Core)
  - `src/ChapterTool.Infrastructure`
  - `src/ChapterTool.Avalonia`
  - `tests/ChapterTool.Core.Tests`
  - `tests/ChapterTool.Infrastructure.Tests`
  - `tests/ChapterTool.Avalonia.Tests` (ViewModel/CLI/service unit tests)
  - `tests/ChapterTool.Avalonia.Headless.Tests` (Avalonia Headless UI tests in a separate process)
- Prefer `rg` for searching files and text.
- Use `docs/code-map/` as the primary navigation index for the current codebase.
- Update the applicable code-map files when feature work changes module ownership, entry points, runtime wiring, or primary tests.
- For product-independent WinForms-to-Avalonia migration methods, start with `.agents/skills/README-winforms-to-avalonia.md` and the `winforms-to-avalonia` orchestrator skill.
- The migration method has phases A through G.
- Use `reusable-learnings.md` for general rules. Use `references/execution-corrections.md` for correction patterns from previous sessions.
- Store user-facing Chinese strings as valid UTF-8.
- Validate localization through behavior, rendered UI, or resource-level checks. Do not hard-code incidental mojibake examples.
- Define, parse, and bind command-line interface (CLI) arguments through `DotMake.CommandLine`.
- Do not write code in `Program.cs` or CLI support files that recognizes or dispatches raw `args`.
- Keep this file focused on durable repository guidance. Do not add one-off implementation notes, completed change records, or transient archive paths here.

## Documentation Language (ASD-STE100)

- Write all new and modified documentation in clear, controlled English that follows ASD-STE100 principles, unless the product requires another language.
- Write repository guidance and code-map text in clear, controlled English that follows ASD-STE100 principles.
- Use one instruction or fact per sentence. Keep sentences short. Prefer the active voice.
- Use a specific subject and a precise verb. Avoid vague verbs such as `handle`, `support`, or `manage` when a more exact verb is available.
- Use the same term for the same concept. Define an abbreviation before you use it, except for names that the codebase defines.
- Use `must` for a required action, `may` for permission, and `can` for capability. Do not use these words as general emphasis.
- Avoid idioms, metaphors, informal language, rhetorical questions, and unnecessary nominalizations.
- Use numbered steps for procedures. Use bullets for independent facts. Keep each bullet to one main idea.
- Keep code identifiers, file paths, commands, product names, and user-facing Chinese or Japanese strings unchanged when they are required by the product.
- Apply the same rules to every file under `docs/code-map/`. When a document uses Chinese for a feature matrix, keep each cell concise and use consistent technical terms.
- Review changed documentation for ASD-STE100 wording before you finish the change.

## PowerShell Guidance

- On Windows, prefer `pwsh.exe` over `powershell.exe` unless Windows PowerShell 5.1 is explicitly required.
- For short native commands, pass the executable and arguments separately.
- Store the executable path in a variable. Store each native argument as one array item.
- Use `&` to run the command. Capture `$LASTEXITCODE` immediately.
- For cmdlets and file operations, use PowerShell-native commands with splatting.
- Use `-LiteralPath` for real paths.
- Specify UTF-8 when you read or write text. Do not depend on implicit wildcard expansion or default encodings.
  - `Get-Content -Raw -Encoding utf8 -LiteralPath $path`
  - `[System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)`
- Write a temporary `.ps1` file for multiline scripts, complex quoting, JSON, XML, regular expressions, or non-ASCII paths.
- Run the temporary file with `pwsh.exe -NoLogo -NoProfile -NonInteractive -File script.ps1`.
- Do not use `Invoke-Expression` for normal task execution.

## OpenSpec Workflow

- OpenSpec specs are under `openspec/specs/`.
- Archived changes are under `openspec/changes/archive/`.
- Use OpenSpec commands to discover active changes. Do not assume a change name from prior work.
- Before implementing spec-driven work, inspect the active change:
  - `openspec list --json`
  - `openspec status --change "<change-name>" --json`
  - `openspec validate "<change-name>" --strict`
- Before you archive a change, sync each delta spec into its main spec under `openspec/specs/`.
- Do not skip spec synchronization.
- After completing and archiving a change, validate all specs:
  - `openspec validate --all`

## Testing And Build

- Run focused Avalonia unit tests after ViewModel/CLI/service changes:
  - `dotnet test tests\ChapterTool.Avalonia.Tests\ChapterTool.Avalonia.Tests.csproj --no-restore`
- Run focused Avalonia Headless tests after XAML or UI shell changes:
  - `dotnet test tests\ChapterTool.Avalonia.Headless.Tests\ChapterTool.Avalonia.Headless.Tests.csproj --no-restore`
- Run the full solution tests before finalizing broader changes:
  - `dotnet test ChapterTool.Avalonia.slnx --no-restore`
- Build the Avalonia app when changing app project files:
  - `dotnet build src\ChapterTool.Avalonia\ChapterTool.Avalonia.csproj --no-restore`
- If dependencies, target frameworks, or generated project assets change, run restore or build once.
- Then run commands that use `--no-restore`.
- The CI workflow is in `.github/workflows/dotnet-ci.yml`.
- If a test/build fails because `ChapterTool.Avalonia.exe` is locked, close the running app or run:
  - `Get-Process ChapterTool.Avalonia -ErrorAction SilentlyContinue | Stop-Process`
- Do not run multiple `dotnet test` commands for this solution in parallel.
- The test projects share referenced-project `obj/` outputs. Parallel test processes can lock files such as `src/ChapterTool.Core/obj/Debug/net10.0/ChapterTool.Core.dll`.
- Run the full solution test command, or run individual test projects in sequence.
- Keep Avalonia Headless UI tests in `tests/ChapterTool.Avalonia.Headless.Tests`. This project runs in a separate process from non-UI unit tests.
- Do not put `[AvaloniaFact]` or `[AvaloniaTheory]` in `ChapterTool.Avalonia.Tests`. `NoAvaloniaHeadlessAttributeGuardTests` detects these attributes.
- In the Headless project, put each class that contains `[AvaloniaFact]` or `[AvaloniaTheory]` in `AvaloniaHeadlessTestCollection`. This collection runs tests in sequence in its process.
- Do not add assembly-level `CollectionBehavior(DisableTestParallelization = true)` to the non-Headless Avalonia unit-test project.
- `HeadlessTestCollectionGuardTests` detects Headless classes that are not in the collection.
- **Process isolation is mandatory.** Avalonia Headless runs a process-wide UI session with `HeadlessUnitTestSession`, the dispatcher, and `PushFrame`.
- `CollectionDefinition(DisableParallelization = true)` only serializes tests in its collection. It does not stop other collections in the same assembly.
- A mixed testhost can hang after unit tests finish. The main thread and thread pool can wait in `Monitor.Wait` while Headless does not complete.
- Do not merge the Headless and non-Headless test projects. A collection alone does not isolate a mixed assembly.
- **Do not delete Headless tests to resolve an unexplained hang.**
- Run the unit project alone. Then run the Headless project alone. Then run the full solution.
- If the separate runs pass but a mixed same-process run hangs, investigate process and UI-session isolation.
- After a hung or terminated test run, stop leftover apphosts before you retry. Stop `ChapterTool.Avalonia.Headless.Tests` and stale `ChapterTool.Avalonia.Tests` testhosts.
- In `[AvaloniaFact]` and `[AvaloniaTheory]`, do not use redundant `Dispatcher.UIThread.Invoke`. The runner already dispatches to the UI thread.
- Use `RunJobs` and deterministic UI state. Avoid long `Task.Delay` waits while you pump the Headless dispatcher.
- Test UI behavior and workflow outcomes in Avalonia Headless tests.
- Drive user actions or state changes. Then verify UI state, command routing, localization refresh, selection changes, or persisted behavior.
- Do not add a Headless test that only verifies the presence of a control, static label, window, screenshot, or non-zero layout size. Such an assertion is permitted only as part of a user-facing behavior test.
- Pass `autoLoad: false` when a test constructs `SettingsToolViewModel` and then calls `LoadAsync`. Otherwise, the constructor starts a second load and can cause a race.
- Do not read source or configuration files as text to test their content. This rule applies to `.cs`, `.axaml`, `.csproj`, scripts, CI YAML, README files, and documentation.
- Use compiled coverage, behavior tests, runtime verification, structured public APIs, or integration checks.
- Add or update tests for changed behavior, especially UI layout constraints, UTF-8 labels, import/export behavior, and platform-service boundaries.

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
