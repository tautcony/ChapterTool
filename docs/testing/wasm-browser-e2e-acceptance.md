# WASM browser E2E acceptance

## First acceptance run

- Date: 2026-10-04 (Asia/Shanghai).
- Source revision: `b1126a189ad569a5a2698b4612634f32efb96efa`.
- The working tree contained the OpenSpec change under test.
- Playwright: `@playwright/test` 1.63.0.
- Locked development dependencies: TypeScript 7.0.2 and `@types/node` 26.6.4.
- Node.js: 22.16.0 on Windows; 22.22.3 in the visual container.
- Windows: Windows 11 Pro Workstation, build 26200, x64.
- Visual container: Debian 12, x64, `node:22-slim` image digest `sha256:7af03b14a13c8cdd38e45058fd957bf00a72bbe17feac43b1c15a689c029c732`.
- Browsers: Chromium 153.0.8010.12, Firefox 155.0, and WebKit 26.6.

## Results

| Check | Result | Duration |
| --- | ---: | ---: |
| WASM workspace tests | 38 passed, 0 failed, 0 skipped | 1.7 seconds |
| Chromium smoke repeated three times | 6 passed, 0 failed, 0 skipped | 11.9 seconds |
| Chromium E2E suite | 18 passed, 0 failed, 0 skipped | Included in the three-engine run |
| Firefox E2E suite | 18 passed, 0 failed, 0 skipped | Included in the three-engine run |
| WebKit E2E suite | 18 passed, 0 failed, 0 skipped | Included in the three-engine run |
| Three-engine E2E suite, no retries | 54 passed, 0 failed, 0 skipped | 1.9 minutes |
| B18 Linux visual suite | 1 passed, 0 failed, 0 skipped | 2.1 seconds |

The B18 run exercised 1280×800, 1920×1080, and 390×844 viewports. It checked modal bounds, key interactions, horizontal overflow, and screenshot baselines. The narrow viewport check found and verified a responsive settings-dialog fix.

## Commands

The Windows checks ran from `tests/ChapterTool.Wasm.E2E`:

```powershell
npm run typecheck
node node_modules/playwright/cli.js test --project=chromium --grep='@smoke' --repeat-each=3 --retries=0
$env:E2E_NO_RETRY='1'; node node_modules/playwright/cli.js test
```

The WASM test ran from the repository root:

```powershell
dotnet test tests/ChapterTool.Wasm.Tests/ChapterTool.Wasm.Tests.csproj --configuration Release --no-restore
```

The Linux visual run prepared the same Pages-shaped site, then ran:

```bash
npm run test:visual -- --update-snapshots
npm run test:visual
```

The first command created the reviewed baselines. The second command passed without updating them.

## Coverage

- B01–B09 cover startup, import, edit history, preview, download, export formats, replacement confirmation, malformed XML, settings persistence, cancellation, and English, Chinese, and Japanese localization.
- B10–B15 cover row selection and deletion, keyboard behavior, frame-rate and expression changes, file drop and size limits, tab isolation, and unavailable settings storage.
- B16–B17 cover MPLS and XPL input, MPLS append, and UTF-8/UTF-16 downloads with BOM byte checks.
- B18 covers layout interactions and geometry at the three recorded viewport sizes.

The HTML report, JUnit report, traces, screenshots, videos, browser logs, and downloaded files are under `artifacts/wasm-e2e/`. Screenshot baselines are under `tests/ChapterTool.Wasm.E2E/specs/layout.spec.ts-snapshots/`.
