# Spec Implementation Verification

Change: `rewrite-avalonia-dotnet10`

Verification date: 2026-06-07

## Scope

This document records the module-by-module verification pass after the Avalonia executable was found to exit immediately. The pass checks OpenSpec requirements against current implementation, automated tests, and practical GUI runtime checks.

## Verification Commands

```powershell
dotnet build ChapterTool.Avalonia.slnx --no-restore
dotnet test ChapterTool.Avalonia.slnx --no-restore
openspec validate rewrite-avalonia-dotnet10 --strict
```

GUI process check:

```powershell
src/ChapterTool.Avalonia/bin/Debug/net10.0/ChapterTool.Avalonia.exe
```

The Debug executable was launched and stayed alive beyond the startup window check, then closed normally.

## Module Status

| Spec module | Implementation status | Verification coverage |
| --- | --- | --- |
| `tests-build-distribution-assets` | Largely consistent for solution topology, project separation, migrated fixtures, CI restore/build/test, version source, and executable smoke coverage. Publish packaging remains script/documentation-level, not full artifact runtime acceptance. | `ProjectBoundaryTests`, `InfrastructureBoundaryTests`, `ResourcePackagingTests`, `BuildPackagingTests`, `ExecutableLaunchTests`, full `dotnet test`, OpenSpec strict validate. |
| `chapter-core-transform-export` | Consistent for time/frame parsing, expression service, editing, naming, core exporters, and segment combine. Follow-up fixes added mixed-source combine rejection, MPLS append service behavior, and frame edit display refresh. | `ChapterEditingServiceTests`, `ChapterSegmentServiceTests`, exporter snapshot tests, expression tests. |
| `chapter-importers-text-xml-matroska-vtt` | Consistent for OGM/TXT, WebVTT, XML, and Matroska adapter behavior. Follow-up fixes made dangling OGM time a partial parse and added coverage. Runtime GUI now routes `.mkv/.mka` into the Matroska importer. | `TextImporterTests`, Matroska infrastructure tests, `RuntimeChapterLoadServiceTests`. |
| `cue-flac-tak-import-export` | Consistent for CUE, FLAC embedded CUE, TAK embedded CUE, and CUE export. Follow-up fixes TAK marker scanning so byte offsets remain correct after non-ASCII padding. | `CueImporterTests`, CUE exporter tests. |
| `disc-playlist-media-importers` | Consistent for MPLS, IFO, XPL, BDMV adapter, MP4 importer contract, segment combine, and runtime routing. MP4 remains dependency-strategy based: runtime routes to MP4 importer and reports structured native-reader diagnostics rather than unsupported source. | `DiscImporterTests`, `BdmvChapterImporterTests`, `RuntimeChapterLoadServiceTests`, `ChapterSegmentServiceTests`. |
| `avalonia-ui-shell` | Now materially implemented for a real Avalonia app host, main window, editable `DataGrid`, progress display, clip panel visibility, advanced options, context menu entries, drag/drop path load, keyboard routing, and automation ids. Some shortcut names in spec remain ViewModel/router tested rather than full UIA-tested. | `MainWindowViewModelTests`, `MainWindowXamlTests`, `ExecutableLaunchTests`, manual Debug exe launch. |
| `supporting-ui-platform-services` | Partially consistent. Typed settings, process runner, external dependency location, native dependency checks, resource packaging, and reusable Avalonia auxiliary window entry points exist. Preview/log/color/about/updater windows are currently entry-point windows, not full feature-specific implementations with preview text, log copy, updater behavior, or full settings UI. | `SettingsMigrationTests`, `PlatformServiceTests`, `ExternalToolLocatorTests`, `ResourcePackagingTests`, `MainWindowViewModelTests`, `MainWindowXamlTests`. |

## GUI Validation Method

Current automated GUI validation has three layers:

1. ViewModel workflow tests cover command availability, load/save orchestration, shortcut routing, clip selection, row edits, and auxiliary-window command dispatch.
2. XAML structure tests cover the real `MainWindow.axaml` surface: `DataGrid`, context menu, progress bar, advanced options, append entry point, and stable automation ids.
3. Executable smoke tests launch the actual Avalonia executable, verify `--smoke-test` exits cleanly, and verify the normal GUI process does not exit immediately.

Stable automation ids are documented in `docs/gui-verification.md`. A future UIA/FlaUI pass should use those ids to click through load, edit, save, context menu, drag/drop, and auxiliary-window workflows in the packaged executable.

## Residual Gaps

- Auxiliary windows need feature-specific contents and behavior to fully satisfy preview/log/color/about/updater scenarios.
- Full GUI automation should move from process-lifetime and XAML-structure checks to UIA/FlaUI interaction tests against the packaged Windows artifact.
- Publish verification currently checks scripts and metadata. It should also run publish outputs and verify assets, licenses, and native dependency layout in produced artifacts.
