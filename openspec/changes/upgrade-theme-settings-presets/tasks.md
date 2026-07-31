## 1. Theme Model And Presets

- [ ] 1.1 Replace the legacy six-slot theme settings model with a preset-id based theme settings model.
- [ ] 1.2 Define semantic theme tokens for `WindowBackground`, `PanelBackground`, `ControlBackground`, `ControlForeground`, `MutedForeground`, `Accent`, `AccentForeground`, `Border`, `HoverBackground`, and `ActiveBackground`.
- [ ] 1.3 Implement a built-in preset catalog containing `Avalonia Default`, `Solarized Light`, `Solarized Dark`, `Gruvbox Light`, `Gruvbox Dark`, `Ayu Light`, `Ayu Mirage`, and `Ayu Dark`.
- [ ] 1.4 Map `Avalonia Default` to the current Fluent/Avalonia-aligned light baseline and make it the reset/default preset.
- [ ] 1.5 Update theme settings load/save behavior to persist the selected preset id without preserving compatibility with the old six-slot JSON shape.

## 2. Theme Application

- [ ] 2.1 Rename application-level theme brush resources from legacy slot names to semantic token names.
- [ ] 2.2 Update `AvaloniaThemeApplicationService` to resolve the selected preset and write all semantic brushes centrally.
- [ ] 2.3 Update main-window, settings, expression-editor, popup, grid, border, hover, active, and foreground XAML bindings to consume semantic theme resources.
- [ ] 2.4 Keep frame-accuracy, validation, warning, destructive, and expression syntax colors outside preset theme application.

## 3. Settings UI And ViewModel

- [ ] 3.1 Replace the appearance tab's color-picker list with a single preset ComboBox and compact palette preview.
- [ ] 3.2 Expose preset options and selected preset state from `SettingsToolViewModel`.
- [ ] 3.3 Apply preset changes immediately to the running shell without saving the typed settings store until Save is invoked.
- [ ] 3.4 Update Save, Reset, Discard, and unsaved-change behavior so appearance reset selects `Avalonia Default` and discard restores the saved preset.
- [ ] 3.5 Remove first-release manual color editing and any `Custom` theme state from the settings surface.
- [ ] 3.6 Add localized display names for all theme presets and appearance labels in Simplified Chinese, English, and Japanese resources.

## 4. Tests

- [ ] 4.1 Add infrastructure tests for preset-id load/save behavior and default reset behavior.
- [ ] 4.2 Add preset catalog tests covering all required `Avalonia Default`, `Solarized`, `Gruvbox`, and `Ayu` variants.
- [ ] 4.3 Update theme application service tests to assert semantic brush resources instead of legacy slot resources.
- [ ] 4.4 Update settings ViewModel tests for preset selection, live apply, Save, Reset, Discard, and dirty-state behavior.
- [ ] 4.5 Update Avalonia headless settings tests to assert the preset selector and palette preview render without color pickers.
- [ ] 4.6 Add or update UI rendering coverage for representative light and dark presets.

## 5. Verification

- [ ] 5.1 Run `openspec validate "upgrade-theme-settings-presets" --strict`.
- [ ] 5.2 Run `dotnet test tests\ChapterTool.Infrastructure.Tests\ChapterTool.Infrastructure.Tests.csproj --no-restore`.
- [ ] 5.3 Run `dotnet test tests\ChapterTool.Avalonia.Tests\ChapterTool.Avalonia.Tests.csproj --no-restore`.
- [ ] 5.4 Capture default, wide, and narrow screenshots for representative light and dark theme presets under `artifacts/`.
