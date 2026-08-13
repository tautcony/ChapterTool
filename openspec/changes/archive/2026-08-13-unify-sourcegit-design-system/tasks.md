# Tasks: unify-sourcegit-design-system

## 1. Safety Net

- [x] 1.1 Create the dev script `scripts/audit-ui-resources.ps1` per design D8: scan `src/**/*.axaml` resource references against keys defined in the resource files and written by `AvaloniaThemeApplicationService`; print unresolved references and unconsumed definitions; record the initial unconsumed-definition list in the change notes
- [x] 1.2 Add Headless runtime-resolution tests per design D8: instantiate `MainView` and every tool view with application resources, apply one light and one dark preset, and assert key controls resolve non-null brushes
- [x] 1.3 Capture baseline screenshots per design D9 (Headless `RenderTargetBitmap`, 960×600 / 1280×720 / 760×520) of the main window and each tool view under `artifacts/`

## 2. Shared Style Layer

- [x] 2.1 Move the container and label classes from `AuxiliaryWindowStyles.axaml` into `Styles.axaml` as plain class selectors (`toolFooter`, `toolToolbar`, `toolLabel`, `toolStatus`, `toolTitle`, `primaryToolAction` mapped onto `flat primary`, `iconToolAction` mapped onto `icon_button`)
- [x] 2.2 Add shared classes for the main-window concepts to `Styles.axaml`: `optionLabel`, `optionCell`, `gridEditor`, and the frame-accuracy text classes (`frameText`, `frameAccurate`, `frameInexact`, `frameNeutral`) without glow variants
- [x] 2.3 Restyle `MainView.axaml` onto the shared classes: `flat` for Load/Save, `icon_button` for Preview/Refresh/Settings/template/script buttons, shared input styling; delete the local `UserControl.Styles` block except view-specific leftovers that the audit justifies
- [x] 2.4 Restyle the small tool views (`Language`, `ForwardShift`, `TemplateNames`, `Expression`, `Text`) onto the shared classes and remove the `auxiliaryWindow` class where no auxiliary-specific rule remains
- [x] 2.5 Delete `AuxiliaryWindowStyles.axaml` and its include in `SharedStyles.axaml`; run both Avalonia test projects
- [x] 2.6 Define or remove the dead `settingsPageScroller` class references and delete the always-hidden `sectionTitle` elements and their style in `SettingsToolView.axaml`
- [x] 2.7 Prune selectors in `Styles.axaml` that match no control or class used in this repository (SourceGit leftovers), guided by the audit script output

## 3. Brush Vocabulary Merge

- [x] 3.1 Add the new imported tokens `Color.Hover`, `Color.Active`, `Color.Selection` (plus `Brush.*` entries with light/dark defaults) to `Themes.axaml` and wire them in `AvaloniaThemeApplicationService`; then apply the authoritative replacement table from design D1 across all XAML
- [x] 3.2 Remove the replaced alias brushes from `SharedResources.axaml`, keeping fonts, font-size tokens, and the semantic brushes (frame accuracy, diagnostics, log levels, expression highlighting)
- [x] 3.3 Simplify `AvaloniaThemeApplicationService`: keep one write path (palette → imported `Color.*` tokens → `RequestedThemeVariant` plus semantic `ChapterTool.*` brushes); move the auxiliary blend output into the imported token computation with unchanged formulas
- [x] 3.4 Delete imported tokens with no consumer (`Color./Brush.Diff.*`, `Conflict.*`, `Badge`, `BadgeFG`, `HistoryBG`, `TitleBar`, and any others the audit lists) from `Themes.axaml`, `ImportedThemeColorKeys`, and the service write path
- [x] 3.5 Add a theme test per design D9 that applies one light and one dark preset and asserts applied resource values match expectations computed from `ThemePresetCatalog` palettes and the unchanged blend formulas; re-run the audit script and confirm the unconsumed-definition list is empty
- [x] 3.6 Verify the Wasm host builds and renders with the merged resource layer

