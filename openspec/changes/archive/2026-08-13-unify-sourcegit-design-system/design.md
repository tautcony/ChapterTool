# Design: unify-sourcegit-design-system

## Context

The UI has two style systems:

- The SourceGit-derived layer: `Themes.axaml` (`Color.*` theme dictionaries, `Brush.*` brushes) and `Styles.axaml` (`Button.flat`, `Button.flat.primary`, `icon_button`, text classes). The Settings and Log views use it.
- The ChapterTool layer: `SharedResources.axaml` (`ChapterTool.*` brushes), inline `MainView` styles, and `AuxiliaryWindowStyles.axaml`. The main window and the small tool views use it.

`AvaloniaThemeApplicationService.Apply` already writes each theme preset into both layers: it sets every `ChapterTool.*` brush and every imported `Color.*` token, then sets `RequestedThemeVariant`. This double write is the main source of duplication.

`MainView.axaml` carries fractional sizes that come from the WinForms migration (original pixel values multiplied by 0.8). The main window base font is 11.2px while the SourceGit layer uses 13px.

Constraints:

- `ChapterTool.Wasm` hosts the same `MainView`; every change must work in the browser single-view host.
- `theme-preset-management` requires that frame-accuracy, validation-error, and log colors stay outside the cosmetic preset palette.
- Headless UI tests assert automation identifiers and some style behavior; test updates ship in the same change.

## Goals / Non-Goals

**Goals:**

- One design system (the SourceGit layer) for all views.
- One brush vocabulary per role; no alias brush that only mirrors another token.
- Integer device-independent pixel sizes; font sizes from the `ChapterTool.FontSize.*` tokens.
- Lower render cost for the Frames column while color stays the semantic channel.
- Visible entry points and shortcut hints for actions that today exist only in context menus.
- Removal of dead resources, dead selectors, dead class names, and hidden shim controls.

**Non-Goals:**

- No change to the theme preset catalog, preset ids, or persistence schema.
- No change to the `DataGridCell` monospace font (intentional).
- No redesign of workflow zones; the four-zone layout stays.
- No new third-party UI dependency.

## Decisions

### D1: Brush vocabulary — keep `Brush.*` as the surface vocabulary, keep `ChapterTool.*` only for semantic roles

The imported `Brush.*` tokens become the only vocabulary for surfaces, borders, and foregrounds (`Brush.Window`, `Brush.ToolBar`, `Brush.Contents`, `Brush.FG1`, `Brush.FG2`, `Brush.Border0/1/2`, `Brush.Accent`, `Brush.FlatButton.*`, `Brush.Popup`, `Brush.Link`).

`ChapterTool.*` keys stay only where `theme-preset-management` requires dedicated semantic colors or where no SourceGit equivalent exists:

- Frame accuracy: `ChapterTool.FrameNeutralBrush`, `FrameAccurateBrush`, `FrameInexactBrush`
- Diagnostics: `ChapterTool.DiagnosticErrorBrush`
- Log levels: `ChapterTool.LogInformationBrush`, `LogWarningBrush`, `LogErrorBrush`
- Expression syntax highlighting: `ChapterTool.Expression.*`
- Fonts: `ChapterTool.UiFontFamily`, `MonospaceFontFamily`, `FontSize.Small/Default/Large`

The imported vocabulary gains three tokens because hover, pressed, and selection backgrounds have no SourceGit equivalent outside the flat-button styles: `Color.Hover`, `Color.Active`, and `Color.Selection` (with matching `Brush.*` entries in `Themes.axaml`, light and dark defaults taken from the current `ChapterTool` values). `AvaloniaThemeApplicationService` writes them from `palette.HoverBackground`, `palette.ActiveBackground`, and the auxiliary selection blend.

Replacement table (authoritative; apply mechanically across all XAML):

