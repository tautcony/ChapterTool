## Why

Frame values derived from timestamps can lose precision when code converts tick counts to decimal seconds and multiplies by a decimal frame rate. The resulting long expansions also make chapter rows and before/after comparisons difficult to scan. The user requested exact rational calculation where rate provenance is available, compact mathematical presentation, and a Settings preference.

## What Changes

- Add a Settings-only checkbox named “显示小数循环节”. Enable it by default.
- Calculate frame values from timestamp ticks and authoritative rational frame rates through the calculation and comparison path. Keep approximate rate inputs on the ordinary numeric path.
- Render an exact recurring frame value as an integer part, a non-repeating prefix, and one overlined repetend.
- Apply the same presentation policy to committed, before, and candidate frame values.
- Keep one value for an unchanged frame cell. Keep the existing before → after comparison for a changed cell.
- Align decimal points independently on each side. Keep the arrow at a stable position.
- Provide integer rounding, fixed decimal places, and no-truncation modes in Settings. Keep the main rounding control synchronized with the selected mode. Turning rounding off from integer mode selects no truncation.
- Preserve plain numeric editing, full-value inspection, semantic colors, calculation, candidate equality, history, and export.
- Store the preference in the existing application settings section. Do not add a display selector, toggle, or new preview control to the main window or chapter grid.

## Capabilities

### New Capabilities

- `compact-frame-decimal-display`: Exact recurring-decimal presentation, Settings-only control, consistent before/after formatting, aligned layout, and plain numeric editing across Avalonia and Web.

### Modified Capabilities

- `versioned-settings-document`: Persist an optional repeating-decimal preference with a default for existing settings documents.

## Impact

- Shared presentation logic uses typed chapter timestamps and authoritative frame-rate metadata.
- Application settings contracts, Avalonia settings bindings, and Web settings drafts carry the new preference.
- Avalonia chapter-cell templates and Web chapter rendering consume structured number parts.
- Fixed decimal places and no-truncation remain explicit frame display modes. The repeating-cycle setting applies only to no-truncation mode.
- Shared locale resources provide the setting label and description. Generated Web locale JSON follows the existing generation workflow.
- Existing Core, Contracts, Infrastructure, Avalonia, Headless, and Web verification paths cover the affected behavior.
- No new package, settings schema version, CLI option, document field, or export format is required.
