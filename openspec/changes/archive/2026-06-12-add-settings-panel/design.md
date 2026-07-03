## Context

The Avalonia app already has typed `AppSettings` and `ThemeColorSettings` stores. Current persisted application settings include saving path, UI language, main window location, MKVToolNix/mkvextract path, eac3to path, ffprobe path, and ffmpeg path. Theme settings persist the six legacy color slots. Several workflow options are currently ViewModel state only, but most of them are also high-frequency current working values on the main screen: naming mode, template text, order shift, expression usage, expression text, frame-rate selection, and round-frames behavior.

The UI exposes some of these concerns as separate small tool windows, such as language and color settings. External tool paths are only available through settings files or legacy migration, even though they directly affect Matroska, BDMV, and ffprobe-backed media import. A settings panel should consolidate durable preferences without turning the main workflow surface into a form-heavy preferences screen or duplicating the current editing state already visible in the main window.

## Goals / Non-Goals

**Goals:**

- Add a unified Avalonia settings panel with grouped sections for general settings, external tools, output defaults, appearance, and platform integration.
- Persist only durable defaults that belong in preferences, specifically default save format and default XML language.
- Keep typed settings as the source of truth and preserve legacy migration compatibility.
- Validate configured external tool paths before saving or clearly mark invalid paths without breaking discovery fallback.
- Reuse existing localization and theme services so settings labels, statuses, and validation messages are localizable.
- Keep language and color tools either routed through the settings panel or compatible as focused entry points.

**Non-Goals:**

- Do not redesign chapter import, transform, or export logic.
- Do not remove existing typed settings stores or legacy migration support.
- Do not move high-frequency current workflow controls from the main screen into Settings.
- Do not expose Windows registry operations as primary always-visible actions.
- Do not add cloud sync, user accounts, or remote configuration.
- Do not make settings changes require app restart except for cases where runtime application is impractical.

## Decisions

1. Add a dedicated settings ViewModel and view.

   `SettingsToolViewModel` should load from `ISettingsStore<AppSettings>` and `ISettingsStore<ThemeColorSettings>`, expose editable properties, perform validation, and save all changed groups through typed stores. `SettingsToolView` should use tabs or grouped sections, not nested cards, and should fit as a secondary tool window. Alternative considered: extend `MainWindowViewModel` with every setting. That would make the main ViewModel larger and mix workflow state with preferences editing.

2. Expand `AppSettings` only for durable output defaults.

   Add optional/defaultable properties for default save format and default XML language. These are preference-like defaults that help initial state without conflicting with the current working values edited on the main screen. Alternative considered: persist every current main-window option in Settings. That would blur the boundary between durable preferences and per-session editing state and would duplicate controls users already adjust frequently in the main workflow.

3. Keep theme colors in `ThemeColorSettings`.

   The settings panel should edit the existing six slots through the existing theme store rather than merging color settings into `AppSettings`. This preserves current migration from `color-config.json` and keeps appearance settings isolated. Alternative considered: move colors into the unified app settings file. That would create unnecessary migration churn for a feature that already has a stable typed store.

4. Validate paths without disabling discovery fallback.

   External tool fields should accept executable paths or directories where the locator already supports directory expansion. The panel should offer browse/clear/test actions and display resolved status for `mkvextract`, `eac3to`, and `ffprobe`. Saving an empty value should clear the override and let PATH/platform discovery run. Invalid values should be visible before save; the implementation may allow saving with a warning only if that matches existing locator behavior.

5. Make Settings the primary preferences surface, not a duplicate of the main editing state.

   Add a `SettingsCommand` and a `"settings"` window route. Existing language/color commands can remain as shortcuts initially, but the settings panel should be the canonical place to edit persistent settings. Main-screen parameters such as expression, order shift, naming mode, template choice, frame-rate selection, and round-frames stay in the main workflow. File association must remain platform-gated and unavailable states must be explicit.

## Risks / Trade-offs

- [Risk] A large settings panel can become harder to scan than focused tools. -> Mitigation: group related settings into compact tabs or sections and keep actions close to their fields.
- [Risk] Users may expect every visible option to appear in Settings. -> Mitigation: keep Settings scoped to durable preferences and leave high-frequency current values on the main screen where they are already faster to adjust.
- [Risk] External tool validation can disagree with platform discovery rules. -> Mitigation: reuse `IExternalToolLocator` or shared path expansion rules for test/status actions.
- [Risk] Active localization and logging changes may touch the same UI resources and services. -> Mitigation: add new resource keys and keep settings persistence independent from log internals.

## Migration Plan

1. Extend `AppSettings` with optional default save format and default XML language fields whose defaults match current behavior.
2. Add tests proving old `appsettings.json` and legacy `chaptertool.json` still load when new fields are absent.
3. Create `SettingsToolViewModel` with load, save, reset, clear path, browse request hooks, and validation/status behavior.
4. Add `SettingsToolView` and route it through `AvaloniaWindowService`.
5. Add `SettingsCommand` to `MainWindowViewModel` and expose it from the main shell without making registry-only actions primary.
6. Apply saved default save format and default XML language during ViewModel settings load and persist settings changes through typed stores.
7. Add focused ViewModel, settings-store, and compiled XAML/build tests.
