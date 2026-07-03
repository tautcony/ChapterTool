## Context

The current Matroska importer already delegates `.mkv` and `.mka` chapter extraction to `mkvextract` through `IExternalToolLocator` and `IProcessRunner`. `ExternalToolLocator` currently checks a configured path from migrated settings and injected search directories, which normally come from `PATH`. The legacy WinForms implementation also searched Windows registry uninstall keys for MKVToolNix, and users on macOS commonly install MKVToolNix as a versioned `.app` bundle whose executables live under `Contents/MacOS`.

This change should improve discovery and execution while preserving the existing clean boundaries: Core remains UI/platform independent, Infrastructure owns platform probes and process execution, and Avalonia composition wires concrete services.

## Goals / Non-Goals

**Goals:**

- Keep configured MKVToolNix path as the highest-priority source.
- Search environment/PATH directories before platform-specific install discovery.
- Discover `mkvextract` on Windows from MKVToolNix uninstall registry keys.
- Discover `mkvextract` on macOS from application bundles such as `/Applications/MKVToolNix-96.0.app/Contents/MacOS/mkvextract`.
- Preserve Linux and other Unix-like behavior through configured path and PATH search.
- Decode external process stdout/stderr explicitly enough that chapter XML and diagnostics are stable across Windows and Unix terminals.
- Keep tests deterministic without requiring MKVToolNix to be installed on the developer or CI machine.

**Non-Goals:**

- Add a new UI for selecting or downloading MKVToolNix.
- Change Matroska chapter parsing semantics after XML is returned.
- Require a real installed MKVToolNix in automated tests.

## Decisions

### Keep `IExternalToolLocator` as the public boundary

The existing Core interface is already sufficient: callers ask for `mkvextract` and receive an `ExternalToolLocation`. The implementation can gain internal discovery sources without forcing Matroska importer or UI code to know about registries, app bundles, or PATH details.

Alternatives considered:

- **Add an `IMkvToolNixLocator` Core interface:** More explicit, but it leaks a specific dependency into Core and duplicates the existing locator contract.
- **Hard-code discovery in `MatroskaChapterImporter`:** Simpler in one file, but couples importer behavior to platform probing and makes tests less focused.

### Split discovery into ordered Infrastructure probes

`ExternalToolLocator` should preserve this order for `mkvextract`:

1. Configured path from settings.
2. Environment/PATH search directories.
3. Windows MKVToolNix registry install locations.
4. macOS MKVToolNix `.app` bundle executable locations.

The platform-specific probes should be narrow and injectable or otherwise isolated for tests. Windows registry probing should be guarded by `OperatingSystem.IsWindows()`. macOS app probing should prefer deterministic directory enumeration under injectable application roots, with `/Applications` as the production root.

### Treat configured paths as file or directory values

The migrated setting may point either to the executable itself or to the MKVToolNix install directory. Directory values should be expanded to the platform executable name (`mkvextract.exe` on Windows, `mkvextract` elsewhere). File values should be used directly when they exist.

### Decode process output explicitly

`mkvextract` emits XML on stdout and diagnostics on stderr. Process execution should set output encodings explicitly instead of relying on terminal defaults. UTF-8 should be the default because MKVToolNix chapter XML is expected to be UTF-8. Windows support should register code pages where needed and allow tests to verify non-ASCII stdout/stderr are decoded correctly. If the implementation adds per-request encoding options, existing callers should continue to get the current default behavior unless they opt in.

### Keep diagnostics structured

Missing discovery remains `MissingDependency` from the locator and `MatroskaMissingDependency` from the importer. Process failures continue to include command metadata. Discovery diagnostics may include attempted sources, but tests should assert stable diagnostic codes rather than full machine-specific messages.

## Risks / Trade-offs

- Windows registry layouts vary by installer architecture and version -> probe both HKLM/HKCU and 32-bit/64-bit uninstall locations, and keep PATH/configured path ahead of registry.
- macOS bundle names include versions and may coexist -> choose the newest-looking candidate deterministically when multiple valid bundles exist, or document the ordering rule in tests.
- Encoding defaults can differ between .NET runtimes and OS locales -> explicitly set process output encodings and cover non-ASCII output in tests.
- Registry and `/Applications` scanning are hard to test directly -> introduce small Infrastructure seams so tests can supply fake registry values and app directories.
