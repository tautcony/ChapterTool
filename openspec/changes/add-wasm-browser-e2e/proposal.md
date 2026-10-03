## Why

The Blazor WebAssembly application has unit and workspace coverage, but no automated check that starts the published site in a real browser. Add browser coverage now so pull requests and GitHub Pages deployments can catch failures in startup, user interaction, JavaScript interop, and downloaded files.

## What Changes

- Add a standalone Playwright Test and TypeScript project for the WASM application.
- Test the Release static site mounted under the GitHub Pages `/ChapterTool/` path, and provide a development-server mode for local diagnosis.
- Cover core import, editing, history, settings, localization, export, keyboard, drag-and-drop, disc workflows, and responsive layout in staged browser suites.
- Add minimal accessible names and stable readiness semantics to the page where browser tests need them.
- Run Chromium E2E checks in CI and a smoke suite against the Pages artifact before deployment; define scheduled or manual cross-browser acceptance for Chromium, Firefox, and WebKit.
- Document setup, commands, ownership, and the first real acceptance results after implementation.

## Capabilities

### New Capabilities
- `wasm-browser-e2e`: Real-browser verification of the published WASM application, including user-visible workflows, browser boundaries, downloadable output, and CI release gates.

### Modified Capabilities

None.

## Impact

- Adds `tests/ChapterTool.Wasm.E2E/` with its locked Node.js dependencies, Playwright configuration, fixtures, support helpers, and browser specs.
- Changes the WASM Razor page only where required for accessible browser interaction and app readiness.
- Updates `.github/workflows/dotnet-ci.yml`, `.github/workflows/github-pages.yml`, `docs/code-map/testing.md`, and `src/ChapterTool.Wasm/README.md`.
- Produces ignored diagnostics under `artifacts/wasm-e2e/` and maintains reviewed visual baselines in the E2E project.
- Keeps existing .NET and package tests in place; browser E2E supplements rather than replaces them.
