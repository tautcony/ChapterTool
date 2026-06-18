## Context

The Avalonia test project already initializes an Avalonia Headless runtime and verifies that the main window loads compiled XAML, lays out, and renders selected XML/IFO/MPLS option labels. Most richer UI behavior is currently covered at the ViewModel layer only. That leaves gaps where XAML bindings, named controls, keyboard handlers, context menus, command enablement, localization resources, and responsive layout can regress while ViewModel tests still pass.

The change is test-only unless new tests expose existing UI defects. It must follow the repository rule that source/configuration files are not validated by static string assertions; UI coverage should come from compiled Avalonia views, behavior-level assertions, and rendered headless verification.

## Goals / Non-Goals

**Goals:**

- Build a comprehensive, deterministic headless UI test matrix for the main window and secondary tool views.
- Verify rendered controls and user interactions route to the same ViewModel commands and platform-service abstractions used at runtime.
- Exercise localization and responsive layout through Avalonia rendering rather than source-text inspection.
- Keep CI-compatible execution through `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore` and the full solution test command.

**Non-Goals:**

- Do not replace Core, Infrastructure, or ViewModel unit tests with UI tests.
- Do not introduce screenshot pixel-perfect approvals or brittle static assertions over `.axaml`/`.cs` source files.
- Do not require installed media tools, native desktop sessions, Windows registry state, or machine-specific absolute paths.
- Do not redesign the UI except for focused fixes required by failing tests.

## Decisions

1. Expand the existing headless host instead of adding a second harness.

   The current `MainWindowHeadlessTestHost` already wires deterministic fake services, opens the compiled `MainWindow`, performs layout, finds named controls, and inspects rendered text. Extending it keeps setup costs low and keeps headless tests consistent. The alternative was per-test window construction, but that would duplicate service wiring and make command/service observability inconsistent.

2. Test rendered behavior at UI boundaries and leave business rules in existing unit tests.

   Headless tests should assert that a visible control is present, bound, enabled/disabled correctly, routes interactions, and renders updated state. They should not duplicate every transformation/export parser assertion already covered in Core or ViewModel tests. This keeps the headless suite valuable without making it slow or brittle.

3. Use fake services and in-process fixtures as the test boundary.

   Load, save, file picking, window opening, shell opening, settings stores, and localization should be observable fakes. Existing real parser fixtures can be used only when deterministic and in-process. External processes and installed tools remain out of scope for headless UI tests.

4. Prefer semantic rendered assertions over pixel comparisons.

   The suite should inspect actual Avalonia controls, binding values, visual-tree text, selection state, visibility, enabled state, and fake-service calls. Screenshots should be captured for the required visual layout sizes and saved under `artifacts/`, but pass/fail criteria should avoid fragile exact-pixel matching unless a specific rendering defect requires it.

5. Split headless tests by workflow area.

   Organize tests into focused files such as main-window layout/state, main-window commands/input, grid command surfaces, localization, and secondary tools. This keeps failures diagnosable and avoids a single large test class that mixes unrelated workflows.

## Risks / Trade-offs

- Headless rendering can be slower than pure unit tests -> keep scenarios focused, share helpers, and avoid unnecessary real parser or screenshot work in every case.
- Avalonia virtualization can hide DataGrid text until layout/realization completes -> provide host helpers that force layout passes, scroll or select rows when needed, and assert both control state and rendered text where appropriate.
- Keyboard and context-menu behavior can be sensitive to focus -> use explicit focus setup and command/service side effects as the primary assertion.
- Localization tests may become brittle if display copy changes intentionally -> assert active-language rendering for representative labels/status messages and absence of localization keys/mojibake rather than exhaustive text snapshots.
- Screenshot artifacts can churn -> reserve screenshots for default/wide/narrow layout verification and keep them as evidence artifacts, not golden approvals.
