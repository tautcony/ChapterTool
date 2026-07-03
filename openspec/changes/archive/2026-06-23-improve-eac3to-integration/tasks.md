## 1. BDMV eac3to Implementation

- [x] 1.1 Update `BdmvChapterImporter` to export chapter text for each chapter-bearing eac3to title candidate through a temporary file.
- [x] 1.2 Parse exported chapter text through the OGM importer and map the result back to BDMV chapter metadata and source options.
- [x] 1.3 Preserve structured diagnostics for invalid structure, missing dependency, eac3to list/export failures, missing export files, unparseable exports, and no parsed candidates.

## 2. Verification

- [x] 2.1 Add or update hermetic infrastructure tests with fake eac3to process output for successful export, metadata preservation, and export failure paths.
- [x] 2.2 Run OpenSpec validation and focused infrastructure tests.
- [x] 2.3 Manually verify against a local eac3to installation and local BDMV sample without recording those machine-local paths in specs or automated tests.

## 3. Load Progress

- [x] 3.1 Propagate optional load progress from the Avalonia load service into importer requests.
- [x] 3.2 Report staged BDMV/eac3to progress for validation, tool discovery, listing, export, and parsing.
- [x] 3.3 Cover intermediate progress updates with behavior tests.
