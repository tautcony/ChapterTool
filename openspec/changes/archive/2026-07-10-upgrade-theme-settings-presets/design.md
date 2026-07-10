## Context

The current settings panel exposes appearance as six low-level color slots backed by `ThemeColorSettings` and applied through six global brush resources. That model is implementation-oriented: the names do not describe user-visible surfaces, the settings UI does not guide users toward coherent palettes, and the app has no curated built-in themes.

This change intentionally breaks from the legacy slot workflow. Compatibility with the existing `theme-colors.json` shape and manual six-color editing are out of scope. Theme selection should become a preset-first settings workflow using established terminal/editor palette families.

The existing app already centralizes theme application through `AvaloniaThemeApplicationService`, so the main implementation shape should remain resource-driven. The important change is replacing legacy slot resources with semantic theme tokens that map to the actual shell, tool, input, grid, and interaction surfaces.

## Goals / Non-Goals

**Goals:**

- Replace the raw six-slot appearance workflow with a single preset selector in Settings
- Provide `Avalonia Default` plus built-in presets for `Solarized`, `Gruvbox`, and `Ayu` families
- Define a semantic token model that covers the UI surfaces and states ChapterTool actually themes
- Keep theme application centralized so the main window and secondary tools update consistently
- Synchronize Fluent control theme variants with light and dark presets
- Make preset selection testable at ViewModel, settings-store, resource, and headless UI levels

**Non-Goals:**

- Preserving compatibility with the current `ThemeColorSettings` JSON shape or legacy six-slot editing UX
- Shipping manual theme editing, `Custom` themes, or arbitrary color overrides in the first release
- Theming syntax-highlight tokens, frame-accuracy semantic colors, validation-error colors, or warning/destructive colors
- Introducing third-party theme packages, runtime theme downloads, or a full theme editor

## Decisions

### 1. Replace the six-slot model with preset identity plus semantic theme tokens

The existing `ThemeColorSettings` shape should be replaced by a new settings model centered on a preset id and a resolved semantic palette. Persisted settings should record the selected preset id. Built-in preset definitions should provide the complete token values used at runtime.

Rationale:

- Preset identity is stable and easier to reason about than matching raw color values
- Semantic tokens describe UI responsibility rather than historical implementation
- A hard break is simpler and cleaner than supporting two appearance models

Alternatives considered:

- Extending the existing six-slot record with preset metadata: rejected because it preserves the old mental model
- Inferring the selected preset from color equality: rejected because future palette tuning would make persisted state ambiguous

### 2. Keep the first release preset-only

The appearance tab should expose one theme preset ComboBox and a compact preview of the selected palette. It should not expose advanced color editors or a `Custom` option in the first release.

Rationale:

- The requested upgrade is about coherent theme selection, not a general color editor
- Removing manual overrides keeps persistence, dirty-state handling, and discard behavior simpler
- Preset-only behavior produces clearer specifications and behavior tests

Alternatives considered:

- Preset plus advanced semantic overrides: rejected for the first release because it reintroduces low-level editing and unclear `Custom` state behavior
- Family selector plus variant selector: rejected because a single preset list is enough for the initial eight presets

### 3. Ship Avalonia Default plus seven curated family variants

The built-in preset catalog should include:

- `Avalonia Default`
- `Solarized Light`
- `Solarized Dark`
- `Gruvbox Light`
- `Gruvbox Dark`
- `Ayu Light`
- `Ayu Mirage`
- `Ayu Dark`

Rationale:

- `Avalonia Default` gives users a stable native baseline and a predictable reset target
- These families directly match the requested direction
- They are familiar to terminal and editor users
- The set covers light, dark, and intermediate contrast without making Settings noisy

### 4. Use semantic tokens with explicit UI responsibility

The theme model should define tokens for the surfaces and states ChapterTool currently needs:

- `WindowBackground`: app and tool-window background, status strip base, outer layout bands
- `PanelBackground`: grouped option areas, settings pages, tool panels, menu/popup base surfaces
- `ControlBackground`: text boxes, combo boxes, grid cells, DataGrid column-header base, expression editor base, editable input surfaces
- `ControlForeground`: primary text, icons, grid text, DataGrid column-header text and sort glyphs, labels, button content
- `MutedForeground`: helper text, status details, placeholder-like secondary text
- `Accent`: focused controls, selected tabs, primary action emphasis, active selection markers
- `AccentForeground`: text/icons rendered on `Accent`
- `Border`: control borders, panel dividers, grid lines, DataGrid column-header dividers, separators
- `HoverBackground`: button hover, menu item hover, selector item hover, row hover, DataGrid column-header hover
- `ActiveBackground`: pressed controls, selected combo/list items, checked menu items, active tabs, pressed DataGrid column headers

Rationale:

- This is broad enough to avoid carrying forward exactly six legacy slots
- The names are still small enough for XAML resources and tests to stay maintainable
- Separating foreground, muted text, accent, hover, and active states is necessary for dense tool-style UI readability

Alternatives considered:

- A very granular token set for every control type: rejected as premature
- A tiny six-token set mirroring the old shape: rejected because it looks like a rename rather than a real theme model upgrade

### 5. Keep diagnostic and domain colors outside preset theming

The preset palette should not control frame-accuracy green/red glow, validation errors, warnings, destructive actions, or expression syntax token colors.

Rationale:

- These colors communicate meaning, not theme preference
- Presets overriding them could reduce accessibility or hide important state

### 6. Rename application resource keys to semantic names

`AvaloniaThemeApplicationService` should remain the single writer of application-level brushes, but resource keys should be renamed from legacy slot names to semantic token names. Views should bind to semantic resource keys only.