| Removed alias | Replacement |
| --- | --- |
| `ChapterTool.WindowBackgroundBrush` | `Brush.Window` |
| `ChapterTool.PanelBackgroundBrush` | `Brush.ToolBar` |
| `ChapterTool.ControlBackgroundBrush` | `Brush.Contents` |
| `ChapterTool.ControlForegroundBrush` | `Brush.FG1` |
| `ChapterTool.MutedForegroundBrush` | `Brush.FG2` |
| `ChapterTool.AccentBrush` | `Brush.Accent` |
| `ChapterTool.AccentForegroundBrush` | delete; `primaryToolAction` consumers restyle to `flat primary`, which uses the Fluent `AccentButton*` resources |
| `ChapterTool.BorderBrush` | `Brush.Border1` |
| `ChapterTool.HoverBackgroundBrush` | `Brush.Hover` (new) |
| `ChapterTool.ActiveBackgroundBrush` | `Brush.Active` (new) |
| `ChapterTool.AuxiliaryTitleBackgroundBrush` | delete (no view consumer; `Color.TitleBar` also loses its last consumer and is deleted) |
| `ChapterTool.AuxiliaryToolbarBackgroundBrush` | `Brush.ToolBar` |
| `ChapterTool.AuxiliaryContentBackgroundBrush` | `Brush.Contents` |
| `ChapterTool.AuxiliaryControlBackgroundBrush` | `Brush.FlatButton.Background` |
| `ChapterTool.AuxiliaryPopupBackgroundBrush` | `Brush.Popup` |
| `ChapterTool.AuxiliaryBorderBrush` | `Brush.Border1` |
| `ChapterTool.AuxiliarySubtleBorderBrush` | `Brush.Border2` |
| `ChapterTool.AuxiliaryHoverBackgroundBrush` | `Brush.Hover` (new) |
| `ChapterTool.AuxiliaryPressedBackgroundBrush` | `Brush.Active` (new) |
| `ChapterTool.AuxiliarySelectionBackgroundBrush` | `Brush.Selection` (new) |
| `ChapterTool.AuxiliaryFocusBrush` | `Brush.Accent` |
| `ChapterTool.AuxiliaryDisabledForegroundBrush` | `Brush.FG2` |

Merge note: for the `Avalonia Default` preset the auxiliary hover value (`#FFFFFF`) differs from the main hover value (`#D6E9F8`); after the merge both resolve through `Brush.Hover` fed by `palette.HoverBackground`. This is an accepted, intentional visual unification. Known pre-existing quirk, out of scope: `flat primary` accent comes from Fluent `AccentButton*` resources, which follow the OS accent rather than the preset accent.

`AvaloniaThemeApplicationService` keeps one write path: preset palette → imported `Color.*` tokens → `RequestedThemeVariant`, plus the semantic `ChapterTool.*` brushes above. The `AuxiliaryPalette` blend logic moves into the imported token computation where it still adds value (`Color.ToolBar`, `Color.Contents`, `Color.Selection`) and keeps its formulas unchanged.

Alternative considered: rename `Brush.*` to `ChapterTool.*`. Rejected because it would rewrite the adapted SourceGit files and break easy diffing against upstream.

Migration safety: apply the table above key by key, then run the resource audit (D8) before deleting any definition.

### D2: Style classes — extend `Styles.axaml`, delete `AuxiliaryWindowStyles.axaml` and inline `MainView` styles

The main window adopts the existing classes: `Button.flat` for Load/Save primary actions, `Button.flat.primary` for accent actions (Apply in tool footers), `icon_button` for icon-only actions. Missing concepts move into `Styles.axaml` as new shared classes:

- `toolFooter` / `toolToolbar` containers (from `AuxiliaryWindowStyles.axaml`)
- `optionLabel` / `optionCell` for the bottom options grid
- `gridEditor` for DataGrid cell editors
- frame accuracy classes (`frameText.frameAccurate` etc., without glow variants)

Selectors scoped to `UserControl.auxiliaryWindow` become plain class selectors so the main window can reuse them; the `auxiliaryWindow` class stays only if a residual auxiliary-specific rule survives the audit, otherwise it is removed. Unused selectors in `Styles.axaml` (upstream SourceGit leftovers with no matching class or control in this repository) are deleted in the same audit; `Brush.*` tokens that lose their last consumer are deleted with them, together with their `Color.*` sources and the corresponding entries in `AvaloniaThemeApplicationService.ImportedThemeColorKeys`.

### D3: Sizing — integer scale with SourceGit metrics

