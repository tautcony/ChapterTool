## 1. Theme Model And Presets

- [x] 1.1 Replace the legacy six-slot theme settings model with a preset-id based model stored in `theme-settings.json`; intentionally ignore the old `theme-colors.json` shape.
- [x] 1.2 Define semantic theme tokens for `WindowBackground`, `PanelBackground`, `ControlBackground`, `ControlForeground`, `MutedForeground`, `Accent`, `AccentForeground`, `Border`, `HoverBackground`, and `ActiveBackground`.
- [x] 1.3 Implement a built-in preset catalog containing `Avalonia Default`, `Solarized Light`, `Solarized Dark`, `Gruvbox Light`, `Gruvbox Dark`, `Ayu Light`, `Ayu Mirage`, and `Ayu Dark`, with stable ids, preview swatches, and explicit light/dark base variants.
- [x] 1.4 Map `Avalonia Default` to the current Fluent/Avalonia-aligned light baseline and make it the reset/default preset.
- [x] 1.5 Update load/save behavior to persist the selected preset id, fall back to `Avalonia Default` for missing/blank/unknown ids without an implicit write, and retain corrupt-file preservation for malformed replacement JSON.

## 2. Theme Application

- [x] 2.1 Rename application-level theme brush resources from legacy slot names to semantic token names.
- [x] 2.2 Update `AvaloniaThemeApplicationService` to resolve the selected preset, write all semantic brushes centrally, and synchronize `Application.RequestedThemeVariant` with the preset's base variant.
- [x] 2.3 Update main-window, settings, expression-editor, popup, grid cell, border, hover, active, and foreground XAML bindings to consume semantic theme resources.
- [x] 2.4 Add shared `DataGridColumnHeader` semantic styles covering background, foreground, dividers/borders, hover, pressed, and sort-glyph states so existing headers refresh on runtime preset changes.
- [x] 2.5 Keep frame-accuracy, validation, warning, destructive, and expression syntax colors outside preset theme application.

## 3. Settings UI And ViewModel

- [x] 3.1 Replace the appearance tab's color-picker list with a single preset ComboBox and compact palette preview.
- [x] 3.2 Expose preset options and selected preset state from `SettingsToolViewModel`.
- [x] 3.3 Apply preset changes immediately to the running shell without saving the typed settings store until Save is invoked.
- [x] 3.4 Update Save, Reset, Discard, and unsaved-change behavior so appearance reset selects `Avalonia Default` and discard restores the saved preset.
- [x] 3.5 Remove first-release manual color editing and any `Custom` theme state from the settings surface.
- [x] 3.6 Add localized display names for all theme presets and appearance labels in Simplified Chinese, English, and Japanese resources.
- [x] 3.7 Remove the standalone legacy color-settings route, command/view/ViewModel, obsolete localization resources, and the now-unused `Avalonia.Controls.ColorPicker` package and style include.
- [x] 3.8 Make the non-editable palette preview update with selection, retain stable sizing, and expose an accessible localized name.

## 4. Tests

- [x] 4.1 Add infrastructure tests for preset-id load/save, missing/blank/unknown-id fallback, ignored legacy files, malformed replacement-file preservation, and no-write-on-load behavior.
- [x] 4.2 Add preset catalog tests covering all required `Avalonia Default`, `Solarized`, `Gruvbox`, and `Ayu` variants, unique ids, declared base variants, complete token values, contrast thresholds, and distinct hover/active states.
- [x] 4.3 Update theme application service tests to assert semantic brush resources and the matching Avalonia light/dark base variant instead of legacy slot resources.
- [x] 4.4 Update settings ViewModel tests for preset selection, live apply, Save, Reset, Discard, and dirty-state behavior.
- [x] 4.5 Update Avalonia Headless settings tests to select representative light and dark presets and verify live semantic resources, base-variant changes, existing DataGrid column-header effective brushes and state colors, preview updates, Save/Discard outcomes, and absence of the legacy editing route as part of those workflows.
- [x] 4.6 Add localization behavior coverage proving open preset options/preview names refresh across Simplified Chinese, English, and Japanese without changing the selected stable id.

## 5. Maintainer Navigation

- [x] 5.1 Update `docs/code-map/infrastructure.md`, `docs/code-map/avalonia.md`, and `docs/code-map/testing.md` for the preset catalog, replacement settings file/store, application service, settings UI, and high-signal tests.

## 6. Verification

- [x] 6.1 Run `openspec validate "upgrade-theme-settings-presets" --strict`.
- [x] 6.2 Build `src\ChapterTool.Avalonia\ChapterTool.Avalonia.csproj --no-restore` after the app project and theme resources change.
- [x] 6.3 Run `dotnet test tests\ChapterTool.Infrastructure.Tests\ChapterTool.Infrastructure.Tests.csproj --no-restore`.
- [x] 6.4 Run `dotnet test tests\ChapterTool.Avalonia.Tests\ChapterTool.Avalonia.Tests.csproj --no-restore` after the Infrastructure tests finish.
- [x] 6.5 Run `dotnet test ChapterTool.Avalonia.slnx --no-restore` before finalizing the broad change.
