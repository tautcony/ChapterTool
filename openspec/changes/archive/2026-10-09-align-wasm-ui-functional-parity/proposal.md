## Why

The Web and Avalonia hosts share the editing kernel, but their controls do not provide the same editing behavior. Web settings can reapply naming or numbering operations, while several desktop actions are missing or commit without review.

## What Changes

- Make Web naming, numbering, frame-shift, and frame-rate operations use draft, preview, apply, and cancel states.
- Prevent settings and display changes from committing chapter content or reapplying operation drafts.
- Add Web frame-cell editing and align signed frame-shift limits.
- Align numbering controls with the existing nonnegative Core policy. Remove ineffective negative input from Avalonia.
- Add Web Lua file loading, syntax highlighting, completion, and positioned authoring diagnostics.
- Add configurable Web shortcuts, row commands, and previous/next clip navigation.
- Add settings draft close protection and preserve valid settings when validation or persistence fails.
- Add Web log search, severity filters, explicit details, copy, and JSON/CSV export.
- Align localized option labels, command availability, keyboard workflows, and responsive acceptance checks.
- Verify equivalent committed content, history, and exports through both hosts.

## Capabilities

### New Capabilities

- `wasm-content-operation-parity`: Reviewed content operations, preference isolation, signed shifts, and shared numbering policy.
- `wasm-grid-expression-authoring`: Frame-cell editing, table edit lifecycle, Lua file loading, and expression editor assistance.
- `wasm-settings-shortcuts`: Browser shortcut configuration, command routing, settings validation, persistence compatibility, and close protection.
- `wasm-log-tool-parity`: Browser log filtering, inspection, copy, export, localization, and responsive interaction.

### Modified Capabilities

None. The new browser contracts extend existing shared behavior. They do not replace the desktop settings store, shortcut parser, or log provider contracts.

## Impact

- Web `Home.razor`, dialog components, workspace services, settings models, styling, and existing JavaScript adapters.
- Core candidate APIs where an existing operation needs a reusable entry point. Core remains the owner of edit semantics.
- Host-neutral shortcut catalog and gesture validation surfaces in Contracts.
- Avalonia numbering control and targeted parity regressions.
- Derived frame displays in both hosts. Display preferences must not create content transactions. Frame edits, shifts, and expression candidates must use the same effective FPS.
- Core frame conversion must quantize document and segment boundaries through the same frame calculation. It must preserve source metadata and known duration invariants.
- Shared AXAML locale resources and generated Web JSON resources.
- Core, Web, Avalonia unit, Avalonia Headless, and browser E2E tests.
- Relevant capability, ownership, and testing code-map pages.

## Non-goals

- Native media import, external tools, BDMV directory access, local save directories, or local file overwrite.
- Desktop shell operations, related-media access, system font enumeration, or telemetry.
- Changes to the browser download bridge, export-success acknowledgement, or page-leave handling.
- Replacing Blazor with Avalonia, copying desktop window layouts, or adding durable document history.
- Changing Lua syntax, export formats, or the Core numbering policy to support negative offsets.
- Archiving or rewriting completed OpenSpec changes.