Replacement table (old → new):

- Font sizes: 9.6 → 12 (`FontSize.Small`), 10.4 → 12, 11.2 → 13 (`FontSize.Default`), 12.8 → 13, 13.6 → 14 (`FontSize.Large`)
- Control heights: 19.2 → 24, 25.6 → 28, 27.2 → 28, 35.2 → 32 (icon buttons), 43.2 → 40 (primary buttons)
- Spacing/margins: 5.6 → 6, 6.4 → 6, 8 stays, 9.6 → 8, 11.2 → 12, 12.8 → 12, 14.4 → 16
- Widths: 41.6 → 48, 73.6 → 80, 94.4 → 96, 176/208/288 stay (already integer)
- Window: default 736×576 → 960×600, minimum 608×480 → 760×520. The advanced-options breakpoint (760) is re-checked against the new minimum width so the narrow layout still activates only below the default size.

Rationale: whole numbers render crisp borders at common DPI scales; 12/13/14 matches the SourceGit text scale already used in Settings and Log.

### D4: Frames column — one `TextBlock`, class-driven color, no effects

The cell template keeps a single `TextBlock` with `Classes.frameAccurate` / `Classes.frameInexact` / `Classes.frameNeutral` bindings that switch `Foreground`. Both `DropShadowEffect` glow layers and the two extra `TextBlock` layers are removed. Color remains the only semantic channel (user decision). To keep the accent visible without effects, the accurate/inexact states may also set `FontWeight="SemiBold"`; this is a styling detail, not a new semantic channel.

Alternative considered: keep one glow layer. Rejected: any `Effect` forces bitmap composition per cell and the cost returns with large chapter lists.

### D5: Discoverability — `SplitButton` for Load, visible menu for frame-rate actions, shortcut hints everywhere

- Load becomes a `SplitButton`: primary part runs browse-and-load; the flyout contains Reload (`Ctrl+R`/`F5` hint) and Append MPLS. The button context menu is removed. The inner `PART_PrimaryButton` and `PART_SecondaryButton` do not read the control `Background` in their state styles, so `Themes.axaml` overrides the Fluent `SplitButton*` and `Button*` state resources (`*BackgroundPointerOver` → `Color.Hover`, `*BackgroundPressed` → `Color.Active`, foreground and border to `Color.FG1` / `Color.Border2`). Class-level `:pointerover` `Background` setters on `SplitButton` do not work and must not be reintroduced.
- The clip Combine toggle stays in the chapter-grid context menu (already present there) and the `ComboBox` context menu is removed as a duplicate entry point.
- Change FPS moves from the frame-rate `ComboBox` context menu into a small `icon_button` next to the selector (with tooltip and accessible name); the context menu is removed.
- `MenuItem.InputGesture` shows the shortcut on Reload, Insert, Delete, and Preview items; Save and Load tooltips include `Ctrl+S` / `Ctrl+O`.

Rationale: right-click on buttons and combo boxes is not discoverable and has no touch equivalent. `SplitButton` is the standard control for "default action plus variants".

### D5a: NumericUpDown text area and spin buttons

- The Fluent `NumericUpDown` template forwards the control `Padding` to `PART_TextBox` with a template binding. A style setter on `PART_TextBox.Padding` loses to that binding. The shared style therefore sets `Padding="8,0"` on the `NumericUpDown` control itself.
- The Fluent `ButtonSpinner` template hardcodes `MinWidth="34"` on both spin buttons at template priority, which no style can override and which starves the value text. The shared style replaces the `ButtonSpinner` template inside `NumericUpDown` with a copy that uses the `ChapterTool.SpinnerRepeatButton` control theme (22px buttons, `Brush.Hover` / `Brush.Active` states). The replacement keeps the `PART_SpinnerPanel`, `PART_IncreaseButton`, `PART_DecreaseButton`, and `PART_ContentPresenter` names that the control code requires.
- Hover and focus turn the spinner border to `Brush.Accent`, which matches the `TextBox` and `ComboBox` input pattern.
- The style `MinWidth` stays 96 so the ja-JP wide layout cell (about 100px of input width at the 1100px checkpoint) does not overflow; with 22px buttons the text area still shows `-1000` without clipping.

