## MODIFIED Requirements

### Requirement: Matroska container import
The system SHALL import `.mkv` and `.mka` chapters through an encoding-safe mkvextract adapter resolved by platform-aware MKVToolNix discovery.

#### Scenario: mkvextract stdout delegates to XML importer
- **WHEN** mkvextract returns valid chapter XML
- **THEN** the Matroska importer SHALL parse stdout through the XML importer and preserve multiple editions

#### Scenario: External process failure is structured
- **WHEN** mkvextract is missing, exits non-zero, writes relevant stderr, times out, or returns empty stdout
- **THEN** the importer SHALL return a dependency/process diagnostic containing process metadata

#### Scenario: Platform discovery supplies mkvextract
- **WHEN** no configured MKVToolNix path exists but mkvextract is discoverable through the platform tool locator
- **THEN** Matroska import SHALL execute the discovered mkvextract executable without requiring UI input

#### Scenario: Non-ASCII mkvextract output is decoded
- **WHEN** mkvextract writes chapter XML or diagnostics containing non-ASCII text
- **THEN** the importer SHALL receive decoded stdout/stderr text without mojibake caused by platform terminal defaults
