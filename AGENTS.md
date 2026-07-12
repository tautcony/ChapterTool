# AGENTS.md

## Repository Overview

- This repo is the current .NET 10 ChapterTool codebase. Use `ChapterTool.Avalonia.slnx` as the main solution.
- Main projects:
  - `src/ChapterTool.Core` (pure managed; browser WASM-capable via stream/text import APIs)
  - `src/ChapterTool.Infrastructure`
  - `src/ChapterTool.Avalonia`
  - `tests/ChapterTool.Core.Tests`
  - `tests/ChapterTool.Infrastructure.Tests`
  - `tests/ChapterTool.Avalonia.Tests` (ViewModel/CLI/service unit tests)
  - `tests/ChapterTool.Avalonia.Headless.Tests` (Avalonia Headless UI tests; separate process from unit tests)
  - `samples/ChapterTool.Core.WasmDemo` (Blazor WebAssembly standalone demo for Core)
- Prefer `rg` for searching files and text.
- Use `docs/code-map/` as the primary navigation index for the current codebase. Update the relevant files there when feature work changes module ownership, entry points, runtime wiring, or the main tests a maintainer should inspect.
- Keep user-facing Chinese strings as valid UTF-8. Validate localization through behavior, rendered UI, or resource-level checks rather than hard-coding incidental mojibake examples.
- CLI argument entry points must be defined, parsed, and bound through `DotMake.CommandLine`; do not hand-write logic in `Program.cs` or CLI support code to iterate, recognize, or dispatch raw `args`.
- Keep this file focused on durable repository guidance. Do not add one-off implementation notes, completed change records, or transient archive paths here.

## PowerShell Guidance

- On Windows, prefer `pwsh.exe` over `powershell.exe` unless Windows PowerShell 5.1 is explicitly required.
- For short native-command invocations, pass the executable and arguments separately. Store the executable path in a variable, keep each native argument as one array item, invoke with `&`, and capture `$LASTEXITCODE` immediately.
- For cmdlets and file operations, prefer PowerShell-native commands with splatting and `-LiteralPath` when working with real paths.
- For file operations, prefer explicit path and encoding handling. Use `-LiteralPath` for real paths, specify UTF-8 when reading or writing text, and avoid relying on implicit wildcard expansion or default encodings.
  - `Get-Content -Raw -Encoding utf8 -LiteralPath $path`
  - `[System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)`
- For multiline scripts, complex quoting, JSON, XML, regular expressions, or non-ASCII paths, write a temporary `.ps1` file and run it with `pwsh.exe -NoLogo -NoProfile -NonInteractive -File script.ps1`.
- Do not use `Invoke-Expression` for normal task execution.

## OpenSpec Workflow

- OpenSpec specs are under `openspec/specs/`.
- Archived changes are under `openspec/changes/archive/`.
- Active changes are discovered with OpenSpec commands; do not assume a specific change name from prior work.
- Before implementing spec-driven work, inspect the active change:
  - `openspec list --json`
  - `openspec status --change "<change-name>" --json`
  - `openspec validate "<change-name>" --strict`
- Before archiving a change with delta specs, sync every delta into the corresponding main spec under `openspec/specs/`; do not archive with spec sync skipped.
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
- If dependencies, target frameworks, or generated project assets change, restore/build once before running no-restore test commands.
- The CI workflow is in `.github/workflows/dotnet-ci.yml`.
- If a test/build fails because `ChapterTool.Avalonia.exe` is locked, close the running app or run:
  - `Get-Process ChapterTool.Avalonia -ErrorAction SilentlyContinue | Stop-Process`