### D6: Behavior fixes

- `ApplyAdvancedOptionsLayout` stores the current mode (wide/narrow) and returns early when the mode did not change, so resize no longer rebuilds `ColumnDefinitions` per pixel.
- The status-bar `ProgressBar` binds `IsVisible` to an `IsOperationRunning` (progress > 0 and < 1, or explicit busy flag from the ViewModel) so an empty bar does not show at idle.
- `LogToolView` replaces `ReflectionBinding IsInitiallyExpanded` with a compiled binding using `x:DataType`.
- The Log view `GridSplitter` column widens to 6px grab width.
- `OnExpressionEditorMultilineExpansionChanged` keeps the window-resize behavior on desktop but exits when `TopLevel` is not a `Window` (already implicit) and clamps the result to the screen working area; the single-view host relies on layout growth instead.
- The hidden `PathBox` is removed. Tests and automation set `MainWindowViewModel.SourcePath` directly; the `SourcePath` automation surface moves to the status-bar output-directory block if a UI probe is still required.

### D7: Accessibility

- `FrameRateBox` gets `AutomationProperties.Name` bound to the localized frame-rate label resource.
- `LoadExpressionButton` gets `AutomationProperties.Name` matching its tooltip.
- The status Log button uses `FontSize.Small` (12) and minimum height 24.

### D8: Audit method — dev script for text scanning, Headless tests for runtime resolution

Repository rules forbid tests that read `.cs`/`.axaml` files as text. The audit therefore splits:

- A development script `scripts/audit-ui-resources.ps1` (pwsh, runnable on macOS and Windows) scans `src/**/*.axaml` for `{DynamicResource ...}` / `{StaticResource ...}` keys and compares them against keys defined in `Themes.axaml`, `SharedResources.axaml`, `Styles.axaml`, and the keys written by `AvaloniaThemeApplicationService`. It prints two lists: unresolved references and unconsumed definitions. It runs during implementation and stays in the repository as a maintenance tool; it is not a test.
- Headless tests provide the runtime guarantee: instantiate `MainView` and every tool view inside the Headless application resources, apply one light and one dark preset, pump `RunJobs`, and assert that key controls resolve non-null brushes (window background, button background/foreground, grid line brushes, frame-accuracy foregrounds, log level foregrounds). These tests express the "resources resolve" scenarios of the delta specs.

### D9: Theme regression expectations and screenshots

- Theme color expectations are computed, not recorded: tests derive expected swatch colors from `ThemePresetCatalog` palettes plus the blend formulas (which this change keeps identical), then assert the applied resource values match. No "before" snapshot is required.
- Screenshots for `artifacts/` are captured with the Avalonia Headless `RenderTargetBitmap` path (`CaptureRenderedFrame`) at 960×600 (default), 1280×720 (wide), and 760×520 (narrow), for the main window plus each tool view, before (task 1.3) and after (task 4.5/9.2) the restyle. Screenshot generation is evidence, not a test assertion, in line with repository rules.

## Risks / Trade-offs

- [Visual regression across all views] → Capture before/after screenshots at default, wide, and narrow sizes under `artifacts/`; run Headless tests per project; manual pass over each theme preset (light and dark).
- [Alias brush removal misses a consumer and a control renders with a missing resource] → Run the D8 audit script before and after each deletion batch; the D8 Headless runtime-resolution tests guard the same failure at runtime. Delete only after both pass.
- [Theme presets look different after the auxiliary blend logic moves] → Keep the blend formulas identical; only the destination keys change. Compare rendered swatch colors for one light and one dark preset in tests.
- [`SplitButton` changes automation tree and breaks Headless tests] → Update the affected tests in the same task; keep the `LoadButton` automation id on the primary part.
- [Removing `PathBox` breaks drop/load flows that read `SourcePath`] → `SourcePath` stays a ViewModel property; only the hidden control goes away. Update tests that set text through the control.
- [Window default size change surprises users] → The size is not persisted today, so the new default applies cleanly; minimum size still fits the narrow layout breakpoint.

## Open Questions

None blocking. Icon-button placement for Change FPS (left or right of the selector) is decided during implementation from screenshot review.
