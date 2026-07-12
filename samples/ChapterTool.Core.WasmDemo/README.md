# ChapterTool.Core WASM Demo (Blazor)

Blazor WebAssembly standalone sample that hosts `ChapterTool.Core` with an **Avalonia-like main workflow**:

1. **Top** — Load / Save / Save As, optional clip selector, frame rate readout  
2. **Center** — chapter grid (`#`, Time, Name, Frames)  
3. **Bottom** — save format, chapter name mode, order shift, XML language, expression  
4. **Status strip** — status text + progress  

Load imports immediately into the grid. Save exports with the current bottom options and downloads the result. There is no separate “paste source → convert” pipeline.

## Prerequisites

- .NET 10 SDK  
- Chromium browser for Rider/VS debugging (Chrome / Edge; not Safari “Default”)  
- Optional AOT: `dotnet workload install wasm-tools`

## Run

```bash
dotnet run --project samples/ChapterTool.Core.WasmDemo/ChapterTool.Core.WasmDemo.csproj --launch-profile ChapterTool.Core.WasmDemo
```

Default URL: `http://localhost:5261`

### Rider

1. Run configuration: **ChapterTool.Core.WasmDemo** (profile name matches `Properties/launchSettings.json`)  
2. Browser: **Chrome** or **Edge** (not Default)  
3. If profile missing: right-click `launchSettings.json` → **Generate Configurations**

## Interaction (same idea as Avalonia)

| Action | Behavior |
|--------|----------|
| **Load** | Pick `.txt` / `.vtt` / `.xml` / `.cue` / `.mpls` / `.ifo` → import → fill grid |
| **Clip combo** | Shown when import has multiple entries; switches active `ChapterSet` |
| **Grid edit** | Time / Name editable; used on save |
| **Save / Save As** | `ChapterExportService` with bottom options → browser download |
| **Round frames + FPS** | `FrameRateService.UpdateFrames` fills Frames column (Auto detect or fixed rate) |
| **Expression + Use** | `ChapterOutputProjectionService` / Lua engine rewrites times + frames in the grid |
| **Save as** | TXT, XML, QPFile, TimeCodes, … |
| **Chapter name** | As is / Auto generate |
| **Order +** | Display number shift |
| **XML lang** | Enabled only for XML export |

Empty grid offers **Load OGM sample** for a quick smoke path.

## Architecture

| Piece | Role |
|-------|------|
| `Services/DemoWorkspace` | Avalonia-like session: load, clip select, rows, options, save |
| `Services/ChapterDemoService` | Core importers + `ChapterExportService` |
| `Pages/Home.razor` | Main shell UI zones |
| `wwwroot/js/download.js` | File picker trigger + export download |

Browser note: always pass chapter bytes via `ChapterImportRequest.Content` (no real filesystem).

## Publish (local)

```bash
dotnet publish samples/ChapterTool.Core.WasmDemo/ChapterTool.Core.WasmDemo.csproj -c Release -o artifacts/wasm-demo
```

Static site root: `artifacts/wasm-demo/wwwroot` (or the project `bin/Release/net10.0/publish/wwwroot` path).

## GitHub Pages

CI workflow: `.github/workflows/github-pages.yml`

| Trigger | Behavior |
|---------|----------|
| `push` to `master` (demo / Core / workflow paths) | Build + deploy |
| `workflow_dispatch` | Manual deploy |

Published URL (project pages):

`https://tautcony.github.io/ChapterTool/`

### One-time repository settings

1. **Settings → Pages → Build and deployment → Source**: **GitHub Actions**
2. Ensure Actions can run workflows (default GITHUB_TOKEN is enough for `pages: write`)
3. First deploy: Actions → **Deploy WASM Demo (GitHub Pages)** → **Run workflow**, or merge to `master`

The workflow:

1. `dotnet publish` the Blazor WASM demo (Release)
2. Writes `.nojekyll` so `_framework` is not ignored by Jekyll
3. Copies `index.html` → `404.html` for SPA deep-link fallback
4. Rewrites `<base href>` to `/ChapterTool/` for project-page hosting
5. Uploads and deploys via `actions/deploy-pages`

Local smoke of the same publish layout:

```bash
dotnet publish samples/ChapterTool.Core.WasmDemo/ChapterTool.Core.WasmDemo.csproj -c Release
# serve samples/.../bin/Release/net10.0/publish/wwwroot with any static host
```
