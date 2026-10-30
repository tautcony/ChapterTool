## Why

Closing the desktop window can discard unexported edits without a warning. Frame controls, narrow tables, and shortcut hints do not clearly explain the available actions.

## What Changes

- Protect desktop close when content has not been exported or a candidate is pending. Offer export and close, discard, and cancel. Preserve session-only history.
- Label frame display separately from chapter frame-rate conversion. Show the source rate, target rate, and effect before applying conversion.
- Give chapter names useful space in narrow windows. Keep time and frame values intact in normal, edit, and preview states. Preserve user column widths.
- Use distinct localized names for source reload, frame display refresh, candidate refresh, and export-text preview. Build shortcut hints from the active mapping. Add macOS open and save aliases.
- Cover shared controls and terms in desktop and Web hosts. Preserve exact timing and candidate application rules.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `document-session-lifecycle`: Protect desktop close without treating history alone as unexported work.
- `avalonia-ui-shell`: Clarify frame actions and improve narrow chapter-table sizing in both hosts.
- `keyboard-shortcut-customization`: Keep visible hints synchronized with active mappings and platform aliases.

## Impact

Desktop window lifecycle, shared main view, frame-conversion review, Web main view, localization resources, shortcut catalog, Headless tests, browser tests, and code-map documentation are affected. No dependency upgrade or history persistence is required. The separate preview/export redesign and new editing toolbar are outside this change.
