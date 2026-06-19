## Context

The current `SettingsToolViewModel` loads editable preferences from typed settings stores and applies them to the running `MainWindowViewModel` only when Save is executed. Appearance colors are already closer to the desired behavior: color slot changes call the theme application service immediately, while persistence remains tied to Save.

The new behavior should make the entire settings panel follow that model. Runtime-safe settings must update the current shell as users edit them, but typed settings files must only be written when Save is clicked. If a user closes the settings window after live changes without saving, the app must warn and restore the last persisted values if the user discards changes.

## Goals / Non-Goals

**Goals:**

- Apply settings panel edits immediately to the running `MainWindowViewModel` and theme application service.
- Keep durable persistence explicit through the existing Save command.
- Track whether the applied runtime state differs from the last loaded or saved settings.
- Intercept settings window close and show a localized unsaved-changes confirmation.
- Restore the saved settings snapshot when unsaved changes are discarded.
- Preserve existing validation and path browsing behavior.

**Non-Goals:**

- Introducing autosave.
- Changing the external settings file format.
- Making external tool discovery itself persistent before Save.
- Reworking all auxiliary windows or replacing the existing window service architecture.

## Decisions

1. Use snapshots for saved settings state.

   `SettingsToolViewModel` should keep normalized `AppSettings` and `ThemeColorSettings` snapshots representing the last loaded or saved durable state. Dirty state is computed from current ViewModel values against these snapshots. This makes Save, Reset, and Discard behavior explicit and avoids relying on individual flags that can drift.

2. Add live apply inside the settings ViewModel.

   Property setters for settings that affect runtime behavior should call a shared `ApplyCurrentSettingsToOwner` method after initialization. This keeps application-settings logic near the data it manages and matches existing immediate theme application for color slots. The method should update owner language, save directory, default output format/language, and frame tolerance without writing stores.

3. Save updates the snapshot after writing stores.

   Save should persist the currently applied values to app and theme stores, then refresh the saved snapshots and clear dirty state. This preserves the current Save command semantics while making it clear that Save no longer performs the first runtime application.

4. Close confirmation belongs in the window service.

   The window service owns secondary window lifecycle, so it should handle settings-window close interception. The ViewModel should expose testable operations such as `HasUnsavedChanges`, `SaveAsync`, and `DiscardUnsavedChanges`; the service should decide whether to cancel close based on a dialog result.

5. Discard restores runtime state before allowing close.

   If the user chooses to discard unsaved changes, the settings ViewModel should reapply the saved app settings to the owner and saved theme settings to the theme service, then refresh editable fields from the snapshots. This prevents transient live settings from leaking after the settings panel is closed.

## Risks / Trade-offs

- Live language changes can update labels while the settings window is open → Use the existing localization manager and culture-change handling rather than adding a separate refresh path.
- Property setters may apply settings during initial load → Gate live apply until the initial load has completed, and apply once after loading normalized snapshots.
- Close confirmation could recurse if closing is retried programmatically → Track a per-window confirmed-close state or remove the handler path by marking the close as accepted after Save/Discard.
- External tool path edits affect locators only after persistence today → Runtime owner state can update immediately, while external locator precedence remains durable on Save because locators read settings stores.
