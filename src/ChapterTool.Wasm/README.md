# ChapterTool WASM (Blazor)

ChapterTool WASM is a Blazor WebAssembly browser app that runs `ChapterTool.Core` in the browser.

The app provides chapter import, editing, time transforms, preview, and export. Its main workflow has these zones:

1. **Top** — Load / Save, optional clip selector, frame rate readout
2. **Center** — chapter grid (`#`, Time, Name, Frames)
3. **Bottom** — save format, chapter name mode, expression and advanced export actions
4. **Status strip** — status text + progress

Load imports data into the grid. Save and Preview use the Core projection and export options. Reload reuses the last successful file bytes. Append MPLS combines another playlist through the Core segment service.

## Prerequisites

- .NET 10 SDK  
- Chromium browser for Rider/VS debugging (Chrome / Edge; not Safari “Default”)  
- Optional AOT: `dotnet workload install wasm-tools`

## Run

```bash
dotnet run --project src/ChapterTool.Wasm/ChapterTool.Wasm.csproj --launch-profile ChapterTool.Wasm
```

Default URL: `http://localhost:5261`

### Rider

1. Run configuration: **ChapterTool.Wasm** (profile name matches `Properties/launchSettings.json`)  
2. Browser: **Chrome** or **Edge** (not Default)  
3. If profile missing: right-click `launchSettings.json` → **Generate Configurations**

## Interaction

| Action | Behavior |
|--------|----------|
| **Load** | Pick `.txt` / `.vtt` / `.xml` / `.cue` / `.mpls` / `.ifo` / `.xpl` / `.flac` / `.tak` → import → fill grid |
| **Reload / Append MPLS** | Load context menu reuses the last file or appends another MPLS group |
| **Clip combo** | Shown when import has multiple entries; switches active `ChapterSet` |
| **Clip combo context menu** | Combine MPLS/IFO entries or restore separate clips |
| **Grid edit** | Time, name, and frame drafts use Core cell edits. Enter, Tab, and valid blur commit once. Escape cancels. Invalid drafts retain an accessible error. |
| **Save** | `ChapterExportService` with bottom options → browser download |
| **Round frames + FPS** | `FrameRateService.UpdateFrames` fills Frames column (Auto detect or fixed rate) |
| **Frame rate context menu** | Review source and target FPS before applying one captured conversion candidate |
| **Expression** | Edit expressions inline and review candidate values in the grid before Apply or Discard. The advanced editor loads bounded UTF-8 Lua files and provides shared highlighting, completion, and positioned diagnostics. |
| **Edit history** | Expand or collapse retained branches, locate the current node, inspect its field changes, and restore any node. Undo and redo remain available. |
| **Advanced export options** | Stage XML language, encoding, and BOM preferences. Apply updates output preferences. |
| **Naming and numbering** | Review automatic names, loaded templates, and integer numbering offsets from 0 to 1000. Apply commits once and clears the active intent. |
| **Save as** | TXT, XML, QPFile, TimeCodes, … |
| **Chapter name** | As is / Auto generate |
| **Order +** | Display number shift |
| **XML lang** | Enabled only for XML export |
| **Settings** | Draft output, appearance, and shared shortcuts. Save persists before activation. Close can discard a changed draft or keep editing. Settings do not replay content operations. |
| **Log** | Filter the bounded list by severity and text, then reset both filters together. Open details explicitly. Copy an entry or download a captured filtered JSON/CSV snapshot. |
| **Selection / context actions** | Ctrl/Shift multi-select; batch delete, `--zones`, Preview, forward translation, and related-media references |
| **Drag and drop / language** | Drop-to-load with size/read errors; `en-US`, `zh-CN`, and `ja-JP` UI dictionaries |

Empty grid offers **Load OGM sample** for a quick smoke path.

All tools use the native browser dialog lifecycle. Dialogs contain focus and make the main page inert. Escape closes a dialog from focused inputs. Close returns focus to the opener when it still exists. Dialog bodies scroll independently. Short pages can scroll to retain the toolbar, grid, bottom controls, and status strip.

