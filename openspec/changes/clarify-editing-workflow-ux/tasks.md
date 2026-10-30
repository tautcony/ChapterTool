## 1. Desktop close protection

- [x] 1.1 Add cancel, discard, and export close decisions. Verify rendered-window workflows retain the session on cancel and release it on discard.
- [x] 1.2 Keep failed, canceled, stale, and partial-track exports open. Verify close reentrancy and exported history-only close with lifecycle tests.

## 2. Frame controls and responsive tables

- [x] 2.1 Clarify display rounding and provide a named conversion action with visible source, target, and time effect. Verify exact-rate candidate preservation and Chinese, English, and Japanese switching.
- [x] 2.2 Adapt desktop and Web column widths for names, timestamps, and frame decimals. Verify normal, editor, and preview cells at default, wide, and narrow sizes. Preserve desktop user widths.
- [x] 2.3 Remove the standalone frame accuracy icon and persistent guidance. Show concise guidance on hover over the integer-frame control and conversion review on the desktop action tooltip. Align the bottom expression action group and verify it has no trailing gaps.
- [x] 2.4 Remove the visible frame-rate conversion button label in Desktop and Web. Preserve its tooltip, accessible name, and action behavior.

## 3. Workflow terms and shortcuts

- [x] 3.1 Distinguish reload, frame display refresh, candidate refresh, and export-text preview in shared locales and accessible labels. Verify resource parity and language switching.
- [x] 3.2 Derive visible hints from active mappings and add macOS open/save aliases with conflict validation. Verify remap, clear, fixed aliases, and editor focus through tests and browser key events.

## 4. Integration verification

- [x] 4.1 Generate Web locale JSON and run its check. Run affected test projects sequentially and browser workflows. Record intentional host differences.
- [x] 4.2 Capture default, wide, and narrow layouts in empty, normal, valid, invalid, pending, and stale states. Review screenshots and update relevant code-map pages.
- [x] 4.3 Validate the OpenSpec change strictly and reconcile every task with implementation evidence.
