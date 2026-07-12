## ADDED Requirements

### Requirement: Disc binary parsers bound untrusted allocations
MPLS and shared disc binary helpers SHALL reject untrusted length or count fields that would allocate or iterate beyond documented finite limits. Each declared-length container SHALL validate its mandatory header, entries, and final position before allocation, iteration, or skip. Parsers MUST NOT allocate multi-gigabyte buffers, preallocate unchecked collections, or seek backwards solely because a corrupted or malicious field claims that size or count.

#### Scenario: Oversized exact-read length fails
- **WHEN** a binary playlist field instructs the parser to read a byte length above the configured maximum for that read helper or structure
- **THEN** the parser SHALL throw a structured parse failure (`InvalidDataException` or equivalent import failure path)
- **AND** it SHALL NOT allocate a buffer of that oversized length

#### Scenario: Valid MPLS within bounds still imports
- **WHEN** a valid MPLS playlist within normal Blu-ray structural sizes is imported
- **THEN** chapter segments SHALL continue to import according to existing MPLS requirements

#### Scenario: Extension data and mark tables respect limits
- **WHEN** MPLS extension-data block lengths or mark/table counts exceed their configured limits
- **THEN** parsing SHALL fail closed with an invalid-data diagnostic rather than continuing with unbounded allocation

#### Scenario: Playlist and subpath counts respect limits
- **WHEN** MPLS play-item, subpath, stream-table, or nested-entry counts exceed their configured limits
- **THEN** the parser SHALL fail with an invalid-data diagnostic before preallocating or iterating an oversized collection

#### Scenario: Declared container length cannot contain consumed entries
- **WHEN** a MPLS container declares a length smaller than its mandatory header or entries already consumed
- **THEN** parsing SHALL fail with an invalid-data diagnostic
- **AND** it SHALL NOT seek backwards or continue by skipping a negative remainder

### Requirement: XPL XML import uses secure XML loading
XPL playlist import SHALL load XML with the same class of secure settings as Matroska XML import: DTD processing prohibited and no external entity resolution.

#### Scenario: XPL with hostile external entity declarations fails closed
- **WHEN** an XPL document relies on external entity resolution to inject content
- **THEN** import SHALL fail closed or ignore unresolved external entities without fetching them
- **AND** a normal valid XPL without external entities SHALL continue to import per existing XPL requirements
