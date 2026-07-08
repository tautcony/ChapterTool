# Feature Implementation Progress Report

Change: `rewrite-avalonia-dotnet10`

Date: 2026-06-07

## Executive Summary

The current implementation is more usable than the previous audit state, but it still cannot be honestly described as fully implemented or legacy-complete.

Automated build and tests pass. The IFO crash caused by clip-selection refresh recursion has been fixed, real source browsing/save-directory/append-MPLS workflows have been added, preview/log/zones/forward-shift windows now perform live work, color/language settings have persistence entry points, and related-media references are generated for MPLS/IFO/XPL/MP4. The main Avalonia window has also been reworked toward the original compact light tool-window layout from the legacy screenshot while keeping normal operation on cross-platform file/settings services instead of Windows registry dependencies. The highest remaining risks are MP4 native reading, deep GUI automation, Windows association/elevation, packaging validation, and several legacy-only transform/export details.

Current verification:

```powershell
dotnet test ChapterTool.Avalonia.slnx --no-restore
openspec validate rewrite-avalonia-dotnet10 --strict
```

Current test totals:

- Core: 104 passed
- Infrastructure: 32 passed
- Avalonia: 45 passed

## Progress By OpenSpec Module

| Module | Overall Status | Progress | Summary |
| --- | --- | ---: | --- |
| Core model/transform/export | Partial | ~74% | Main data model, time formatting, expression basics, edit operations, zones, forward shift, seven export formats, combine/append are present. Legacy-complete behavior is not done. |
| Text/XML/Matroska/WebVTT importers | Partial | ~88% | OGM/TXT, WebVTT, Matroska adapter are strong. Runtime `.txt/.xml/.vtt` routing is now covered. XML `ChapterTimeEnd` behavior still differs from module docs. |
| CUE/FLAC/TAK import/export | Partial | ~78% | CUE parser/importer is close to done; runtime `.cue` routing and CUE save integration are now covered. FLAC/TAK compatibility tests still need expansion. |
| Disc/playlist/media importers | Partial | ~68% | MPLS/IFO/XPL/BDMV adapter foundations exist. MPLS/IFO/XPL/MP4 source refs and Open Related Media are now implemented. MP4 real reader and deeper DVD/BDMV cases remain incomplete. |
| Avalonia UI shell/interactions | Partial | ~70% | Main layout now follows the legacy compact light tool-window shape with large load/save actions, central grid, bottom options, and compact context-menu actions. Real browse, save-to, append MPLS, PageUp/PageDown, save-format shortcuts, preview/log/zones/forward-shift, and related-media actions exist. Full MVVM binding and deep GUI automation are still incomplete. |
| Supporting UI/platform services | Partial | ~60% | Settings, colors storage, process runner, native dependency discovery, real file pickers, clipboard-backed preview/log copy, shell open, and auxiliary window content are improved. Windows association/elevation and full localization remain incomplete. |
| Tests/build/distribution/assets | Partial | ~65% | Solution, CI, fixture resolution, core tests, runtime route/save tests and process smoke tests exist. Publish artifacts, licenses/native layout, and real GUI automation are incomplete. |

## Detailed Progress

### Core Model / Transform / Export

| Feature | Status | Notes |
| --- | --- | --- |
| Core records and UI boundary | Done | `Chapter`, `ChapterInfo`, `ChapterInfoGroup`, `ChapterSourceOption` exist and Core has no UI dependency. |
| Time formatting/parsing | Done | Legacy rollover, >24h hour component, invalid diagnostics, CUE 75fps are covered. |
| Frame rate and frame display | Partial | Basic explicit/detect/K/star behavior exists. Missing expression-applied frame calculation and some legacy `ChangeFps` behavior. |
| Expression service | Partial | Infix/postfix, `t`, `fps`, failure diagnostics exist. Missing several legacy constants/functions and some token coverage. |
| Edit operations | Partial | Edit time/frame/name, delete-first shift, insert, order shift, template apply, forward translation, and create zones exist. Missing conditional MPLS/IFO rename regeneration and several legacy edge cases. |
| Naming behavior | Partial | Export has fallback `Chapter NN`, template apply exists. Missing standalone naming service and fuller auto/template behavior. |
| Export formats | Partial | TXT/XML/QPF/TimeCodes/tsMuxeR/CUE/JSON generate content. Missing byte/BOM contract, save-path policy, some XML legacy details, JSON separator edge cases. |
| Segment combine/append | Done | MPLS/DVD combine and MPLS append have tests. Source metadata/media references are not preserved fully. |

