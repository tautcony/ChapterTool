# ChapterTool WASM browser tests

This project tests the Blazor page in real browsers. It uses Playwright Test and
TypeScript. It is separate from the .NET solution and `packages/chaptertool`.

## Prerequisites

- Node.js 22.x
- .NET 10 SDK
- Chromium and WebKit for the pull request suite; Chromium, Firefox, and WebKit for full
  acceptance

## Install and run

Run from the repository root:

```bash
dotnet restore src/ChapterTool.Wasm/ChapterTool.Wasm.csproj
dotnet publish src/ChapterTool.Wasm/ChapterTool.Wasm.csproj --configuration Release --no-restore --output artifacts/wasm-e2e/publish
npm --prefix tests/ChapterTool.Wasm.E2E ci
npm --prefix tests/ChapterTool.Wasm.E2E exec -- playwright install chromium
npm --prefix tests/ChapterTool.Wasm.E2E run prepare:site
npm --prefix tests/ChapterTool.Wasm.E2E run typecheck
npm --prefix tests/ChapterTool.Wasm.E2E run test:e2e -- --project=chromium
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
diagnosis. Run `npm run test:visual` only in the fixed Linux visual environment.

## Reports and failures

The HTML report, JUnit report, traces, screenshots, videos, browser logs, and
downloaded files are written under `artifacts/wasm-e2e/`. Open the report with
`npm --prefix tests/ChapterTool.Wasm.E2E run report`.

Each test starts in a fresh BrowserContext. The fixture records page errors,
console errors, failed requests, and failed same-origin resources. On failure,
open the attached trace first. Check the console and network requests next.
Tests must wait for visible state changes. Do not add fixed delays to hide a
race. A test that passes only after retry still fails the CI gate.

`@smoke` marks startup, core import/edit/download flows, and short-screen modal closure. The Pages workflow
runs these tests against the exact prepared directory it uploads. The
`.NET 10 CI` workflow runs all implemented behavior tests in Chromium. The
weekly and manually triggered WASM browser acceptance workflow runs Chromium,
Firefox, and WebKit.

The WASM acceptance workflow also runs targeted WebKit modal and editing checks on pull requests. Set `E2E_RUN_NAME=webkit` for a separate report and result directory. The default browser suite includes `modal-layout.spec.ts`. It asserts geometry, actual clicks, focus, cancellation, background isolation, and long-content scrolling. It checks 320-pixel width and both sides of the 520- and 760-pixel breakpoints.

## Visual checks

`specs/layout.spec.ts` checks idle, history, expression, diagnostics, advanced export, and settings states at 1280×800, 1920×1080, 390×844, 390×640, and 844×390. Visual checks run on pull requests. Results use `results-visual`, `report-visual`, and `junit-visual.xml`. Create
or update baselines only in the fixed Linux environment. Review each image
change before committing it.

Physical iPhone browser chrome and keyboard behavior require a device check. Mobile viewports and WebKit emulation do not establish that result.
