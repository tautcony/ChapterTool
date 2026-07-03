## 1. Foundation And Contracts

- [x] 1.1 Create the SDK-style .NET 10 solution structure with Core, Infrastructure, Avalonia, and test projects.
- [x] 1.2 Add shared build settings, nullable/analyzer policy, and one unified version source.
- [x] 1.3 Add test fixture resolver and migrate existing sample files into stable test assets.
- [x] 1.4 Define Core models for chapters, chapter sets, source groups, source options, diagnostics, import/export results, and command result data.
- [x] 1.5 Define importer, exporter, settings, process runner, tool locator, dialog, clipboard, shell, window, localization, and platform service interfaces.
- [x] 1.6 Add project-boundary tests proving Core has no Avalonia, WinForms, or Windows-only dependencies.

## 2. Core Transform Export

- [x] 2.1 Add failing tests for time parsing/formatting, CUE timestamps, frame-rate options, automatic detection, and frame display markers.
- [x] 2.2 Implement time and frame-rate services until tests pass.
- [x] 2.3 Add failing tests for infix/postfix expressions, comments, `t`/`fps`, invalid expressions, and unsupported legacy operators.
- [x] 2.4 Implement expression parser/evaluator compatibility behavior until tests pass.
- [x] 2.5 Add failing tests for chapter edit operations: time edit, frame edit, rename, delete first row shift, insert row, order shift, template names, and auto names.
- [x] 2.6 Implement chapter editing and naming services until tests pass.
- [x] 2.7 Add golden snapshot tests for TXT, XML, QPF, TimeCodes, tsMuxeR meta, CUE, and JSON export.
- [x] 2.8 Implement exporters and encoding policies until snapshots pass.

## 3. Shared Infrastructure And Platform Services

- [x] 3.1 Add tests for legacy `chaptertool.json` and `color-config.json` migration.
- [x] 3.2 Implement typed settings, theme settings, and migration stores.
- [x] 3.3 Add tests for process runner stdout, stderr, exit code, timeout, cancellation, command, and working directory capture.
- [x] 3.4 Implement process runner and fakeable external tool locator contracts.
- [x] 3.5 Add tests for Windows-only unsupported behavior on non-Windows service stubs.
- [x] 3.6 Implement shell, privilege, file association, native dependency, clipboard, dialog, localization, and window service abstractions.
- [x] 3.7 Add tests for resource packaging and required icon/image availability.

## 4. Text XML Matroska VTT Importers

- [x] 4.1 Add failing OGM/TXT importer tests for existing sample, normalization, leniency, invalid first line, and partial parse.
- [x] 4.2 Implement OGM/TXT importer and diagnostics.
- [x] 4.3 Add failing WebVTT tests for existing sample, header validation, cue id skipping, malformed cue failure, and unsupported timing settings.
- [x] 4.4 Implement WebVTT importer and diagnostics.
- [x] 4.5 Add XML importer fixtures/tests for single/multiple editions, nested atoms, end boundaries, duplicate removal, invalid roots, and missing values.
- [x] 4.6 Implement XML/Matroska XML importer.
- [x] 4.7 Add Matroska adapter tests using fake mkvextract for missing tool, stdout XML, empty stdout, stderr, non-zero exit, timeout, cancellation, and path quoting.
- [x] 4.8 Implement Matroska importer adapter through process runner and XML importer.

## 5. CUE FLAC TAK Import Export

- [x] 5.1 Add failing CUE parser/import tests for supported encodings, existing samples, non-ASCII filename, performer suffix, timestamps, empty files, and malformed indexes.
- [x] 5.2 Implement CUE parser/importer and diagnostics.
- [x] 5.3 Add failing FLAC embedded CUE tests for invalid header, Vorbis cuesheet, missing cuesheet, native CUESHEET skip, and malformed comments.
- [x] 5.4 Implement FLAC embedded CUE reader.
- [x] 5.5 Add failing TAK embedded CUE tests for invalid header, marker scan, terminator, missing marker, and small-file behavior.
- [x] 5.6 Implement TAK embedded CUE scanner.
- [x] 5.7 Add CUE exporter snapshot tests for header, source filename option, track numbering, separator skip, auto names, BOM policy, and timestamp conversion.
- [x] 5.8 Implement CUE exporter behavior through the common exporter contract.

