## Why

BDMV import currently has only a minimal eac3to adapter contract and needs to handle real disc-folder output more robustly. This change completes the eac3to-backed path so users can load BDMV sources without relying on hard-coded assumptions or test-only fixtures.

## What Changes

- Improve BDMV eac3to import to discover playable titles from eac3to output and import the selected/default playlist chapter text.
- Preserve structured diagnostics for missing tools, invalid BDMV structure, process failures, empty title lists, and chapter parse failures.
- Surface intermediate load progress in the Avalonia shell while long-running importers report staged work.
- Keep external executable paths and sample disc locations as runtime configuration or manual verification inputs only; do not embed machine-local paths in specs or tests.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `disc-playlist-media-importers`: strengthen BDMV/eac3to import behavior for real eac3to title listing and chapter extraction.
- `avalonia-ui-shell`: update the main-window load progress bar from importer-reported intermediate progress.

## Impact

- Affected code: BDMV importer, eac3to process invocation/parsing, external tool diagnostics, and related importer tests.
- No new external dependencies are required.
- Manual verification may use a locally installed eac3to and local BDMV sample, but automated tests must remain hermetic with fake process runners.