- Do not run multiple `dotnet test` commands for projects in this solution in parallel. The test projects share referenced project `obj/` outputs, and parallel external test processes can fail with locked files such as `src/ChapterTool.Core/obj/Debug/net10.0/ChapterTool.Core.dll`. Prefer the full solution test command above, or run individual test projects sequentially.
- Keep Avalonia Headless UI tests in `tests/ChapterTool.Avalonia.Headless.Tests` so they run in a separate process from non-UI Avalonia unit tests. Do not put `[AvaloniaFact]` or `[AvaloniaTheory]` back into `ChapterTool.Avalonia.Tests`. The unit project guard `NoAvaloniaHeadlessAttributeGuardTests` fails if those attributes reappear there.
- Within the Headless project, put every class containing `[AvaloniaFact]` or `[AvaloniaTheory]` in `AvaloniaHeadlessTestCollection` (serial within the process). Do not reintroduce assembly-level `CollectionBehavior(DisableTestParallelization = true)` for the non-Headless Avalonia unit-test project. The guard test `HeadlessTestCollectionGuardTests` exists to catch missed Headless classes.
- **Process isolation is required, not optional.** Avalonia Headless runs a process-wide UI session (`HeadlessUnitTestSession` + dispatcher/`PushFrame`). xUnit `CollectionDefinition(DisableParallelization = true)` only serializes tests *inside* that collection; it does **not** stop other collections/classes in the same assembly from running in parallel. Putting `[AvaloniaFact]` next to ordinary `[Fact]` tests in one testhost has caused hard hangs (main thread and thread-pool stuck in `Monitor.Wait`) after unit tests finish while Headless never completes. Merging the projects again, or “isolating” only with a collection in a mixed assembly, will reintroduce that failure mode.
- **Do not “fix” unexplained hangs by deleting Headless tests.** First split runs: unit project alone, Headless project alone, then full solution. If unit-only and Headless-only pass but a mixed same-process run hangs, treat it as process/UI-session isolation, not as a bad assertion in a single test file.
- After a hung or force-killed test run, stop leftover apphosts before retrying (`ChapterTool.Avalonia.Headless.Tests`, and any stale `ChapterTool.Avalonia.Tests` testhost). Stray Headless/Skia processes make subsequent Headless runs more likely to stall.
- Inside `[AvaloniaFact]`/`[AvaloniaTheory]`, avoid redundant `Dispatcher.UIThread.Invoke` (the runner already dispatches onto the UI thread). Prefer `RunJobs` and deterministic UI state over long `Task.Delay` waits when pumping the headless dispatcher.
- Keep Avalonia Headless tests focused on UI behavior and workflow outcomes. Prefer tests that drive user actions or state changes and then verify the resulting UI state, command routing, localization refresh, selection changes, or persisted behavior.
- Do not add Headless tests that only assert a control exists, a static label renders, a window opens, a screenshot file was written, or a layout has non-zero size unless that assertion is part of a broader user-facing behavior change being verified.
- When a test constructs `SettingsToolViewModel` and then calls `LoadAsync` explicitly, pass `autoLoad: false`. Otherwise the constructor starts a background load and the test performs the same initialization twice, which slows Headless runs and can introduce races.
- Do not test source/configuration files by reading them as text and asserting strings. This includes `.cs`, `.axaml`, `.csproj`, scripts, CI YAML, README, and docs. Prefer compiled coverage, behavior tests, runtime verification, structured public APIs, or integration checks.
- Add or update tests for changed behavior, especially UI layout constraints, UTF-8 labels, import/export behavior, and platform-service boundaries.

## Avalonia UI Guidelines

- Use responsive Avalonia layout panels and stable sizing constraints. Do not rely on absolute positioning for normal workflow controls.
- The Avalonia main window should preserve these workflow zones:
  - top load/save and frame controls
  - central chapter grid
  - bottom options area
  - status/progress strip
- Avoid `Canvas`, `Canvas.Left`, and `Canvas.Top` for normal workflow controls.
- Bottom options must remain responsive when the window is resized. Use star-sized Grid columns and inner label/control grids where alignment matters.
- Keep numeric controls wide enough that values are not covered by spinner buttons.
- Keep DataGrid columns protected with sensible `MinWidth` values so headers and content do not overlap when resized.
- Buttons should center content horizontally and vertically.
- Do not expose Windows registry-dependent actions, such as file association, as always-visible primary UI.
- When verifying visual layout changes manually, capture screenshots at default, wide, and narrow sizes and store them under `artifacts/`. Do not treat screenshot generation by itself as an automated test assertion.
- Preserve accessible names, keyboard navigation, focus behavior, and localization boundaries when changing controls.

## Change And PR Expectations

- Keep changes scoped to the current feature or fix.
- Mention the primary test commands run in the PR or final summary.
- For UI changes, include screenshot artifact paths when available.
- When a feature change affects code ownership or lookup paths, update the relevant files under `docs/code-map/` in the same change.
- Do not revert unrelated user or generated changes in a dirty worktree.
