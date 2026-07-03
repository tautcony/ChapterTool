## 1. Discovery Contract and Test Baseline

- [x] 1.1 Add or update Infrastructure tests proving configured MKVToolNix directory and executable paths still take precedence for `mkvextract`.
- [x] 1.2 Add or update Infrastructure tests proving PATH/search-directory discovery wins before platform install discovery.
- [x] 1.3 Add deterministic tests for Windows MKVToolNix registry uninstall discovery using a fake registry/platform probe rather than the real machine registry.
- [x] 1.4 Add deterministic tests for macOS MKVToolNix `.app` bundle discovery using fake application roots such as `MKVToolNix-96.0.app/Contents/MacOS/mkvextract`.
- [x] 1.5 Add missing-tool tests that verify structured `MissingDependency` results include stable codes and do not require UI prompts.

## 2. Platform Discovery Implementation

- [x] 2.1 Introduce a narrow Infrastructure-only discovery seam for platform-specific MKVToolNix install locations, keeping Core interfaces unchanged.
- [x] 2.2 Update `ExternalToolLocator` to preserve lookup order: configured path, environment/PATH search directories, then platform install discovery.
- [x] 2.3 Implement Windows MKVToolNix discovery behind an `OperatingSystem.IsWindows()` guard, probing HKLM/HKCU and 32-bit/64-bit uninstall locations for install paths or display icons.
- [x] 2.4 Implement macOS MKVToolNix bundle discovery behind an `OperatingSystem.IsMacOS()` guard, resolving executables under `Contents/MacOS`.
- [x] 2.5 Ensure Unix/Linux behavior remains configured path plus PATH/search directories unless a future discovery provider is explicitly added.

## 3. Process Encoding and Matroska Integration

- [x] 3.1 Add process runner tests covering non-ASCII stdout and stderr decoding without relying on terminal defaults.
- [x] 3.2 Update `ProcessRunner` to set explicit redirected output encodings, registering code pages on Windows if required by the implementation.
- [x] 3.3 Add Matroska importer tests proving non-ASCII mkvextract stdout XML is parsed into chapter names without mojibake.
- [x] 3.4 Add Matroska importer tests proving non-ASCII stderr diagnostics remain readable in structured process failure diagnostics.
- [x] 3.5 Verify `MatroskaChapterImporter` continues to pass argument-list based commands (`chapters`, source path) and does not reintroduce shell quoting or UI prompts.

## 4. Composition, Documentation, and Boundaries

- [x] 4.1 Update Avalonia composition only if new Infrastructure discovery dependencies require explicit construction.
- [x] 4.2 Add boundary tests proving Core remains free of registry, filesystem discovery, MKVToolNix app-bundle, and process encoding implementation details.
- [x] 4.3 Update packaging/dependency documentation to describe MKVToolNix discovery order and platform-specific install probes.

## 5. Verification

- [x] 5.1 Run `dotnet test tests/ChapterTool.Infrastructure.Tests/ChapterTool.Infrastructure.Tests.csproj --no-restore`.
- [x] 5.2 Run `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore` if composition or registry wiring changes.
- [x] 5.3 Run `dotnet test ChapterTool.Avalonia.slnx --no-restore`.
- [x] 5.4 Validate the change with `openspec validate support-mkvtoolnix-platform-discovery --strict`.