The DataGrid migration must explicitly style `DataGridColumnHeader`, including its background, foreground, divider/border, hover, pressed, and sort-glyph states. Relying only on the DataGrid Fluent base variant is insufficient because the ChapterTool semantic palette can differ from Fluent defaults. Header styles should live at the same shared scope as the other application theme mappings so every grid header follows a runtime preset change.

Rationale:

- The current centralized resource update path is sound
- Semantic resource names make XAML easier to audit
- It avoids direct per-control theme code in views or code-behind

### 7. Give presets stable ids, localized presentation, and an explicit base variant

Each preset definition should contain a stable ASCII id, semantic palette, preview swatches, and a light or dark base variant. Stable ids should use kebab-case values such as `avalonia-default`, `solarized-light`, and `ayu-mirage`; localized names belong to Avalonia resources and are resolved for the active UI language rather than persisted. `Ayu Mirage` uses the dark base variant.

`AvaloniaThemeApplicationService` should update both the semantic brush resources and `Application.RequestedThemeVariant`. The base variant keeps Fluent control templates, DataGrid, popups, and AvaloniaEdit chrome consistent with the selected palette; semantic ChapterTool brushes remain the source of application-specific colors.

Rationale:

- Display text can change with locale without destabilizing persisted settings
- An explicit base variant avoids guessing light or dark mode from color values
- Updating only application brushes would leave dark presets mixed with light Fluent chrome

### 8. Make persistence failure and fallback behavior deterministic

The replacement settings record should persist only `PresetId` in `theme-settings.json`. The legacy `theme-colors.json` file is intentionally ignored rather than deserialized or migrated. A missing settings file selects `Avalonia Default`. A syntactically valid file containing a blank or unknown preset id also resolves to `Avalonia Default` without rewriting the file until the user saves; malformed JSON continues through the existing corrupt-file preservation path.

Rationale:

- Using a new filename makes the compatibility break explicit and prevents the old object shape from partially deserializing into the new model
- Unknown ids can occur after a preset is renamed or removed and must not crash startup
- Deferring writes preserves the existing rule that live application does not itself persist settings

### 9. Remove every manual color-editing entry point

Preset-only means removing the standalone `ColorSettingsView` / `ColorSettingsViewModel` route as well as the Settings color-picker list. Commands, menu entries, localization strings, tests, and the `Avalonia.Controls.ColorPicker` package/style include that exist only for this workflow should be removed. Appearance remains available through the Settings preset selector.

Rationale:

- Leaving the separate tool reachable would contradict the preset-only requirement
- Removing the unused package reduces runtime and maintenance surface

### 10. Treat contrast as a catalog invariant

Every built-in palette should be validated as data: primary and muted foreground/background pairs and `AccentForeground` on `Accent` should meet a 4.5:1 contrast ratio; borders and state indicators against adjacent surfaces should meet 3:1. Hover and active colors must remain visually distinct from their base control surface. Dedicated diagnostic/domain styles remain outside the preset values, but must still be checked against representative light and dark surfaces.

Rationale:

- Curated source palettes do not automatically produce accessible semantic UI pairings
- Catalog-level checks catch regressions consistently across all presets
- Dense grids and compact settings controls depend on state and divider contrast

## Risks / Trade-offs

- [Risk] Existing user theme customizations are discarded by the new settings shape
  Mitigation: document the breaking change and default to a strong built-in preset.

- [Risk] Some XAML may keep referencing legacy resource names during migration
  Mitigation: migrate resource keys in one scoped pass and cover the result with build and focused headless checks.

- [Risk] A preset may work in the main window but remain inconsistent in secondary tools
  Mitigation: exercise representative light and dark presets across the main window, settings panel, and expression editor in Headless behavior tests.

- [Risk] Dark presets can reduce readability in dense grids
  Mitigation: tune `ControlBackground`, `ControlForeground`, `Border`, `HoverBackground`, and `ActiveBackground` together and verify DataGrid cells, column headers, dividers, sort glyphs, hover, and pressed states through focused behavior tests.

- [Risk] DataGrid column headers continue using Fluent default brushes while cells use the selected semantic palette
  Mitigation: add explicit shared `DataGridColumnHeader` semantic styles and verify their effective brushes while switching between representative light and dark presets.

- [Risk] A persisted preset id is no longer present in the catalog
  Mitigation: resolve blank and unknown ids to `Avalonia Default`, cover the fallback in store/catalog tests, and do not rewrite settings until Save.

- [Risk] Fluent controls use a base theme variant that disagrees with the semantic palette
  Mitigation: store an explicit light/dark base variant per preset and apply it centrally with the semantic brushes.

- [Risk] A recognizable palette family still produces low-contrast semantic pairings
  Mitigation: enforce contrast and state-distinction invariants for every catalog entry and exercise representative presets in Headless workflows.

## Migration Plan

1. Introduce the new preset-id settings model, preset catalog, explicit base variants, and semantic brush keys.
2. Store the replacement shape in `theme-settings.json`; ignore the legacy file and define default fallback for missing, blank, or unknown ids.
3. Map `Avalonia Default` to the current Fluent/Avalonia-aligned light baseline, then update `AvaloniaThemeApplicationService` and themed XAML, including explicit DataGrid column-header states, to consume semantic resources and the matching base variant.
4. Replace the appearance tab UI with a single preset selector and compact palette preview.
5. Remove the legacy Settings color list, standalone color-settings tool, obsolete localization entries, and ColorPicker dependency.
6. Add or update tests for preset catalog contents and contrast invariants, persistence and fallback, selection/live apply, reset/discard behavior, semantic brush and base-variant application, localization refresh, and preview behavior.
7. Update the affected code maps and run the focused and full automated test suites.

Rollback strategy:

- Revert the change set as a unit. This design does not support partial rollback because the storage model, ViewModel surface, and resource names change together.

## Open Questions

None.
