## Why

The Avalonia rewrite now has a headless test harness, but the rendered UI coverage is still narrow compared with the main-window and tool-window behavior implemented in ViewModels and XAML. Expanding the headless suite will catch binding, command routing, localization, layout, and interaction regressions that pure ViewModel tests or source-text checks cannot reliably detect.

## What Changes

- Extend Avalonia headless tests from basic load/clip-selection coverage into a comprehensive UI regression suite.
- Add deterministic headless cases for main-window initial state, loading, saving command surfaces, option binding, grid command surfaces, selection/delete/insert menu availability, keyboard shortcuts, context menus, localization refresh, auxiliary tool opening, and secondary tool rendering.
- Add reusable test-host helpers for rendered text lookup, named control lookup, layout at multiple view sizes, command/service fakes, keyboard input, and screenshot/artifact capture where visual evidence is required.
- Keep tests deterministic by using fake services, in-process fixtures, and headless rendering instead of external media tools, desktop sessions, registry state, machine-specific paths, or static source-string assertions.
- No breaking changes.

## Capabilities

### New Capabilities

### Modified Capabilities

- `tests-build-distribution-assets`: Strengthen the Avalonia Headless UI test coverage requirement to define a comprehensive rendered UI test matrix and determinism constraints.

## Impact

- `tests/ChapterTool.Avalonia.Tests/Headless/*`: New and expanded headless UI tests plus shared host helpers.
- `tests/ChapterTool.Avalonia.Tests/*`: Possible small test-fake consolidation where headless tests need service observability already present in ViewModel tests.
- `src/ChapterTool.Avalonia/Views/**/*.axaml` and view code-behind: Only touched if tests reveal missing names, inaccessible command surfaces, layout regressions, or binding defects.
- CI remains `dotnet test ChapterTool.Avalonia.slnx --no-restore`; no new desktop or external tool dependency is introduced.
