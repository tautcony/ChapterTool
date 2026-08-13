# Proposal: unify-sourcegit-design-system

## Why

The Avalonia UI now contains two parallel design systems. The main window uses ad-hoc `ChapterTool.*` brushes with inline styles and fractional WinForms-derived sizes (values multiplied by 0.8, such as `9.6`, `27.2`, `35.2`). The Settings and Log views use the SourceGit-derived style layer (`Brush.*`, `Button.flat`, `icon_button`). The two systems produce inconsistent visuals, duplicate resources, and dead style content. The SourceGit style layer is the chosen base. The whole UI must converge on it.

## What Changes

- Adopt the SourceGit style layer as the single design system for the main window, the tool views, and the auxiliary window styles.
- Merge `AuxiliaryWindowStyles.axaml` and the inline `MainView` styles into the shared SourceGit-based style layer. Keep dedicated semantic brushes (frame accuracy, log levels, diagnostics, expression highlighting) as required by `theme-preset-management`.
- Remove `ChapterTool.*` alias brushes that duplicate a `Brush.*` token. Simplify `AvaloniaThemeApplicationService` and `SharedResources.axaml` to match.
- Remove imported SourceGit resources that no view consumes (for example `Color.Diff.*`, `Color.Conflict.*`, `Color.Badge`, `Color.HistoryBG`). Prune unused selectors from `Styles.axaml`. Remove dead style class names (`settingsPageScroller`) and always-hidden elements (`sectionTitle`).
- Replace all fractional device-independent pixel values with an integer scale. Use the `ChapterTool.FontSize.*` tokens (12/13/14) for text. Raise the status-bar Log button above the current 9.6px font and 19.2px height.
- Keep color as the only semantic channel for frame accuracy, but render the Frames cell with one `TextBlock` and no `DropShadowEffect` layers. **BREAKING** for the visual glow effect only; the color semantics stay.
- Make hidden context-menu actions discoverable: expose Reload and Append MPLS from a visible split-style Load control, and show keyboard shortcut hints on menu items and tooltips.
- Fix behavior issues found in review: reflow the advanced options grid only when the width crosses the breakpoint, hide the progress bar when no operation runs, replace `ReflectionBinding` with a compiled binding in the Log view, widen the Log view `GridSplitter` grab area, guard the expression-editor window resize for non-window hosts, and remove the hidden `PathBox` shim.
- Add missing accessible names (frame-rate selector, Lua script load button).
- Out of scope: `DataGridCell` monospace font (intentional), theme preset catalog contents, any Core/Infrastructure behavior.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `avalonia-ui-shell`: Add a requirement for a single shared design system with integer sizing and minimum readable font sizes. Extend discoverability scenarios so context-menu-only actions gain visible entry points and shortcut hints. Extend the hidden-shim requirement so state-only hidden controls (`PathBox`) are also absent. Add scenarios for idle progress-bar visibility and the single-layer frame-accuracy indicator.
- `theme-preset-management`: Narrow the imported-token requirement so presets provide only the tokens that the style layer consumes, and the resource set contains no unused imported tokens.

## Impact

- `src/ChapterTool.Avalonia.UI/Views/MainView.axaml` and `MainView.axaml.cs`
- `src/ChapterTool.Avalonia.UI/Views/Tools/*.axaml` (Settings, Log, Text, Expression, ForwardShift, Language, TemplateNames)
- `src/ChapterTool.Avalonia.UI/Views/Styles/AuxiliaryWindowStyles.axaml` (merged, then removed)
- `src/ChapterTool.Avalonia.UI/Resources/Styles.axaml`, `Themes.axaml`, `SharedStyles.axaml`, `SharedResources.axaml`
- `src/ChapterTool.Avalonia.UI/Views/Controls/ExpressionEditor.axaml` and code-behind
- `src/ChapterTool.Avalonia/Services/AvaloniaThemeApplicationService.cs` and `src/ChapterTool.Avalonia/Views/MainWindow.axaml.cs`
- `tests/ChapterTool.Avalonia.Tests` and `tests/ChapterTool.Avalonia.Headless.Tests` (style-contract, theme, and UI behavior tests; tests that use the hidden `PathBox`)
- `docs/code-map/avalonia.md`
- The Wasm host (`ChapterTool.Wasm`) consumes the same shared UI project and must keep working.
