## Why

The current appearance settings expose only six raw color slots, which keeps the theme model too low-level and prevents the app from offering a coherent visual identity. The settings panel needs to move to a preset-first theme system with recognizable curated palettes instead of preserving the legacy slot-editing workflow.

## What Changes

- **BREAKING** Replace the legacy six-slot appearance workflow with a preset-first theme selector centered on curated theme families.
- Add an `Avalonia Default` preset plus built-in presets from the `Solarized`, `Gruvbox`, and `Ayu` families, including their common light, dark, and intermediate variants.
- Redefine the theme data model around semantic UI surfaces and interaction states rather than preserving the current slot-oriented legacy shape.
- Define a stable color responsibility map so each semantic theme token controls a predictable group of components and interaction states.
- Keep Avalonia's Fluent control theme variant synchronized with each preset so dark palettes do not retain light control chrome.
- Keep the first release focused on preset selection and preview; manual theme editing and `Custom` themes are out of scope.
- Remove the standalone legacy color-settings tool and its ColorPicker dependency so manual color editing is not reachable outside Settings.

## Capabilities

### New Capabilities

- `theme-preset-management`: Provide curated built-in theme families, active preset selection, preview metadata, and semantic theme token definitions for the settings panel.

### Modified Capabilities

- `avalonia-ui-shell`: Replace the current appearance settings workflow with preset-only semantic theme selection and documented surface coverage.

## Impact

- Affected specs: `openspec/specs/avalonia-ui-shell/spec.md` and a new `openspec/specs/theme-preset-management/spec.md`
- Affected code: settings ViewModel and appearance tab UI, legacy color-settings tool, theme application service resource mapping, theme settings persistence model, app resources and dependencies, localized strings, code maps, and related Infrastructure/Avalonia tests
- Existing persisted theme settings are out of scope for compatibility and may be replaced by the new format
- No external dependencies are required; the work stays within the existing Avalonia and Infrastructure settings stack
