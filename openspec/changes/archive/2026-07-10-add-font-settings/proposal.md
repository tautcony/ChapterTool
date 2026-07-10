## Why

ChapterTool currently mixes platform-default UI typography with hard-coded monospace fallback lists in expression editors, text previews, logs, and settings styles. Users need two independent font choices so the application can remain readable across languages while code-like and time-oriented content keeps predictable monospace alignment.

## What Changes

- Add two independently configurable font-family settings: a general UI font and a monospace font.
- List fonts available on the current system, provide explicit system-default choices, and show each option using a localized display label without persisting localized text.
- Apply font changes immediately while Settings is open, but persist them only when Save is invoked; Reset and Discard follow the existing settings behavior.
- Apply the UI font to normal windows, controls, menus, table headers, dialogs, and tool surfaces through a shared semantic font resource.
- Apply the monospace font to expression/script editors, chapter-table data cells, chapter/time-oriented text where fixed-width alignment is intentional, text previews, logs, and other code-like surfaces through a separate semantic font resource.
- Fall back deterministically when a saved font is blank, unavailable, or removed from the system, without rewriting settings during load.
- Add localized labels, accessible names, and representative previews for both font categories in Simplified Chinese, English, and Japanese.
- No font-size customization is introduced by this change.

## Capabilities

### New Capabilities

- `font-settings-management`: Defines independent UI and monospace font selection, system-font discovery, stable persistence, defaults, unavailable-font fallback, and live application semantics.

### Modified Capabilities

- `avalonia-ui-shell`: Extends the Appearance settings workflow and semantic application resources so normal UI surfaces and monospace content consume the independently selected fonts and refresh at runtime.

## Impact

- Infrastructure configuration gains a typed font-settings model/store and JSON source-generation coverage.
- Avalonia composition and settings-window wiring gain a font catalog/application boundary alongside the existing theme boundary.
- Application styles and code-created editor/preview controls move from implicit or hard-coded font families to semantic dynamic resources.
- Settings ViewModel state, Save/Reset/Discard behavior, localization resources, and focused Infrastructure/Avalonia/Headless tests require updates.
- `docs/code-map/infrastructure.md`, `docs/code-map/avalonia.md`, and `docs/code-map/testing.md` require updates to identify the new ownership and verification paths.
