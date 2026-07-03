## 1. Reader Contract and Test Baseline

- [x] 1.1 Add focused Core tests proving `Mp4ChapterImporter` converts reader-provided durations into cumulative chapter starts for `.mp4`, `.m4a`, and `.m4v`.
- [x] 1.2 Add Core tests for empty MP4 reader results producing `NoChaptersFound` and failed reader results preserving diagnostic codes.
- [x] 1.3 Confirm `IMp4ChapterReader`, `Mp4ChapterReadResult`, and `Mp4ChapterClip` expose enough data for an ATL-backed reader without leaking library-specific types into Core.

## 2. ATL.NET MP4 Reader Adapter

- [x] 2.1 Add the ATL.NET NuGet dependency (`z440.atl.core`) to the Infrastructure project.
- [x] 2.2 Add an Infrastructure MP4 reader implementation (`AtlMp4ChapterReader`) that uses ATL.NET to read MP4-family chapter metadata and returns `Mp4ChapterReadResult`.
- [x] 2.3 Normalize ATL chapter metadata into ordered `Mp4ChapterClip` duration entries, including start/end-to-duration conversion when needed.
- [x] 2.4 Catch ATL/file parsing exceptions and convert invalid, inaccessible, unsupported, or malformed files into structured diagnostics.
- [x] 2.5 Add reader tests using committed MP4-family fixtures or a narrow ATL abstraction seam for valid chapters, Unicode names, fractional timing, unsupported metadata, malformed metadata, and empty chapter output.

## 3. Native Dependency Retirement

- [x] 3.1 Remove production MP4 import dependency on `INativeDependencyService` and `libmp4v2` lookup.
- [x] 3.2 Update or remove tests that currently expect MP4 import to fail because `libmp4v2` is missing (e.g., `Mp4ImporterReturnsReaderDiagnostics` with `"NativeLibraryMissing"` / `"NativeReadFailed"` codes).
- [x] 3.3 Keep external tool locator behavior unchanged for `mkvextract` and `eac3to`.
- [x] 3.4 Verify project references: Core and Avalonia projects SHALL NOT reference ATL.NET (`z440.atl.core`), and ATL types SHALL NOT appear in Core or Avalonia `using` directives.
- [x] 3.5 Remove `libmp4v2` lookup logic from `FileSystemNativeDependencyService` once no production caller uses it; keep the service's other dependency checks intact.

## 4. Runtime Wiring

- [x] 4.1 Update `RuntimeChapterImporterRegistry` to accept `IMp4ChapterReader` as a constructor parameter and pass it to `Mp4ChapterImporter` for `.mp4`/`.m4a`/`.m4v` extensions.
- [x] 4.2 Update `AppCompositionRoot` to construct `AtlMp4ChapterReader` and inject it into the registry; update registry tests to prove `.mp4`, `.m4a`, and `.m4v` route through the ATL reader-backed importer.
- [x] 4.3 Remove `MissingMp4ChapterReader` from the production codebase; if retained for diagnostic tests, move it to the test project and never wire it in `AppCompositionRoot`.
- [x] 4.4 Add runtime load service tests proving successful MP4 reader output imports chapters and ATL reader failures surface as diagnostics.

## 5. Documentation and Packaging

- [x] 5.1 Update `docs/packaging-strategy.md` to document ATL.NET as the managed MP4 reader dependency and state that no separate MP4 CLI or `libmp4v2` DLL is required for the default path. Remove references to `NativeLibraryMissing` as a normal MP4 diagnostic.
- [x] 5.2 Update `BuildPackagingTests` assertions to match the revised `packaging-strategy.md` content (ATL.NET mentions replace legacy `NativeLibraryMissing` / `libmp4v2` references).
- [x] 5.3 Add or update packaging tests to assert publish output does not include legacy `Time_Shift/mp4v2` native DLLs by default.
- [x] 5.4 Document that legacy `libmp4v2`/Knuckleball, external MP4 CLI experiments, and `fsutil` hardlink behavior remain retired unless a future backend is explicitly designed.

## 6. Verification

- [x] 6.1 Run `dotnet test tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj --no-restore`.
- [x] 6.2 Run `dotnet test tests/ChapterTool.Infrastructure.Tests/ChapterTool.Infrastructure.Tests.csproj --no-restore`.
- [x] 6.3 Run `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore`.
- [x] 6.4 Run `dotnet test ChapterTool.Avalonia.slnx --no-restore` before finalizing the implementation.
- [x] 6.5 Validate the change with `openspec validate support-cross-platform-mp4-chapters --strict`.