## 6. Disc Playlist Media Importers

- [x] 6.1 Add MPLS tests for existing samples, play item count, chapter count, source names, duration, fps, no marks fallback, invalid header, and multi-angle references.
- [x] 6.2 Implement MPLS importer, source media references, and append support.
- [x] 6.3 Add IFO/DVD tests for existing sample, BCD time conversion, PAL/NTSC fps, short-title filtering, invalid structure, and chapter numbering.
- [x] 6.4 Implement IFO/DVD importer and metadata isolation.
- [x] 6.5 Add XPL tests for valid synthetic sample, namespace, time base, missing duration, malformed time, and malformed XML.
- [x] 6.6 Implement XPL importer.
- [x] 6.7 Add BDMV/eac3to tests with fake process output for playlist listing, chapter export, missing dependency, unrecognized output, stderr, and metadata title reading.
- [x] 6.8 Implement BDMV importer through eac3to adapter and OGM parser.
- [x] 6.9 Add MP4 adapter tests for existing sample, title encodings, missing native dependency, native read failure, and no-chapter fallback.
- [x] 6.10 Implement MP4 importer through selected native or replacement adapter.
- [x] 6.11 Add combine/clip selection tests for MPLS/IFO offsets, unsupported combine sources, and related media references.
- [x] 6.12 Implement segment selection, combine, append, and source reference services.

## 7. Avalonia UI Shell And Auxiliary Windows

- [x] 7.1 Add ViewModel initialization and command availability tests for unloaded state.
- [x] 7.2 Implement main window ViewModel state and command surface with mocked services.
- [x] 7.3 Add load/save command tests for success, invalid path, service failure, dropped file, dropped directory, save options, custom directory, and save-and-advance.
- [x] 7.4 Implement load/save orchestration and status/progress updates.
- [x] 7.5 Add shortcut routing tests for global shortcuts, clip shortcuts, save-type shortcuts, and expression shortcuts.
- [x] 7.6 Implement Avalonia key bindings and context menu command routing.
- [x] 7.7 Add chapter grid ViewModel tests for editing, deleting, inserting, selection, and row refresh.
- [x] 7.8 Implement observable chapter row models and grid commands.
- [x] 7.9 Add tests for preview, log, color settings, about, updater, language switching, and file association entry points.
- [x] 7.10 Implement auxiliary windows/ViewModels and platform-service entry points.
- [x] 7.11 Add UI smoke tests for opening, closing, and reopening auxiliary windows.

## 8. Build Distribution Verification

- [x] 8.1 Add CI workflow for restore, build, and test on .NET 10.
- [x] 8.2 Add publish workflow or script for selected framework-dependent and self-contained artifacts.
- [x] 8.3 Decide and document MP4 dependency strategy and package behavior.
- [x] 8.4 Update NSIS scripts or document the replacement packaging strategy.
- [x] 8.5 Add package checks for required assets, native dependencies, licenses, and version consistency.
- [x] 8.6 Update README/release metadata to match Avalonia features, dependencies, and supported formats.
- [x] 8.7 Run full `dotnet test` and build/publish verification.

## 9. Modern UX And Cross-Platform Polish

- [x] 9.1 Update the Avalonia UI shell spec with responsive layout requirements, readable UTF-8 labels, localized grid edit routing, and non-primary registry-dependent actions.
- [x] 9.2 Rework the main window XAML to use modern responsive Avalonia panels while preserving the original workflow zones: top load/save and frame tools, central grid, bottom option rows, and status/progress strip.
- [x] 9.3 Keep normal load/save/drop/settings workflows on cross-platform services and remove always-visible file-association/registry controls from the main surface.
- [x] 9.4 Add or update static UI tests for responsive layout, UTF-8 text, no absolute Canvas positioning, and registry-gated main surface.
- [x] 9.5 Verify OpenSpec and the Avalonia UI test/build gates.
