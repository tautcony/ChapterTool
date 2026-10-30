## Context

See proposal.md for the selected UX issues. MainView owns shared layout. The desktop Window owns lifetime. Home.razor owns Web controls. ShortcutMapping already resolves saved overrides. Locales are shared AXAML sources.

## Goals / Non-Goals

**Goals:** Preserve content and candidate rules while making close decisions, frame actions, and table widths clear. Reuse existing services and mappings.

**Non-Goals:** Do not add persistent history, autosave, a new editing toolbar, or automatic candidate application during export. Do not upgrade dependencies.

## Decisions

1. Intercept desktop Closing before session teardown. Use one asynchronous decision workflow. Offer export and close, discard and close, and cancel. Export uses the existing command. Recheck document identity and export state before retrying Close. Keep teardown in Closed. A history-only session does not trigger the new guard. Window-close cancellation remains valid.
2. Reuse exact frame-rate values for conversion guidance. Show an icon-only conversion button with its action name in the tooltip and accessible name. Show source rate, target rate, and time effect in the desktop conversion button tooltip while its candidate is pending. Keep frame accuracy guidance on hover over the integer-frame control. Remove the separate information icon and persistent explanatory text. Keep display rounding distinct. Do not create another candidate model.
3. Size normal time cells for one timestamp and preview cells for their actual comparison layout. Reserve useful name space. Track user column resizing so responsive layout does not overwrite it. Web uses the same readability contract with its existing responsive table.
4. Keep action identities stable. Rename localized labels only. Generate action hints from ShortcutMapping, including aliases. Add Meta+O and Meta+S as fixed aliases. Avoid hard-coded shortcut strings in labels.
5. Use Chinese, English, and Japanese locale sources. Generate Web JSON once after integration. Agents must request shared-file changes through the integrator when ownership overlaps.
6. Run test projects sequentially. Verify desktop close branches, exact candidate preservation, localized widths, shortcut remapping, and browser real-key events. Capture empty, normal, valid, invalid, pending, and stale layouts at default, wide, and narrow sizes where applicable.
7. Put the desktop expression actions in one right-aligned group. Hidden actions MUST NOT leave trailing gaps. Align visible actions vertically with the expression editor.

## Risks / Trade-offs

- Asynchronous close can race with another close or edit. Use one active workflow and recheck state after export.
- A single-track export can leave other tracks unexported. Keep the window open and explain the remaining content.
- Longer translated frame labels can crowd narrow layouts. Use responsive label/control rows and verify all supported languages.
- Browser unload cannot await a custom export dialog. This change applies close decisions to desktop. Browser unload remains non-recoverable.

## Migration Plan

No stored document or settings migration is required. Preserve action IDs and saved gesture overrides. Revert the UI changes to roll back.
