# AGENTS.md

## Dev Environment Tips

- This repo contains the legacy WinForms project under `Time_Shift/` and the current .NET 10 Avalonia/Core/Infrastructure solution under `src/`.
- Use `ChapterTool.Avalonia.slnx` for the current Avalonia/Core/Infrastructure solution.
- Main projects:
  - `src/ChapterTool.Core`
  - `src/ChapterTool.Infrastructure`
  - `src/ChapterTool.Avalonia`
  - `tests/ChapterTool.Core.Tests`
  - `tests/ChapterTool.Infrastructure.Tests`
  - `tests/ChapterTool.Avalonia.Tests`
- Prefer `rg` for searching files and text.
- When reading files with PowerShell, explicitly use UTF-8, for example:
  - `Get-Content -Raw -Encoding utf8 path\to\file`
  - `[System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)`
- Keep this file focused on durable repository guidance. Do not add one-off implementation notes, completed change records, or transient archive paths here.
- Do not port WinForms absolute positioning into Avalonia. Use responsive Avalonia layout panels and stable sizing constraints.
- Keep user-facing Chinese strings as valid UTF-8. Validate localization through behavior, rendered UI, or resource-level checks rather than hard-coding incidental mojibake examples.

## OpenSpec Workflow

- OpenSpec specs are under `openspec/specs/`.
- Archived changes are under `openspec/changes/archive/`.
- Active changes are discovered with OpenSpec commands; do not assume a specific change name from prior work.
- Before implementing spec-driven work, inspect the active change:
  - `openspec list --json`
  - `openspec status --change "<change-name>" --json`
  - `openspec validate "<change-name>" --strict`
- After completing and archiving a change, validate all specs:
  - `openspec validate --all`

## Testing Instructions

- Run the focused Avalonia tests after XAML or UI shell changes:
  - `dotnet test tests\ChapterTool.Avalonia.Tests\ChapterTool.Avalonia.Tests.csproj --no-restore`
- Do not test source/configuration files by reading them as text and asserting strings. This includes `.cs`, `.axaml`, `.csproj`, scripts, CI YAML, README, and docs. Prefer compiled coverage, behavior tests, runtime verification, structured public APIs, or integration checks.
- Run the full solution tests before finalizing broader changes:
  - `dotnet test ChapterTool.Avalonia.slnx --no-restore`
- Do not run multiple `dotnet test` commands for projects in this solution in parallel. The test projects share referenced project `obj/` outputs, and parallel external test processes can fail with locked files such as `src/ChapterTool.Core/obj/Debug/net10.0/ChapterTool.Core.dll`. Prefer the full solution test command above, or run individual test projects sequentially.
- Build the Avalonia app when changing app project files:
  - `dotnet build src\ChapterTool.Avalonia\ChapterTool.Avalonia.csproj --no-restore`
- The CI workflow is in `.github/workflows/dotnet-ci.yml`.
- If a test/build fails because `ChapterTool.Avalonia.exe` is locked, close the running app or run:
  - `Get-Process ChapterTool.Avalonia -ErrorAction SilentlyContinue | Stop-Process`
- Add or update tests for changed behavior, especially UI layout constraints, UTF-8 labels, import/export behavior, and platform-service boundaries.
- If dependencies, target frameworks, or generated project assets change, restore/build once before running no-restore test commands.

## UI Implementation Notes

- The Avalonia main window should preserve workflow zones, not WinForms pixel geometry:
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
- When verifying visual layout changes, capture screenshots at default, wide, and narrow sizes and store them under `artifacts/`.
- Avoid static string assertions over source/configuration layout. Validate UI through Avalonia compilation, behavior-level tests, and screenshots/runtime checks instead.
- Preserve accessible names, keyboard navigation, focus behavior, and localization boundaries when changing controls.

## PR Instructions

- Keep changes scoped to the current feature or fix.
- Mention the primary test commands run in the PR or final summary.
- For UI changes, include screenshot artifact paths when available.
- Do not revert unrelated user or generated changes in a dirty worktree.
