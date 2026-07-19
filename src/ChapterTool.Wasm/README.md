# ChapterTool WASM (Blazor)

Blazor WebAssembly standalone browser workspace that hosts `ChapterTool.Core` with an **Avalonia-like main workflow**:

1. **Top** — Load / Save, optional clip selector, frame rate readout
2. **Center** — chapter grid (`#`, Time, Name, Frames)
3. **Bottom** — save format, chapter name mode, order shift, XML language, expression
4. **Status strip** — status text + progress

Load imports immediately into the grid. Save and Preview use the same Core projection/export options. Reload reuses the last successful file bytes; Append MPLS combines another playlist through Core's segment service. There is no separate “paste source → convert” pipeline.

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

## Interaction (same idea as Avalonia)

| Action | Behavior |
|--------|----------|
| **Load** | Pick `.txt` / `.vtt` / `.xml` / `.cue` / `.mpls` / `.ifo` → import → fill grid |
| **Reload / Append MPLS** | Load context menu reuses the last file or appends another MPLS group |
| **Clip combo** | Shown when import has multiple entries; switches active `ChapterSet` |
| **Clip combo context menu** | Combine MPLS/IFO entries or restore separate clips |
| **Grid edit** | Time / Name editable; used on save |
| **Save** | `ChapterExportService` with bottom options → browser download |
| **Round frames + FPS** | `FrameRateService.UpdateFrames` fills Frames column (Auto detect or fixed rate) |
| **Frame rate context menu** | Change chapter timing from the current frame rate to the selected valid rate |
| **Expression + Use** | `ChapterOutputProjectionService` / Lua engine rewrites times + frames in the grid |
| **Save as** | TXT, XML, QPFile, TimeCodes, … |
| **Chapter name** | As is / Auto generate |
| **Order +** | Display number shift |
| **XML lang** | Enabled only for XML export |
| **Settings** | Modal settings pages with browser-persisted output, appearance, and WASM capability preferences |
| **Selection / context actions** | Ctrl/Shift multi-select; batch delete, `--zones`, Preview, forward translation, and related-media references |
| **Drag and drop / language** | Drop-to-load with size/read errors; `en-US`, `zh-CN`, and `ja-JP` UI dictionaries |

Empty grid offers **Load OGM sample** for a quick smoke path.

## Architecture

| Piece | Role |
|-------|------|
| `Services/WasmWorkspace` | Browser session state: load/reload/append, clip selection, multi-select editing, projection, diagnostics, logs, and save/preview |
| `Services/WasmChapterService` | Byte-based Core importers + `ChapterExportService` |
| `Services/WasmLocalizer` | `en-US` / `zh-CN` / `ja-JP` UI and workspace status strings |
| `Pages/Home.razor` | Main shell UI zones |
| `wwwroot/js/download.js` | File picker trigger, encoded export download, appearance application, and localStorage persistence |

Browser note: always pass chapter bytes via `ChapterImportRequest.Content` (no real filesystem).

## Feature boundaries

Implemented browser behavior includes text/XML/CUE/WebVTT/MPLS/IFO byte imports, chapter editing, managed Lua expressions over chapter data, frame transforms, templates, export formats, settings persistence, drag and drop, and browser downloads.

The browser intentionally does not expose desktop-only behavior: choosing a local save directory, running `mkvtoolnix`/`eac3to`/`ffprobe`/`ffmpeg`, importing external-tool media/BDMV sources, opening local Related Media through a desktop shell, or loading Lua script files and the Lua editor/completion workflow.

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

The workflow:

1. `dotnet publish` the Blazor WASM app (Release)
2. Writes `.nojekyll` so `_framework` is not ignored by Jekyll
3. Copies `index.html` → `404.html` for SPA deep-link fallback
4. Rewrites `<base href>` to `/ChapterTool/` for project-page hosting
5. Uploads and deploys via `actions/deploy-pages`

Local smoke of the same publish layout:

```bash
dotnet publish src/ChapterTool.Wasm/ChapterTool.Wasm.csproj -c Release
# serve src/.../bin/Release/net10.0/publish/wwwroot with any static host
```
