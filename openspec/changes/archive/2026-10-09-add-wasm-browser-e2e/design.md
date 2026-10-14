## Context

The WASM application is a Blazor WebAssembly site. Existing .NET tests exercise its workspace and browser shortcut rules, but they do not launch the published site or a real browser. GitHub Pages publishes a static site below a repository subpath, so the browser suite must exercise the prepared deployment shape as well as support local development diagnosis.

The source plan is `docs/testing/wasm-browser-e2e-plan.md`. It defines the proposed project layout, scenarios, CI gates, and acceptance evidence. This design keeps that plan as the detailed implementation reference while the capability spec defines required outcomes.

## Goals / Non-Goals

**Goals:**
- Exercise the actual Blazor page and JavaScript interop through Playwright browser interaction.
- Test the same prepared Release static directory that is uploaded for GitHub Pages.
- Make browser diagnostics actionable and retain user-visible behavior as the assertion boundary.
- Stage implementation from Chromium smoke coverage through cross-browser and visual acceptance.

**Non-Goals:**
- Replace Core, workspace, Avalonia, or package-level tests.
- Re-test parser and conversion algorithms by calling internal APIs from browser tests.
- Claim operating-system file drag/drop coverage or full Safari platform coverage from Playwright WebKit.
- Add browser automation dependencies to `packages/chaptertool` or the .NET solution.

## Decisions

### Use a standalone Playwright Test and TypeScript project

Place the suite under `tests/ChapterTool.Wasm.E2E/`, with its own pinned dependencies and lockfile. Playwright Test provides browser projects, isolated contexts, download/file chooser events, managed web servers, traces, and screenshot assertions. Do not add a .NET browser-test host or reuse the Node package project.

Alternatives considered: Playwright .NET would keep tests in C# but adds browser-host lifecycle and package concerns to the .NET test setup. Cypress and Selenium do not fit the plan's direct three-engine project model and the repository's lack of WebDriver infrastructure.

### Make the Pages-shaped Release artifact the default target

Publish WASM once, prepare its `wwwroot` into `artifacts/wasm-e2e/site/ChapterTool/`, and serve that directory at `/ChapterTool/`. The preparation logic must match Pages base-path behavior and produce the static deployment files. The Pages workflow must test and upload the same prepared directory so the tested bytes are the deployed bytes.

The local development configuration uses the Blazor development server at `/` for diagnosis and keeps the same tests and assertions. Both modes use Playwright `webServer` lifecycle management and refuse to reuse an existing process.

Alternative considered: testing only the development server is faster to set up but misses release output, static MIME handling, and repository subpath regressions.

### Use semantic interaction and explicit readiness

Prefer roles, labels, and scoped dialog locators. Add only the accessible names and stable attributes needed to identify the table, fields, preview, status, and application readiness. Set readiness after initialization, settings restoration, and JavaScript event registration. Treat readiness as a synchronization signal, not proof of working interop; smoke coverage must import a real fixture.

Wait for observable outcomes such as table values, row counts, button states, dialogs, downloads, and navigation behavior. For `@onchange` editing, blur after filling before asserting history or exported output.

Alternative considered: CSS selectors and direct calls into the WASM bridge are more coupled to implementation and bypass the user's actual path.

### Isolate browser state and collect diagnostics by default

Use a fresh BrowserContext per test. Use two pages in one context only for tests that distinguish per-tab workspace state from shared localStorage settings. Install listeners before navigation for page errors, console errors, failed requests, and failed same-origin resource responses. Attach their output on failure and fail on unexpected errors; tests that intentionally inject failures must declare the expected errors.

Use browser-side JavaScript only to create boundary conditions that users can trigger, such as DOM `DataTransfer`, malformed persisted settings, or storage denial. Do not mock application results, parsers, or downloads.

### Gate in stages and preserve evidence

Start with Chromium P0 behavior in pull requests. Add a Pages pre-deployment Chromium `@smoke` run. Run the complete suite in Chromium, Firefox, and WebKit on a scheduled or manual workflow. Run screenshot comparisons only in a fixed Linux environment with reviewed baselines. Upload reports and failure diagnostics even when the job fails.

Use one worker initially to constrain WASM memory use. A CI retry may collect diagnostics, but flaky retries remain failures. Do not fix timing failures with fixed sleeps or erase assertions.

## Risks / Trade-offs

- [Three browser engines increase CI time and maintenance] → Keep pull request coverage on Chromium and run the full matrix on scheduled/manual and release acceptance paths.
- [WASM cold start may exceed the initial timeout] → Measure CI duration and adjust the timeout from evidence; synchronize on readiness and observable results.
- [Visual baselines vary with fonts and runtime images] → Pin the Linux runner, browser version, locale, viewport, fonts, and theme; review baseline updates manually.
- [Static preparation can diverge from Pages deployment] → Share the preparation script and make Pages test and upload one unchanged prepared artifact.
- [DOM drag/drop does not cover shell-level file dragging] → Record that limitation and retain focused manual platform acceptance where needed.

## Migration Plan

1. Add the standalone test project, preparation and static-serving scripts, fixtures, readiness contract, and Chromium smoke workflow.
2. Add P0 scenarios and the Pages pre-deployment smoke gate; run and record real acceptance results.
3. Add P1 browser-boundary and disc workflows, then enable the three-engine acceptance workflow.
4. Add fixed-environment visual coverage and reviewed baselines.
5. Update testing ownership and WASM usage documentation as each command becomes available.

Rollback consists of disabling the new workflow gates and removing the standalone test project. It does not require changes to persisted application data or user-facing APIs.

## Open Questions

- Which scheduled cadence best balances cross-browser feedback and CI cost? The implementation can begin with a weekly schedule and adjust from observed duration and regressions.
- Does the first implementation need all P1 import formats in the initial cross-browser run, or can they enter incrementally by stage? The task list stages them without changing the final coverage goal.
