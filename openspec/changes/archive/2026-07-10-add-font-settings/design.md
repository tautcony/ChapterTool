## Context

Typography is currently split between implicit Fluent/platform defaults and local hard-coded values. Normal controls inherit whatever font Avalonia chooses, while `ExpressionEditor`, `TextToolView`, settings preview text, and related code-like surfaces embed fallback strings such as `Menlo, Consolas, monospace`. There is no typed font settings model, no shared font resource, and no application service that can refresh existing views.

The existing settings workflow already establishes the behavior this change must preserve: values load asynchronously, runtime-safe changes apply immediately, Save is the only persistence action, Reset applies defaults, and Discard restores the last loaded or saved snapshot. Theme settings also provide a useful boundary for a small appearance-specific store and application service. The implementation must remain cross-platform, keep Chinese/Japanese text readable, avoid introducing a font package, and support deterministic tests without depending on the fonts installed on the test machine.

## Goals / Non-Goals

**Goals:**

- Represent UI and monospace font families as independent typed settings.
- Let users select from fonts available to Avalonia on the current system, with explicit defaults for both categories.
- Apply both choices immediately to existing and newly opened views through semantic application resources.
- Persist only canonical font-family identifiers, never localized labels or serialized Avalonia objects.
- Preserve current Save, Reset, Discard, dirty-state, corrupt-file, and no-write-on-load behavior.
- Make unavailable-font handling deterministic and testable across Windows, macOS, and Linux.
- Cover normal UI, popup/tool surfaces, expression editors, text previews, logs, and other intentionally fixed-width content without changing icon fonts.

**Non-Goals:**

- Font size, weight, style, line-height, letter-spacing, per-window, or per-control customization.
- Downloading, embedding, installing, or managing font files.
- Automatically classifying installed fonts as monospace.
- Migrating unrelated application or theme settings.
- Changing syntax colors, theme palettes, or localization fallback behavior.

## Decisions

### 1. Store font preferences in a dedicated typed settings file

Add a small Infrastructure record equivalent to `FontSettings(UiFontFamily, MonospaceFontFamily)` and a `FontSettingsStore` backed by `font-settings.json`. Empty values represent the category defaults. The store trims values, preserves malformed files through the existing corrupt-settings mechanism, writes atomically, and does not need a dependency on Avalonia.

This keeps appearance-specific lifecycle and failures separate from `AppSettings`, mirrors the existing `ThemeSettingsStore`, and avoids making CLI/general configuration consumers aware of UI typography. Adding the fields to `AppSettings` was considered, but would couple an Avalonia-only concern to the broad application settings contract and make independent fallback/application behavior less clear.

### 2. Use semantic dynamic font resources as the runtime contract

Define application resources named `ChapterTool.UiFontFamily` and `ChapterTool.MonospaceFontFamily`. `AvaloniaFontApplicationService` resolves effective settings and replaces both resource values with `FontFamily` instances on the UI thread.

Normal window and text-bearing control styles consume `ChapterTool.UiFontFamily`. DataGrid column headers remain normal UI, while `DataGridCell` applies `ChapterTool.MonospaceFontFamily` so displayed and edited chapter-row values align without changing header typography. The chapter-number shift `NumericUpDown` also uses the monospace resource for aligned numeric entry while its label remains UI typography. Other explicit monospace styles and code-created editor/preview controls consume the same monospace resource, overriding inherited UI typography. Consumers use dynamic resource references so already-open views refresh without reconstruction. Icon controls keep their icon library font and are not assigned either semantic family directly.

Passing font names through every ViewModel and control constructor was rejected because it spreads presentation state across unrelated workflows and cannot update all existing views consistently. Replacing the global Fluent theme was also rejected because typography is application state, not a theme package.

### 3. Isolate platform font discovery behind a catalog interface

Introduce an `IFontFamilyCatalog` abstraction owned by the Avalonia layer. Its production implementation snapshots `FontManager.Current.SystemFonts`, removes blank and case-insensitive duplicate family names, and sorts them for display using the current UI culture. Tests inject a fixed catalog.

Both selectors use the same installed-family catalog. The UI selector prepends a localized "System default" option; the monospace selector prepends a localized "System monospace default" option. Installed family names are canonical selectable values and may render in their own family in the drop-down and preview.

Each installed-family item renders its label with that family, but the selector keeps a virtualizing items panel so font-specific text shaping occurs only for realized viewport items. This provides progressive rendering while scrolling without asynchronous mutation of the option collection or eager preview generation for every installed font.

Font metadata may expose localized family names. Each catalog entry keeps its canonical family name for identity and persistence, then lazily resolves a display name when the virtualized item is realized: exact active culture first, another name for the same language second, and canonical name last. Runtime language changes rebuild display wrappers for the new culture without changing either canonical selection.

The catalog will not attempt to filter the monospace selector. Avalonia does not expose reliable cross-platform fixed-pitch metadata, and name-based heuristics would hide valid CJK monospace fonts or admit proportional fonts. A representative preview containing Latin letters, digits, punctuation, and localized text lets the user assess the choice directly.

