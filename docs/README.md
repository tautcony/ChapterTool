# ChapterTool Documentation

This directory contains documentation for the current ChapterTool codebase.

## Current Documentation

- `code-map/` is the primary maintainer navigation index. Start here when you need to locate code, ownership, entry points, or primary tests.
- `testing/` contains current testing guidance and performance notes.
- `code-map/ui-logging.md` maps shared Avalonia UI logging scenarios and log outputs.
- `tasks/ui-visual-consistency.md` provides the current workflow for investigating Avalonia visual inconsistencies.
- `tasks/unified-editing-and-unbounded-history.md` defines one document editing model and in-memory session history without an operation-count limit.
- `tasks/wasm-modal-layout-and-regression-plan.md` analyzes browser layout failures and defines modal workflows and layout regression checks.
- `testing/headless-performance.md` records the Headless UI lifecycle diagnosis and triage steps.
- `testing/wasm-browser-e2e-plan.md` records the Playwright browser test design, execution commands, CI gates, and acceptance criteria for the Blazor WebAssembly app.
- `testing/wasm-modal-layout-acceptance.md` records the browser dialog implementation and layout regression evidence.

## Historical Documentation

- `archive/code-review/` contains dated code reviews and their remediation records.
- `archive/migrations/` contains completed migration plans, parity records, and implementation reports.

Historical documents provide context only. They do not define current behavior or current work items. Read them only when a task explicitly asks for historical context, review evidence, or migration history.

When current documentation conflicts with an archived document, follow the current code, OpenSpec specifications, and `docs/code-map/`.