## 4. Integer Sizing

- [x] 4.1 Apply the sizing replacement table from the design to `MainView.axaml` (fonts 12/13/14 via `ChapterTool.FontSize.*`, heights 24/28/32/40, integer spacing)
- [x] 4.2 Raise the status-strip Log button to font size 12 and minimum height 24; keep the strip height consistent
- [x] 4.3 Update `MainWindow` default size to 960×600 and minimum size to 760×520; confirm the 760 advanced-options breakpoint still selects the narrow layout only below the default width
- [x] 4.4 Sweep the remaining views and `ExpressionEditor` for fractional values and replace them with the integer scale
- [x] 4.5 Update Headless layout tests that assert old sizes; capture after screenshots at default, wide, and narrow sizes

## 5. Frames Column

- [x] 5.1 Replace the three-layer Frames cell template with one `TextBlock` using the shared frame-accuracy classes; remove both `DropShadowEffect` layers
- [x] 5.2 Add `FontWeight="SemiBold"` to the accurate/inexact classes so the state stays visually prominent without effects
- [x] 5.3 Update or add a Headless test that drives a frame-accurate and a frame-inexact row and asserts the cell foreground uses the matching semantic brush

## 6. Discoverability

- [x] 6.1 Replace the Load `Button` with a `SplitButton`: primary part keeps `BrowseAndLoadCommand` and the `LoadButton` automation id; flyout holds Reload and Append MPLS; remove the button context menu
- [x] 6.2 Remove the Combine entry from the clip `ComboBox` context menu (the chapter-grid context menu keeps it); remove the frame-rate `ComboBox` context menu and add a visible `icon_button` next to the selector for Change FPS with tooltip and accessible name
- [x] 6.3 Add `InputGesture` display to menu items with shortcuts (Reload, Insert, Delete, Preview) and append shortcut text to the Save (`Ctrl+S`) and Load (`Ctrl+O`) tooltips; add the localized strings to all three locale files as valid UTF-8
- [x] 6.4 Update Headless tests for the new command surfaces (SplitButton flyout, Change FPS button) and verify shortcut routing still passes

## 7. Behavior Fixes

- [x] 7.1 Guard `ApplyAdvancedOptionsLayout` with a stored wide/narrow mode and return early when the mode is unchanged; add a unit test for the threshold crossing
- [x] 7.2 Bind the status-strip `ProgressBar.IsVisible` to a ViewModel busy state so the bar hides at idle; add a ViewModel test for the busy transitions
- [x] 7.3 Replace `ReflectionBinding IsInitiallyExpanded` in `LogToolView` with a compiled binding
- [x] 7.4 Widen the Log view `GridSplitter` column to a 6px grab area
- [x] 7.5 Clamp the expression-editor window growth to the screen working area in `OnExpressionEditorMultilineExpansionChanged`; keep the single-view host on layout growth
- [x] 7.6 Remove the hidden `PathBox` from `MainView.axaml`; migrate tests and automation that used it to set `MainWindowViewModel.SourcePath` directly

## 8. Accessibility

- [x] 8.1 Add `AutomationProperties.Name` to the frame-rate selector bound to its localized label resource
- [x] 8.2 Add `AutomationProperties.Name` to `LoadExpressionButton` matching its tooltip
- [x] 8.3 Add a Headless test that asserts accessible names exist for icon-only controls in the main window

## 9. Verification And Docs

- [x] 9.1 Run `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore`, then the Headless project, then the full solution in sequence
- [x] 9.2 Manually verify each theme preset (one light, one dark at minimum) over the restyled main window and tool views; store screenshots under `artifacts/`
- [x] 9.3 Update `docs/code-map/avalonia.md` for the removed `AuxiliaryWindowStyles.axaml`, the merged brush vocabulary, and the new command surfaces
- [x] 9.4 Validate the change with `openspec validate "unify-sourcegit-design-system" --strict`
