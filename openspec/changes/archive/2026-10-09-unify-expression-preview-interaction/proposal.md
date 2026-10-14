## Why

Avalonia and Web expose expression candidates as flat technical differences. Users cannot readily compare chapter times or distinguish time edits from frame-information updates. The user selected design A in [the interaction design](../../../docs/tasks/expression-preview-interaction-design.md): an independent expression surface with editing and chapter-level before/after review in one view.

## What Changes

- Replace flat difference text with a structured, read-only chapter comparison in both Avalonia and Blazor WebAssembly.
- Show the current-track scope, complete change counts, time differences, frame-information changes, and document properties with localized labels.
- Use a multiline editor above the preview. Reflow chapter comparisons into vertical entries on narrow screens.
- Keep Cancel and Apply changes visible. Disable Apply for empty drafts, pending computation, invalid results, unchanged candidates, stale candidates, and active submissions.
- Commit the exact candidate that the user reviewed. Do not recalculate and commit a replacement in the same confirmation action.
- Align close, cancellation, focus restoration, success feedback, and one-step undo behavior across both hosts.
- Add behavior and layout coverage for both hosts, including the supplied screenshot's frame-only changes.

## Capabilities

### New Capabilities

- `expression-preview-interaction`: Shared expression review, presentation, state, and confirmation requirements for Avalonia and Web.

### Modified Capabilities

None. This change adds a shared interaction contract over the existing candidate and transaction capabilities. It does not replace the Lua language or document model.

## Impact

- Avalonia expression tool view, ViewModel, narrow session ports, and tool-host lifecycle.
- Web expression dialog, workspace presentation, modal styling, and focus lifecycle.
- Shared host-neutral preview presentation and existing immutable candidate snapshots.
- Shared locale AXAML files and generated Web locale JSON.
- Core/session verification, Avalonia unit and Headless coverage, Web workspace tests, and browser E2E coverage.
- Relevant code-map pages and design-document status.

The completed changes `live-expression-preview` and `fix-wasm-modal-layout` remain unarchived. This change builds on their implemented behavior and records that dependency in its design. It does not archive or rewrite those changes. No new runtime package, expression syntax, history storage, or scope selector is required.