The browser app imports file bytes. It does not use local file paths for import.

Portable browser imports use the shared 64 MiB byte limit in `ChapterTool.Core.Boundaries.PortableInputPolicy`. The limit applies to load, reload, and MPLS append.

## Browser end-to-end tests

`tests/ChapterTool.Wasm.E2E` owns real-browser coverage for the Blazor page and
its JavaScript boundary. The suite uses Playwright Test and TypeScript. It does
not replace the workspace or Core test projects.

Run these commands from the repository root in a clean checkout:

```bash
dotnet restore src/ChapterTool.Wasm/ChapterTool.Wasm.csproj
dotnet publish src/ChapterTool.Wasm/ChapterTool.Wasm.csproj --configuration Release --no-restore --output artifacts/wasm-e2e/publish
npm --prefix tests/ChapterTool.Wasm.E2E ci
npm --prefix tests/ChapterTool.Wasm.E2E exec -- playwright install chromium
npm --prefix tests/ChapterTool.Wasm.E2E run prepare:site
npm --prefix tests/ChapterTool.Wasm.E2E run typecheck
npm --prefix tests/ChapterTool.Wasm.E2E run test:e2e -- --project=chromium
```

The default configuration tests the prepared Release output at
`http://127.0.0.1:5261/ChapterTool/`. Run `npm --prefix
tests/ChapterTool.Wasm.E2E run test:e2e:dev` to use the local development
server at `/`. Use `test:e2e:headed` or `test:e2e:ui` for interactive diagnosis.
Run all installed engines with `test:e2e` and compare layout snapshots with
`test:visual` in the fixed Linux CI environment.

Playwright writes reports, traces, screenshots, and downloaded files under
`artifacts/wasm-e2e/`. Open the HTML report with
`npm --prefix tests/ChapterTool.Wasm.E2E run report`.
The first multi-browser and visual acceptance results are in
`docs/testing/wasm-browser-e2e-acceptance.md`.

## Feature boundaries

The browser app imports text, XML, CUE, WebVTT, MPLS, IFO, HD-DVD XPL, and embedded FLAC/TAK CUE data from bytes. It supports chapter editing, managed Lua expressions with Core presets, frame transforms, templates, export formats, settings persistence, drag and drop, and browser downloads.

The browser does not expose choosing a local save directory, running `mkvtoolnix`/`ffprobe`, importing external-tool media sources, opening local Related Media through a desktop shell, system font enumeration, or desktop Sentry telemetry.

Related Media paths are informational. Relative paths are rendered as browser links when present, but local filesystem paths are not made accessible by WASM.

## Publish (local)

```bash
dotnet publish src/ChapterTool.Wasm/ChapterTool.Wasm.csproj -c Release -o artifacts/wasm
```

Focused verification:

```bash
dotnet test tests/ChapterTool.Wasm.Tests/ChapterTool.Wasm.Tests.csproj --no-restore
dotnet build src/ChapterTool.Wasm/ChapterTool.Wasm.csproj --no-restore
git diff --check
```

Static site root: `artifacts/wasm/wwwroot` (or the project `bin/Release/net10.0/publish/wwwroot` path).

## GitHub Pages

CI workflow: `.github/workflows/github-pages.yml`

| Trigger | Behavior |
|---------|----------|
| `push` to `master` (WASM / Core / workflow paths) | Build + deploy |
| `workflow_dispatch` | Manual deploy |

Published URL (project pages):

`https://tautcony.github.io/ChapterTool/`

### One-time repository settings

1. **Settings → Pages → Build and deployment → Source**: **GitHub Actions**
2. Ensure Actions can run workflows (default GITHUB_TOKEN is enough for `pages: write`)
3. First deploy: Actions → **Deploy WASM (GitHub Pages)** → **Run workflow**, or merge to `master`

The workflow publishes the browser app and deploys its static files through GitHub Pages.
