## Context

Home.razor places history and expression diagnostics inside a three-row main grid. Browser probes show that the chapter grid covers the history close button at 390×640 and 844×390. Existing Playwright tests cover workflows and idle screenshots. They do not assert modal focus, hit targets, or these occupied layout states.

## Goals / Non-Goals

Goals are independent tool dialogs, responsive editing zones, isolated drafts, and automated browser regression gates. Preserve committed chapter data, candidate validation, history branches, export bytes, localization, and settings persistence.

This change does not replace Playwright, change the core history model, or redesign the desktop UI. Browser emulation does not establish physical iPhone keyboard behavior.

## Decisions

- Use a shared native dialog component with showModal, cancel handling, scrollable body, and focus restoration. Native top-layer placement prevents the chapter grid from covering controls. Reuse it for existing tools to avoid divergent modal behavior.
- Use one active-dialog enum in Home. Close context menus on entry. Block background shortcuts and file drops while a dialog is active.
- Put history, expression editing, and advanced export options outside the main grid. Keep format and naming mode in compact bottom controls. Give the grid its own scrolling region. Let short pages scroll when toolbar and bottom controls need more space.
- Keep expression draft and debounce ownership in a focused component. Cancel cancels pending work and restores the previous expression and preset. Apply commits one valid candidate and closes the dialog. History navigation remains immediate.
- Stage advanced export values and template bytes until Apply. Cancel must not update settings or chapter projections. Apply uses the existing persistence and projection paths.
- Extend default Playwright tests with geometry, focus, actual clicks, and state assertions. Keep pixel snapshots in a separate fixed-Linux visual suite. Add dialog states to that suite and run visual and WebKit regression gates on PRs.

## Risks / Trade-offs

- Native dialogs require event cleanup and focus handling. Cover reopen, Escape from inputs, and removed components in browser tests.
- Virtualized history can lose scroll after a render. Retain the dialog while navigating and verify branch navigation.
- Moving inputs changes browser interaction paths. Update all affected workflow tests and accessible labels together.
- Pixel snapshots vary across hosts. Generate and review them in the existing pinned Linux environment.
- Physical mobile browser chrome and keyboard behavior require a separate device check. Record this limitation in the validation report.

## Migration Plan

Add failing browser regressions. Implement the shared lifecycle and move the tools. Update translations and generate JSON. Run WASM unit tests, all browser engines, and fixed-Linux visual checks. Update ownership and validation documentation. No data migration is required. Rollback consists of reverting this change.

## Open Questions

None for implementation. Physical iPhone validation remains a documented follow-up when a device is available.
