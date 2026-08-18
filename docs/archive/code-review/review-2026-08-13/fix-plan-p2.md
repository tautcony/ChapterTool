# P2/P3 Fix Plan (review 2026-08-13)

Status: implemented for remaining review findings that have a local, testable fix.

## Medium findings

| ID | Fix |
|----|-----|
| CORE-08 | OGM timestamps earlier than the first chapter clamp to zero and add `PartialParse`. |
| CORE-09 | Text importers decode with a strict UTF-8 pass, then a permissive fallback plus `TextEncodingFallback`. |
| CORE-10 | XML import resets the content stream only when `CanSeek` is true. |
| CORE-11 | FLAC reads use exact block reads and stop on the last-block flag. |
| CORE-12 | MPLS and FLAC dispose only streams they open. |
| CORE-15 | IFO PGC offsets use unsigned 16-bit values. |
| INFRA-04 | Windows Explorer receive `/select,path` as one argument. |
| INFRA-05 | `OpenAsync` logs start failures and does not throw. |
| INFRA-06 | BDMV scan treats `UnauthorizedAccessException` as a diagnostic. |
| HOST-03 | `ChapterService` and dependents live in one file in declaration order. |
| HOST-04 | Export encoding accepts settings ids and enum names. |
| HOST-05 | Empty Node import returns `EMPTY_INPUT`. |
| UI-05 | MainView attach/detach subscribe in pairs. Detach does not clear `Content`. |

## Selected low findings

| ID | Fix |
|----|-----|
| CORE-16 | `EditTime` warns when a time of 24 hours or more is reset. |
| CORE-17 | Unused FLAC/TAK parser fields removed. |
| CORE-18 | Reserved MPLS frame-rate codes return 0. |
| INFRA-07 | MKVToolNix probe ignores inaccessible application directories. |
| INFRA-08 | Settings write flushes to disk before replace. |
| INFRA-09 | A failed corrupt-file move still returns `CorruptSettingsFileException`. |
| INFRA-10 | Matroska importer lists `.mks` and `.webm`. |
| INFRA-11 | Unused `DotNetHost` removed. |
| INFRA-12 | Log service events have no silent default add/remove. |
| INFRA-13 | Missing-tool messages include a rejected configured path. |
| UI-06 | Main window unsubscribes `CultureChanged` in `Dispose`. |
| UI-07 | Shortcut router no longer maps `Ctrl+O`. |
| UI-08 | Settings load catches `CorruptSettingsFileException` by type. |
| TEST-01 | Full-disc parity uses `Assert.Skip` and `CHAPTERTOOL_FULL_DISC_ROOT`. |
| TEST-02 | Missing ffprobe skips the integration test. |
| TEST-03 | Headless theme tests no longer wrap `Dispatcher.UIThread.Invoke`. |
| TEST-04 | CLI convert covers missing path, unknown extension, and MPLS. |
| TEST-05 | Wasm append has a real MPLS merge test. |
| TEST-06 | Zones require a positive frame rate before assertion. |
| TEST-08 | Matroska integration deletes its temp settings directory. |
| TEST-09 | Wasm expression diagnostic match no longer accepts any message. |
| TEST-10 | Unused `tests/MplsVerify` and empty Avalonia test folders removed. |
| TEST-11 | Settings Headless path uses a GUID folder name. |

## Later leftovers

These items are now implemented:

| ID | Fix |
|----|-----|
| CORE-19 | `AllocateUniqueFilePath` takes an injected `fileExists` probe. Desktop hosts pass `File.Exists`. |
| UI-09 | `FormatBox` binds `SaveFormatOptions` from `ChapterExportFormats`. |
| HOST-06 | Explicit `--output` refuses to overwrite unless `--force` is set. |
| HOST-07 | Out-of-range `--group-index` uses `Cli.Error.GroupIndex`. Unspecified multi-group input still uses `Cli.Error.MultipleGroups`. |
| HOST-08 | `--frame-rate` requires a finite value greater than zero. |
| HOST-09 | CLI and Wasm share `UiLanguageCode`. Unrecognized `--language` writes a warning. The localizer does not rewrite thread culture. |
| HOST-10 | Wasm order-shift input minimum is 0. |
| HOST-11 | JS `localStorage` access is inside try/catch. First render also catches `JSException`. |
| HOST-12 | The empty `chapterNameModeIndex` block is removed. |
| HOST-13 | `LoadFileAsync` reads into a pre-sized byte array. |
| HOST-14 | Wasm drop-zone limit comes from `WasmWorkspace.MaxLoadBytes`. Node reads `NodeApi.GetMaxInputBytes`. |
| HOST-15 | `package.json` has top-level `main`/`types` and `require`/`default` export conditions. |
| HOST-16 | Hosts default to UTF-8 without a BOM. CLI accepts `--encoding` and `--bom`. |
| TEST-07 | `tests/ChapterTool.TestSupport` owns repository-root and logger helpers. |
