## 1. Browser project and deployment-shaped site

- [x] 1.1 Create `tests/ChapterTool.Wasm.E2E/` as an independent Node.js 22 Playwright Test and TypeScript project; pin dependencies and commit `package-lock.json`.
- [x] 1.2 Add the package scripts, TypeScript configuration, default Release config, development-server config, and fixed-environment visual config.
- [x] 1.3 Implement `prepare-site.mjs` to copy the Release `wwwroot`, apply the Pages base path, create deployment files, and validate cleanup targets before deleting output.
- [x] 1.4 Implement the loopback static server with MIME handling, 404 behavior for missing assets, and root-confined path resolution.
- [x] 1.5 Configure Playwright `webServer` lifecycle for both modes, one worker, no stale-server reuse, HTML/JUnit reporting, and failure trace/screenshot/video retention.
- [x] 1.6 Add small OGM, Unicode, malformed XML, and fixed expected-output fixtures; reuse tracked disc fixtures instead of copying the full fixture tree.
- [x] 1.7 Add an E2E README with clean-checkout installation, browser installation, site preparation, type check, run modes, report viewing, and troubleshooting commands.

## 2. Readiness, diagnostics, and P0 workflows

- [x] 2.1 Add the minimum accessible labels and table, preview, status, and application readiness semantics to the WASM page; set readiness after settings restore and JavaScript event registration.
- [x] 2.2 Add Playwright fixtures that wait for readiness and collect page errors, console errors, failed requests, same-origin resource failures, and attach diagnostics while failing on unexpected errors.
- [x] 2.3 Add semantic workspace helpers that operate through accessible roles and labels, scope modal queries, and wait for observable workflow outcomes.
- [x] 2.4 Implement B01–B04 Chromium smoke coverage for cold startup, import, edit/undo/redo, preview, and an actual download checked against fixed expected output.
- [x] 2.5 Implement B05–B09 P0 coverage for representative formats, replacement confirmation, malformed-input recovery, persisted settings, cancellation, and English/Chinese/Japanese localization.
- [x] 2.6 Ensure edits submitted through `@onchange` use a real blur action before history and export assertions; ensure file chooser, download, and confirm handlers are registered before user actions.
- [x] 2.7 Add `@smoke` tags to startup and core import/edit/download flows; keep tests independently runnable and do not generate expected exports through Core code.

## 3. CI and GitHub Pages release gates

- [x] 3.1 Add the Chromium E2E job to `.github/workflows/dotnet-ci.yml` with .NET 10, Node.js 22, Release publish, site preparation, type checking, browser installation, and report upload using `if: always()`.
- [x] 3.2 Include the E2E lockfile in the Node cache key and update relevant path filters for WASM, Core, Contracts, E2E files, workflows, shared build configuration, and localization sources.
- [x] 3.3 Update `.github/workflows/github-pages.yml` to prepare the deployment directory once, run Chromium `@smoke` against it, and upload that same unchanged directory only after the smoke gate passes.
- [x] 3.4 Verify CI retains reports, traces, screenshots, logs, JUnit output, and downloaded files; configure retry behavior so tests passing only after retry still fail the gate.
- [x] 3.5 Update `docs/code-map/testing.md` and `src/ChapterTool.Wasm/README.md` with E2E ownership, local commands, CI coverage, and diagnostic locations.

## 4. P1 boundaries, cross-browser, and visual acceptance

- [x] 4.1 Implement B10–B15 coverage for selection/deletion, keyboard boundaries, FPS and expression behavior, DOM file drop and size boundaries, per-tab session isolation, and malformed or inaccessible settings storage.
- [x] 4.2 Implement B16–B17 coverage for representative disc imports and MPLS append plus UTF-8/UTF-16 and BOM byte-level downloads; keep large boundary files temporary.
- [x] 4.3 Add a scheduled or manually triggered acceptance workflow for the full Chromium, Firefox, and WebKit suite and record pass/fail/skip counts per engine.
- [x] 4.4 Implement B18 layout interaction and geometry checks at 1280×800, 1920×1080, and 390×844; generate and review Chromium baselines in a fixed Linux environment.
- [x] 4.5 Run the first Chromium P0 suite three times with `--repeat-each=3 --retries=0`, then complete a no-retry three-engine acceptance run; investigate failures through traces and diagnostics rather than fixed delays or removed assertions.
- [x] 4.6 Record the first acceptance report with commit SHA, locked dependency and browser versions, OS, exact commands, per-engine pass/fail/skip counts, duration, artifact paths, and planned-versus-executed coverage.
- [x] 4.7 Run affected .NET test projects sequentially before the E2E suite when Razor or browser interop changes, then run the relevant browser acceptance checks and fix regressions caused by the change.