### Text / XML / Matroska / WebVTT

| Feature | Status | Notes |
| --- | --- | --- |
| Importer contract | Done | Structured importer contract and diagnostics exist. |
| OGM/TXT | Done | Existing sample, normalization, invalid first line, partial parse, dangling time partial are covered. |
| WebVTT | Done | Header, cue ids, malformed cue, unsupported timing settings are covered. |
| XML / Matroska XML | Partial | Multi-edition, nested flatten, duplicate removal exist. `ChapterTimeEnd` is stored as `Chapter.End`, while module docs describe generating an extra boundary chapter. |
| Matroska adapter | Done | `mkvextract` adapter has missing tool, stdout XML, stderr, non-zero, timeout, cancel, quoting coverage. |
| Runtime GUI routing | Done | `.txt/.xml/.vtt/.mkv/.mka` are routed; runtime tests now cover text/XML/WebVTT plus Matroska missing-dependency routing. |

### CUE / FLAC / TAK

| Feature | Status | Notes |
| --- | --- | --- |
| CUE importer | Done | `.cue`, decoder, UTF BOMs, sample, Japanese file name are covered. |
| CUE parser | Done | Title/file/track/index mapping and malformed diagnostics exist. Exact old blank-line behavior differs. |
| FLAC embedded CUE | Partial | Invalid header, Vorbis cuesheet, missing cuesheet, native block skip covered. Missing malformed/truncated Vorbis and uppercase marker tests. |
| TAK embedded CUE | Partial | Header, marker, terminator, non-ASCII padding covered. Missing real TAK fixture and tail-window compatibility behavior. |
| CUE export | Partial | Content path works. Not separated into a concrete `IChapterExporter`; BOM/encoding and save naming policy are not fully tested. |
| Runtime routing/save | Partial | `.cue/.flac/.tak` route exists. Runtime `.cue` dispatch and CUE write integration are covered; `.flac/.tak` runtime dispatch and compatibility fixture coverage remain incomplete. |

### Disc / Playlist / Media

| Feature | Status | Notes |
| --- | --- | --- |
| MPLS importer | Partial | Sample, PTS, fps, multi-angle, invalid header and `../STREAM/*.m2ts` source refs exist. Missing no-mark fallback and missing primary video tests. |
| IFO/DVD importer | Partial | Existing sample, PAL/NTSC conversion, and `.VOB` source refs are covered. Missing multi-PGC/title, short-title filtering, invalid fps, and metadata path coverage. |
| XPL importer | Partial | Synthetic title, malformed XML, and `../HVDVD_TS/*` source refs are covered. Missing real fixture and finer diagnostics. |
| BDMV/eac3to | Partial | Missing dependency, metadata title, fake eac3to path covered. Missing more output shapes, timeout/cancel, path/non-ASCII tests. |
| MP4 | Missing/Partial | Core wrapper can consume a fake reader and now emits media refs. Runtime reader still only returns `NativeLibraryMissing`/`NativeReadFailed`; no real native reader. |
| Runtime routing | Partial | `.mpls/.ifo/.xpl/BDMV/.mp4` routing exists. MP4 is diagnostic-only; BDMV success path is not runtime-tested. |
| Clip combine/append | Partial | Core rules exist. UI append now uses a dedicated MPLS file picker; full GUI append automation is still missing. |
| Source media refs | Partial | MPLS/IFO/XPL/MP4 refs and Open Related Media UI command are implemented. BDMV refs and real shell-open GUI automation remain incomplete. |

### Avalonia UI Shell / Interactions

