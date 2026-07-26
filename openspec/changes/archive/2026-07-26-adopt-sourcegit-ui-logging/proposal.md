## Why

ChapterTool currently copies a small set of SourceGit visual ideas into local auxiliary-window styles. This partial copy does not provide the coherent control templates, theme tokens, and icon catalog that define the SourceGit user interface.

The application needs one complete, attributed SourceGit user interface foundation. The log window must use that foundation instead of a separate approximation.

## What Changes

- Import the complete SourceGit icon dictionary.
- Import the complete SourceGit light and dark theme token dictionaries.
- Port all reusable SourceGit control styles to Avalonia 12.1.
- Replace SourceGit application-state bindings with ChapterTool resource tokens.
- Exclude only selectors that require SourceGit Git-domain ViewModels or controls.
- Load the ported resource layer for the complete ChapterTool desktop application.
- Preserve the SourceGit MIT license and add a precise source and adaptation notice.
- Replace the current log panel with a SourceGit-style master-detail log tool.
- Apply the SourceGit form-control and action-bar patterns to the settings window.
- Keep structured log history, live updates, localization, filtering, copying, clearing, and bounded retention.
- Add compiled, behavior, resource, and Headless tests.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `avalonia-ui-shell`: Use the ported SourceGit user interface resource layer for desktop windows and controls.
- `theme-preset-management`: Map ChapterTool presets to the complete SourceGit theme token set.
- `supporting-ui-platform-services`: Present structured application logs in a SourceGit-style master-detail window.

## Impact

- Avalonia application resources, styles, icons, views, ViewModels, composition, and localization.
- Theme application code and resource tests.
- Avalonia unit tests and Headless user-interface tests.
- Third-party notices and the Avalonia code map.
- No Core API, command-line interface, browser host, or settings document schema changes.
