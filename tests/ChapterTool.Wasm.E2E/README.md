# ChapterTool WASM browser tests

This project tests the Blazor page in real browsers. It uses Playwright Test and
TypeScript. It is separate from the .NET solution and `packages/chaptertool`.

## Prerequisites

- Node.js 22.x
- .NET 10 SDK
- Python 3.9+
- Chromium and WebKit for the pull request suite; Chromium, Firefox, and WebKit for full
  acceptance

## Install and run

Run from the repository root:

```bash
python scripts/check-ci.py --stage browser --browser-suite chromium
```

The default Playwright configuration serves the prepared Release output at
`http://127.0.0.1:5261/ChapterTool/`. The preparation script copies the
published `wwwroot` into `artifacts/wasm-e2e/site/ChapterTool/`, rewrites both
HTML entry points, and creates the Pages metadata files.

Run `npm --prefix tests/ChapterTool.Wasm.E2E run test:e2e:dev` to use the local
Blazor development server at `/`. Both configurations use the same workflow
assertions. The development server is for diagnosis.

Run `npm --prefix tests/ChapterTool.Wasm.E2E run test:e2e` to run all configured
browser projects. Install all engines first with
`npm --prefix tests/ChapterTool.Wasm.E2E exec -- playwright install`. Use
`test:e2e:headed` or `test:e2e:ui` from this directory for interactive
diagnosis.

## Reports and failures

The HTML report, JUnit report, traces, screenshots, videos, browser logs, and
downloaded files are written under `artifacts/wasm-e2e/`. Open the report with
`npm --prefix tests/ChapterTool.Wasm.E2E run report`.

Each test starts in a fresh BrowserContext. The fixture records page errors,
console errors, failed requests, and failed same-origin resources. On failure,
open the attached trace first. Check the console and network requests next.
Tests must wait for visible state changes. Do not add fixed delays to hide a
race. Tests run without retries. CI stops after three failures or ten minutes per engine.

`@smoke` marks startup, core import/edit/download flows, and short-screen modal closure. The `.NET 10 CI` workflow runs the behavior suite in Chromium. It runs targeted WebKit regressions for affected pull requests. Version tags, weekly checks, and manual CI runs use Chromium, Firefox, and WebKit. Pages deployment uploads the same prepared site after the required CI checks pass.

The WASM acceptance workflow also runs targeted WebKit modal and editing checks on pull requests. Set `E2E_RUN_NAME=webkit` for a separate report and result directory. The default browser suite includes `modal-layout.spec.ts`. It asserts geometry, actual clicks, focus, cancellation, background isolation, and long-content scrolling. It checks 320-pixel width and both sides of the 520- and 760-pixel breakpoints.

## Layout checks

`specs/layout-behavior.spec.ts` checks page bounds, dialog actions, expression diagnostics, downloads, and long-content footer access. It covers 1280×800, 1920×1080, 390×844, 390×640, and 844×390. Each viewport uses a separate test context. Chromium and targeted WebKit checks include these assertions.

Browser E2E verifies the WASM interface. Avalonia Headless verifies the desktop interface. Screenshots support failure diagnosis and manual review. There are no committed screenshot baselines or pixel comparison commands.

The expression preview test checks English and Chinese layout in the normal suite. Set `E2E_CAPTURE_REVIEW=1` to also capture default, wide, and narrow images under `artifacts/expression-preview/`.

Physical iPhone browser chrome and keyboard behavior require a device check. Mobile viewports and WebKit emulation do not establish that result.
