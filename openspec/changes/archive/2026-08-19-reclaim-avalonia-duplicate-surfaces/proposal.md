## Why

The repository contains several proven duplicate surfaces that increase maintenance cost without adding user-visible behavior. The Avalonia main-window session exposes both concrete adapters and the narrow session contract, path normalization has forwarding-only helpers, the log tool combines projection and orchestration responsibilities, and the settings tool owns a complex edit lifecycle directly. A tracked macOS metadata file also adds noise to the published package source.

This change removes the unused metadata, makes the narrow session the only public access route, removes forwarding-only path helpers, separates log projection ownership, and centralizes settings snapshot coordination while preserving existing UI, persistence, localization, and tool behavior.

## What Changes

- Delete `packages/chaptertool/.DS_Store` and ignore `.DS_Store` files.
- Migrate Avalonia unit and Headless tests from `MainWindowViewModel.PortAdapters` to `ToolSession` and remove the compatibility property.
- Make `MainWindowToolSession` the sole owner of concrete port adapters and expose only `IWorkspaceToolSession` members.
- Remove `SettingsToolViewModel.CleanDirectory` and `MainWindowViewModel.NormalizeConfiguredDirectory`; call `ChapterSavePath.CleanOptionalPath` at the owning boundary.
- Move `LogEntryViewModel`, `LogStructuredNodeViewModel`, and their JSON/tree projection helpers into a dedicated source file without changing the log tool contract.
- Extract a settings snapshot coordinator for saved, draft, load, save, reset, discard, and lifecycle transitions. Keep Avalonia properties, commands, localization, and appearance ownership in the existing ViewModels.
- Preserve all existing user-visible behavior and persisted settings formats.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

`chapter-workspace-session`: strengthen the narrow-port boundary so secondary tools use the session contract without concrete adapter access.
`avalonia-ui-shell`: make settings snapshot lifecycle ownership explicit while preserving the existing settings workflow behavior.

## Impact

- Affected code: `src/ChapterTool.Avalonia.UI` session ports and ViewModels, Avalonia unit and Headless tests, and the npm package source directory.
- Affected repository metadata: the root `.gitignore` gains a macOS Finder metadata rule.
- No runtime dependencies, serialized settings formats, CLI contracts, or published npm files change.
- Internal callers that depend on the removed concrete `PortAdapters` properties must use `IWorkspaceToolSession` instead.
