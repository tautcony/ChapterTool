## 1. xUnit v3 Migration

- [x] 1.1 Identify the latest stable xUnit v3 framework and runner package versions compatible with .NET 10 and the existing `Microsoft.NET.Test.Sdk` version.
- [x] 1.2 Update `tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj` from xUnit v2 to the selected xUnit v3 package set.
- [x] 1.3 Update `tests/ChapterTool.Infrastructure.Tests/ChapterTool.Infrastructure.Tests.csproj` from xUnit v2 to the selected xUnit v3 package set.
- [x] 1.4 Update `tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj` from xUnit v2 to the selected xUnit v3 package set.
- [x] 1.5 Audit direct `Xunit.Sdk` usage and migrate conditional skip or exception patterns to xUnit v3-compatible APIs.
- [x] 1.6 Run focused Core and Infrastructure tests to catch framework migration issues before adding UI runtime changes.

## 2. Avalonia Headless Test Harness

- [x] 2.1 Add the Avalonia Headless test dependency to `ChapterTool.Avalonia.Tests` using a version compatible with the app's Avalonia 12.0.x packages.
- [x] 2.2 Create a reusable headless Avalonia test fixture that initializes the app once without `StartWithClassicDesktopLifetime`.
- [x] 2.3 Add helpers to construct `MainWindow` with fake services and deterministic `MainWindowViewModel` inputs.
- [x] 2.4 Add dispatcher/layout helpers so tests can wait for bindings, layout, and DataGrid realization without timing sleeps.
- [x] 2.5 Verify a smoke headless test can load `MainWindow` compiled XAML, apply localization resources, and complete layout.

## 3. XML/IFO/MPLS Name Display Regression Tests

- [x] 3.1 Add deterministic multi-option XML test data or fake load result with distinct chapter names per edition.
- [x] 3.2 Add deterministic multi-option IFO test data or fake load result with distinct chapter names per program-chain/title option.
- [x] 3.3 Add deterministic multi-option MPLS test data or fake load result with distinct chapter names per playlist/clip option.
- [x] 3.4 Add a headless test that selects a non-default XML edition through the visible selector and asserts the rendered chapter grid name column shows the selected edition names.
- [x] 3.5 Add a headless test that selects a non-default IFO option through the visible selector and asserts the rendered chapter grid name column shows the selected option names.
- [x] 3.6 Add a headless test that selects another MPLS clip through the visible selector and asserts the rendered chapter grid name column shows the selected clip names.
- [x] 3.7 Confirm at least one new headless test reproduces the missing displayed-name issue before applying the production fix.

  Implementation note: the Headless coverage now reproduces UI-only failures before the final fix: the default selected clip/edition label is blank after load, the dropdown option list needs explicit source-option templating, and XML edition selection can clear `SelectedClipIndex` through the `ComboBox` binding path when the selected option is replaced after auto frame-rate refresh.

## 4. Production Fix

- [x] 4.1 Trace the failing path to the smallest affected layer: ViewModel row refresh/projection, `ClipBox` selection event adaptation, DataGrid binding, or layout realization.
- [x] 4.2 Fix the selected option update so `Rows` and the rendered DataGrid name column both reflect the selected XML, IFO, or MPLS option.
- [x] 4.3 Preserve existing behavior for frame-rate detection, selected clip index notifications, current export options, and chapter edit/save flows.
- [x] 4.4 Add or update ViewModel-level assertions only where they protect logic not covered by the Headless tests.

  Implementation note: the failing path was in the UI layer, not ViewModel row projection. `ClipBox` now renders `ChapterSourceOption.DisplayName` explicitly for both dropdown items and the closed selection box, and its `SelectedIndex` binding is one-way so collection item replacement during XML auto-frame refresh cannot push a transient `-1` back into `SelectedClipIndex`. The window refresh path pushes the ViewModel selection back into the control under the `isRefreshing` guard. The Headless tests now cover fake multi-option chapter-name switching and real importer-backed XML/IFO/MPLS default and changed option labels.

## 5. Verification

- [x] 5.1 Run `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore`.
- [x] 5.2 Run `dotnet test ChapterTool.Avalonia.slnx --no-restore`.
- [x] 5.3 Run `openspec validate upgrade-xunit3-add-avalonia-headless --strict`.
- [x] 5.4 Document any unavoidable package/API compatibility decisions in the final implementation summary.

  Verification note: the Avalonia project and solution test commands ran again after the selector-template fix. Headless tests pass independently; the broader runs still fail on existing ffprobe availability and MP4 fixture expectation cases unrelated to the Headless selector fix.
