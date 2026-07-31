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
- Preset-only behavior produces clearer specs and screenshots

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
- `ControlBackground`: text boxes, combo boxes, grid cells, expression editor base, editable input surfaces
- `ControlForeground`: primary text, icons, grid text, labels, button content
- `MutedForeground`: helper text, status details, placeholder-like secondary text
- `Accent`: focused controls, selected tabs, primary action emphasis, active selection markers
- `AccentForeground`: text/icons rendered on `Accent`
- `Border`: control borders, panel dividers, grid lines, separators
- `HoverBackground`: button hover, menu item hover, selector item hover, row hover
- `ActiveBackground`: pressed controls, selected combo/list items, checked menu items, active tabs

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

Rationale:

- The current centralized resource update path is sound
- Semantic resource names make XAML easier to audit
- It avoids direct per-control theme code in views or code-behind

## Risks / Trade-offs

- [Risk] Existing user theme customizations are discarded by the new settings shape
  Mitigation: document the breaking change and default to a strong built-in preset.

- [Risk] Some XAML may keep referencing legacy resource names during migration
  Mitigation: migrate resource keys in one scoped pass and cover the result with build and focused headless checks.

- [Risk] A preset may look acceptable in the main window but weak in secondary tools
  Mitigation: verify representative light and dark presets against the main window, settings panel, and expression editor surfaces.

- [Risk] Dark presets can reduce readability in dense grids
  Mitigation: tune `ControlBackground`, `ControlForeground`, `Border`, `HoverBackground`, and `ActiveBackground` together, then capture screenshots at default, wide, and narrow sizes.

## Migration Plan

1. Introduce the new theme settings model, preset catalog, and semantic brush keys.
2. Map `Avalonia Default` to the current Fluent/Avalonia-aligned light baseline, then update `AvaloniaThemeApplicationService` and themed XAML to consume semantic resources.
3. Replace the appearance tab UI with a single preset selector and compact palette preview.
4. Remove legacy six-slot settings labels, ViewModel color slot collections, and tests that assert color-picker count.
5. Add or update tests for preset catalog contents, selection persistence, live apply, reset/discard behavior, semantic brush application, and appearance-tab rendering.
6. Capture screenshots for representative light and dark presets at default, wide, and narrow sizes.

Rollback strategy:

- Revert the change set as a unit. This design does not support partial rollback because the storage model, ViewModel surface, and resource names change together.

## Open Questions

None.