### 4. Resolve defaults and unavailable fonts without mutating storage

An empty UI choice resolves to `FontFamily.Default`. An empty monospace choice resolves to the current cross-platform fallback chain, retaining generic `monospace` as the final fallback. A non-empty saved family is effective only when the catalog contains it; otherwise that category independently resolves to its default.

Settings load keeps the persisted file untouched. The ViewModel stores the normalized effective snapshot so an unavailable saved name does not make the settings page dirty immediately. If the user later invokes Save, the visible effective choices are persisted. This matches the theme preset fallback contract while ensuring one unavailable category does not reset the other.

Persisting an index was rejected because installed-font ordering differs by machine and locale. Persisting a fallback list was rejected because it obscures the user's selection and makes availability behavior difficult to explain.

### 5. Extend the existing Settings snapshot lifecycle

`SettingsToolViewModel` receives `ISettingsStore<FontSettings>`, `IFontFamilyCatalog`, and `IFontApplicationService` alongside the existing app/theme dependencies. It exposes immutable font options plus selected UI/monospace values and preview metadata.

Selection changes call the font application service only when live apply is enabled. Save writes the current font settings and advances the saved snapshot. Reset selects both defaults and applies them without writing. Discard restores and reapplies the saved snapshot. `HasUnsavedChanges`, load-failure status, close confirmation, and Save command availability include the font store using the same rules as theme settings.

The Appearance tab gains two labeled selectors and two stable-size, non-editable previews below the existing theme preset controls. Labels, default-option names, preview accessible names, and sample text come from Simplified Chinese, English, and Japanese resources. A runtime language change rebuilds display labels without changing canonical selected family names.

### 6. Apply defaults synchronously and persisted settings asynchronously at startup

`AppCompositionRoot` constructs the catalog, store, and application service. It applies `FontSettings.Default` before the main window is created, then loads and applies persisted settings asynchronously using the same non-blocking startup pattern as theme settings. I/O, access, and corrupt-file failures retain defaults and do not prevent the main window from opening.

The same service instances flow into `AvaloniaWindowService` and every Settings window. This prevents a settings preview from diverging from startup application behavior.

### 7. Verify behavior at store, service, ViewModel, and rendered-workflow boundaries

Infrastructure tests cover missing files, round trips, trimming/default values, malformed-file preservation, and no write during load. Application-service tests use a fixed catalog to verify both semantic resources, independent fallback, and replacement of previously applied values. ViewModel tests cover option ordering, independent selection, live apply, Save, Reset, Discard, dirty state, unavailable values, and language refresh.

Focused Avalonia Headless tests drive both selectors and verify that an existing normal text surface and existing expression/text-preview surface resolve different effective font families, that live changes refresh them, and that Save/Discard outcomes match visible state. Tests assert behavior and effective resources rather than reading XAML or source files as text.

## Risks / Trade-offs

- [Some fonts lack glyphs for the active language] -> Keep platform fallback enabled, show localized mixed-script previews, and always provide system-default choices.
- [A font disappears after settings are saved] -> Resolve each category independently against the current catalog and fall back without rewriting the file during load.
- [Global UI font styling overrides icon-library glyphs] -> Apply the UI resource through inheritance/text-bearing styles and keep icon controls on their explicit icon family; add a Headless workflow assertion that settings command icons remain present after a font switch.
- [Dynamic resources do not reach code-created AvaloniaEdit controls] -> Assign a dynamic resource reference when each editor is constructed and test an already-created editor across a runtime change.
- [Enumerating fonts is relatively expensive or returns duplicates] -> Snapshot and deduplicate once per application catalog instance; rebuild only localized wrapper labels, not the system enumeration.
- [A user chooses a proportional font for monospace content] -> Do not impose unreliable filtering; make the category intent clear and provide an alignment-oriented preview.
- [Font metrics alter compact layouts] -> Preserve existing responsive constraints, trimming, and minimum widths; use Headless behavior checks plus manual default/wide/narrow screenshots during implementation.

## Migration Plan

1. Introduce the model, store, catalog abstraction, application service, default resources, and tests while current hard-coded consumers remain unchanged.
2. Wire startup loading and Settings snapshot behavior, then add the two selectors and localized previews.
3. Move normal and monospace consumers to semantic resources, covering code-created controls explicitly.
4. Run focused Infrastructure and Avalonia tests, build the app, then run the full solution tests and manual layout checks at default, wide, and narrow sizes.
5. Update the relevant code maps after ownership and verification paths stabilize.

No migration of existing files is required because `font-settings.json` is new and absence means defaults. Rollback consists of removing the font resource/application wiring; the standalone settings file can remain ignored without affecting older builds.

## Open Questions

None. The first release intentionally fixes the scope at font-family selection only and uses previews rather than attempting platform-dependent monospace classification.
