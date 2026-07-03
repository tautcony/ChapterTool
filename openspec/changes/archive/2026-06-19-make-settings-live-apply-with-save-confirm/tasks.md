## 1. Settings ViewModel Runtime State

- [x] 1.1 Add saved-settings snapshots and dirty-state properties to `SettingsToolViewModel`.
- [x] 1.2 Apply runtime-safe app settings immediately when editable settings change, without writing typed stores.
- [x] 1.3 Keep Save as the only durable write path and refresh snapshots after successful save.
- [x] 1.4 Restore the last saved app and theme settings when unsaved changes are discarded.

## 2. Settings Window Lifecycle

- [x] 2.1 Add localized unsaved-settings prompt resources for supported cultures.
- [x] 2.2 Intercept settings window close in `AvaloniaWindowService` and call save/discard/cancel behavior based on confirmation.
- [x] 2.3 Ensure closing after Save or without changes does not show the confirmation prompt.

## 3. Tests and Verification

- [x] 3.1 Add ViewModel tests for live apply, save-only persistence, discard restore, and dirty state.
- [x] 3.2 Add window-service/headless coverage for close confirmation and cancel/discard behavior.
- [x] 3.3 Run focused Avalonia tests, build the app, and validate the OpenSpec change strictly.