| Feature | Status | Notes |
| --- | --- | --- |
| Main window structure | Partial | Has path, load/save, format, clip selector, progress, advanced panel, DataGrid, aux buttons. Still skeletal versus legacy UI. |
| ViewModel-driven UI | Partial | ViewModel command surface exists, but no full `INotifyPropertyChanged`; window manually calls `Refresh()`. |
| Keyboard shortcuts | Partial | `Ctrl+O/S/R/L`, `Alt+S`, `F5/F11`, `Ctrl+0..9`, Insert/Delete, PageUp/PageDown, and Alt+number save-format selection are wired. Expression shortcuts and F11 behavior parity still need work. |
| DataGrid editing | Partial | Cell edit commits to commands. No real GUI edit automation verifies behavior. |
| Clip selection | Partial | Recursion crash fixed. PageUp/PageDown and Open Related Media are implemented. Full GUI automation is still missing. |
| Drag/drop | Partial | Drop loads first path and IFO crash fixed. No DragEnter/multi-file/BDMV GUI automation. |
| Context menus | Partial | Insert/Delete/Combine/Open Related Media/Preview are present. Legacy load/clip/row menu behaviors are still incomplete. |
| Load/save | Partial | Path load/save, real source picker, save-to directory picker, save directory persistence, and log updates work. Overwrite handling and richer errors remain incomplete. |
| Auxiliary windows | Partial | Preview/log/zones/forward shift are functional; color/language/template windows have editable/persisted entry points. File association remains a placeholder. |
| Startup path / IFO | Partial | Startup path can load IFO and no longer crashes. GUI state is not validated beyond process/memory and service-level assertions. |

### Supporting UI / Platform Services

| Feature | Status | Notes |
| --- | --- | --- |
| Settings migration | Done | Legacy `chaptertool.json` migration is covered. |
| Theme color storage | Partial | Six-slot storage/migration exists and color window can save six slots. Applying the theme live is not done. |
| Process runner | Done | stdout/stderr/exit/timeout/cancel/command/working directory covered. |
| External tool locator | Partial | Configured path and missing diagnostics exist. Registry/install discovery missing. |
| Dialog service | Partial | Test scripted service exists; main source/save/MPLS selection now uses Avalonia storage pickers. Message/confirmation dialogs remain incomplete. |
| Clipboard service | Partial | Memory service and Avalonia clipboard wrapper exist; preview/log copy use the window clipboard. Broader clipboard integration remains incomplete. |
| Shell service | Partial | Basic `UseShellExecute` open exists; platform/failure behavior under-tested. |
| Window service | Partial | Reusable windows now render preview/log/color/language/expression/template/zones/forward-shift content. File association remains a placeholder. |
| Localization | Partial | Dictionary service exists and UI language persistence is wired. Real zh/en-US Avalonia resource application remains incomplete. |
| File association | Partial | Unsupported stub exists; Windows `.mpls` registration missing. |
| Privilege/elevation | Partial | Unsupported stub exists; Windows admin detection/elevation/manifest missing. |
| Native dependency service | Done | `libmp4v2` platform file lookup and missing diagnostics exist. |

### Tests / Build / Distribution

| Feature | Status | Notes |
| --- | --- | --- |
| .NET 10 topology | Done | Core/Infrastructure/Avalonia/tests are split. |
| xUnit migration/layering | Partial | Main parser/import/platform/VM tests exist. SharpDvdInfo, MP4 native, and full GUI are incomplete. |
| Fixture preservation | Done | Fixture resolver and non-ASCII CUE coverage exist. |
| CI restore/build/test | Done | Workflow runs restore/build/test. |
| Publish artifacts | Partial | Publish script exists. CI upload, artifact layout validation, native/license checks are incomplete. |
| Assets packaging | Partial | `Assets/**` copied. Application icon/installer icon strategy not complete. |
| Licenses | Partial | GPL and strategy docs exist. Third-party license inclusion is not enforced by publish tests. |
| Unified version | Done | `Directory.Build.props` centralizes version. |
| Fody/Costura retirement | Done | Strategy documents retiring legacy bundling. |
| GUI verification | Partial | Process lifetime, startup path, XAML static checks, VM tests, runtime route/save tests, and IFO memory/response checks exist. Missing Avalonia Headless/FlaUI real interaction tests. |

## Highest Priority Remaining Work

1. Add Avalonia Headless or FlaUI tests for load/edit/save/shortcut/context-menu/drag-drop workflows.
2. Replace File Association placeholder with a real Windows-gated workflow.
3. Implement or explicitly retire real MP4 native reading; current runtime MP4 is diagnostic-only.
4. Finish BDMV source refs and more eac3to success/timeout/path fixtures.
5. Complete overwrite/error dialogs and full localization resource application.
6. Complete packaging acceptance: publish artifacts, licenses, native dependency layout, icon strategy.
7. Resolve spec/document mismatches: XML `ChapterTimeEnd`, frame expression application, encoding/BOM contract, save path policy.
