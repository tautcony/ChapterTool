## Why

The test projects currently mix xUnit v2 test framework packages with a v3 Visual Studio runner, which leaves the suite on the older assertion/execution model while targeting .NET 10. The Avalonia UI shell also lacks a true headless runtime harness, so UI layout, binding, localization, and command-routing regressions cannot be exercised through rendered controls in CI.

XML, IFO, and MPLS inputs can expose multiple editions, playlists, or clips. Their edition switching path needs rendered UI coverage because a current regression can leave chapter names invisible after switching editions even though the underlying row data is present.

## What Changes

- Upgrade all current .NET test projects from xUnit v2 package references to the xUnit v3 package set compatible with the existing .NET 10 solution.
- Keep `dotnet test ChapterTool.Avalonia.slnx --no-restore` and focused project test commands working from CLI, IDE, and CI.
- Add Avalonia Headless support to `ChapterTool.Avalonia.Tests` so tests can construct the application/window in a headless platform without launching the desktop app.
- Add the first headless UI/unit coverage around XML, IFO, and MPLS edition or clip switching, reproducing the missing chapter-name display issue before implementation fixes it.
- Add initial headless UI coverage for the main shell that validates rendered controls, compiled bindings/localized text resource behavior, and command/input routing at runtime where practical.
- Preserve existing deterministic unit and integration tests while updating skip/exception patterns that are affected by xUnit v3 API changes.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `tests-build-distribution-assets`: Require the test suite to run on xUnit v3 and require Avalonia Headless-backed UI tests for the Avalonia shell, starting with XML/IFO/MPLS edition switching coverage that catches missing displayed chapter names.

## Impact

- Test project package references in `tests/ChapterTool.Core.Tests`, `tests/ChapterTool.Infrastructure.Tests`, and `tests/ChapterTool.Avalonia.Tests`.
- Any test code using xUnit v2-specific skip/exception APIs, especially infrastructure integration tests that currently throw `XunitException` for conditional skips.
- Avalonia test bootstrapping and potential app startup hooks needed to initialize `AppBuilder` with the headless backend.
- Main-window ViewModel or binding code involved in clip/edition switching and chapter-row name display for XML, IFO, and MPLS sources.
- CI and local test commands documented for the repository.
