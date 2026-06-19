## Why

The unified settings panel currently behaves like a deferred editor for most preferences: changes are only applied when Save is clicked. This is inconsistent with the existing color settings behavior and makes settings such as language, output defaults, save directory, tool paths, and frame tolerance feel stale until persistence happens.

## What Changes

- Apply editable settings to the running application immediately as the user changes them.
- Keep persistence explicit: Save writes the current applied settings to the typed stores, while unsaved live changes remain transient.
- Track dirty settings after live changes and show a localized close confirmation when the settings window is closed with unsaved changes.
- Restore previously saved settings when the user discards unsaved live changes by closing the settings window without saving.
- Preserve existing validation, browse, reset, and color appearance behavior.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `avalonia-ui-shell`: Settings changes apply immediately at runtime, are persisted only on Save, and closing with unsaved changes requires confirmation.

## Impact

- `SettingsToolViewModel` will need live-apply state tracking, saved-state snapshots, and discard behavior for application and theme settings.
- `AvaloniaWindowService` will need close interception for the settings window and a localized confirmation path.
- Localization resources will need prompt/status strings for unsaved settings confirmation.
- ViewModel and headless tests will cover live apply, save-only persistence, discard, and close-confirm behavior.
