## Why

Matroska chapter import currently depends on `mkvextract`, but runtime discovery is limited to configured paths and PATH-style search directories. The rewrite should restore the practical MKVToolNix installation discovery users expect on Windows and macOS while keeping command execution deterministic across platform encoding differences.

## What Changes

- Extend MKVToolNix/mkvextract discovery so a configured path remains first priority, then environment/PATH search, then platform-specific installation discovery.
- Add Windows discovery through MKVToolNix uninstall registry keys without making registry access part of Core.
- Add macOS discovery for versioned MKVToolNix app bundles such as `/Applications/MKVToolNix-96.0.app/Contents/MacOS/mkvextract`.
- Preserve Linux/Unix behavior through configured path and PATH search unless a later platform-specific discovery rule is designed.
- Make mkvextract process execution use explicit stdout/stderr decoding appropriate to each platform so chapter XML and diagnostics are not mojibake.
- Keep dependency failures structured and testable; no UI prompt should be raised from Core or Infrastructure.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `chapter-importers-text-xml-matroska-vtt`: Matroska import requires platform-aware mkvextract discovery and encoding-safe execution.
- `supporting-ui-platform-services`: External dependency location gains platform-specific discovery rules for MKVToolNix while preserving configured path precedence and structured missing-dependency results.
- `tests-build-distribution-assets`: Tests must cover cross-platform MKVToolNix discovery and process decoding behavior without requiring a real local MKVToolNix installation.

## Impact

- Affected code: `src/ChapterTool.Infrastructure/Tools`, `src/ChapterTool.Infrastructure/Processes`, `src/ChapterTool.Infrastructure/Importing/Matroska`, Avalonia composition, and tests under `tests/ChapterTool.Infrastructure.Tests` and `tests/ChapterTool.Avalonia.Tests`.
- Affected APIs: `IExternalToolLocator` remains unchanged; platform-specific discovery is implemented behind the existing locator interface.
- Affected platform behavior: Windows may read registry uninstall keys, macOS may scan `/Applications/*.app/Contents/MacOS`, and Unix-like systems continue to prefer PATH.
- Affected diagnostics: missing or unusable `mkvextract` remains a structured diagnostic, but messages should include which discovery sources were attempted when helpful.
