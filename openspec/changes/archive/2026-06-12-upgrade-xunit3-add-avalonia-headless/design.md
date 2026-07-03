## Context

The .NET 10 solution has three test projects using `Microsoft.NET.Test.Sdk` 18.6.0, `coverlet.collector` 10.0.1, `xunit` 2.9.3, and `xunit.runner.visualstudio` 3.1.5. This leaves the suite on xUnit v2 while already depending on a v3 runner package.

`ChapterTool.Avalonia.Tests` currently validates ViewModels, composition, project boundaries, runtime import/save services, and static XAML/text properties. It does not initialize an Avalonia headless platform or render `MainWindow`, so regressions in realized controls, DataGrid cells, localized resources, and control event routing can pass tests. XML, IFO, and MPLS sources are especially exposed because they can provide multiple source options that must update the chapter rows and visible name column when the user changes editions or clips.

## Goals / Non-Goals

**Goals:**

- Move all current test projects to xUnit v3 package references that work with `dotnet test` on the .NET 10 solution.
- Keep existing unit and integration coverage intact while updating xUnit v2-specific skip/exception patterns.
- Add an Avalonia Headless bootstrap for `ChapterTool.Avalonia.Tests` using the same Avalonia major version as the app.
- Add the first headless UI tests around XML, IFO, and MPLS edition/clip switching, verifying that the rendered chapter grid shows the selected edition's chapter names.
- Fix the name-display regression surfaced by those tests in the smallest affected ViewModel, binding, or event-routing code path.

**Non-Goals:**

- Replacing the current unit/integration test structure with an end-to-end GUI automation suite.
- Adding FlaUI, Playwright, or OS desktop automation.
- Reworking importer parsing behavior beyond what is required to create deterministic XML, IFO, and MPLS source-option fixtures for the UI tests.
- Centralizing every NuGet dependency if the existing project style can be preserved with scoped package updates.

## Decisions

1. Use xUnit v3 as the test framework package, keeping the Visual Studio runner package aligned with v3.
   - Rationale: The request is specifically to update to xUnit 3, and the current runner is already v3. Updating the framework package removes the mixed-version state while preserving `dotnet test` usage.
   - Alternative considered: Keep xUnit v2 and only add Headless support. This would leave the requested framework migration incomplete and retain mixed test infrastructure.

2. Add Avalonia Headless only to `ChapterTool.Avalonia.Tests`.
   - Rationale: Core and Infrastructure tests should remain UI-free. The UI project is the only project that needs an Avalonia runtime and rendered controls.
   - Alternative considered: Add Headless packages to all test projects for convenience. That would blur project boundaries and increase restore/runtime surface unnecessarily.

3. Provide a dedicated test AppBuilder/fixture for headless UI tests instead of starting the production desktop lifetime.
   - Rationale: Tests must initialize Avalonia once with `UseHeadless` and avoid `UsePlatformDetect().StartWithClassicDesktopLifetime`. A fixture keeps runtime initialization deterministic and prevents the desktop app from launching during tests.
   - Alternative considered: Reuse `Program.BuildAvaloniaApp()` directly. Production setup uses platform detection and desktop lifetime assumptions that are not appropriate for headless CI tests.

4. Make the first Headless tests exercise the rendered `MainWindow` with fake load services that return XML, IFO, and MPLS multi-option groups.
   - Rationale: Existing ViewModel tests can prove `Rows` changes, but they cannot prove the visible name column, item templates, localized headers, or selection event routing update after a user-level selection. Rendering the window catches the missing displayed-name failure at the right layer.
   - Alternative considered: Add only parser/importer unit tests for XML, IFO, and MPLS. Those already cover source-option parsing and would not reproduce UI display failures.

5. Keep fixtures deterministic and in-process.
   - Rationale: XML can be represented with temporary text, and UI edition-switch tests can use fake `IChapterLoadService` results. IFO/MPLS parser fixtures may be reused where useful, but Headless UI tests should not depend on external tools, installed media software, or machine-specific paths.
   - Alternative considered: Drive real files through every importer in Headless tests. That would make UI tests slower and duplicate importer coverage already present in Core/Infrastructure tests.

## Risks / Trade-offs

- xUnit v3 API differences may require mechanical updates to conditional skip or exception code -> Audit all direct `Xunit.Sdk` usage and convert to the supported v3 pattern before running the full suite.
- Avalonia Headless package naming/support can differ by Avalonia major version -> Choose the package/version compatible with Avalonia 12.0.x and keep the bootstrap isolated in the Avalonia test project.
- Avalonia UI tests can become flaky if they assert raw visual tree timing too aggressively -> Use headless dispatcher helpers, wait for layout/render completion, and assert stable control state rather than screenshots for unit-level coverage.
- DataGrid virtualization may hide unrealized cells -> Set stable test window size/layout, force layout passes, and assert through realized rows/cells or bound item containers in a deterministic way.
- The fix for missing displayed names may live in ViewModel projection, DataGrid binding, or selection event adaptation -> Start with failing Headless tests, then keep the code fix scoped to the smallest path that makes selected option names appear in both `Rows` and rendered grid cells.

## Migration Plan

1. Update test project package references to xUnit v3-compatible packages and restore.
2. Convert any xUnit v2-specific skip/exception usage.
3. Add the Avalonia Headless dependency and test bootstrap in `ChapterTool.Avalonia.Tests`.
4. Add failing headless tests for XML, IFO, and MPLS edition/clip switching and visible chapter names.
5. Fix the selected-option/name-display path until the new tests pass.
6. Run focused Avalonia tests, then the full solution test command.

Rollback is straightforward: revert test package changes, the headless test harness, added tests, and the focused UI fix if the migration reveals a blocking upstream incompatibility.

## Open Questions

- The exact xUnit v3 and Avalonia Headless package versions should be selected during implementation based on the latest compatible NuGet versions for .NET 10 and Avalonia 12.0.x.
