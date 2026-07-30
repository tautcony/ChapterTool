## ADDED Requirements

### Requirement: MPLS CLPI-enhanced import
The system SHALL enhance MPLS chapter import with automatic CLPI discovery from the BDMV directory structure. When an MPLS file resides within a BDMV tree, the importer SHALL locate and parse corresponding CLPI files for STC, entry-point, and stream metadata. CLPI data SHALL be an optional enhancement; a missing or unparseable CLPI file SHALL NOT cause parsing failure or interruption.

#### Scenario: CLPI is auto-discovered from MPLS path
- **WHEN** an MPLS file at path `.../BDMV/PLAYLIST/nnnnn.mpls` references clip `00001`
- **THEN** the importer SHALL derive the BDMV root by walking up the directory tree
- **AND** it SHALL attempt to find and parse `.../BDMV/CLIPINF/00001.clpi` automatically
- **AND** no user action SHALL be required

#### Scenario: CLPI auto-discovery is silent when outside BDMV structure
- **WHEN** an MPLS file is loaded from a path that is not within a BDMV directory tree
- **THEN** CLPI discovery SHALL be skipped silently without producing any diagnostic
- **AND** MPLS chapter import SHALL proceed with its existing PTS accumulation algorithm

#### Scenario: STC metadata does not shift playlist time
- **WHEN** CLPI data is available for a clip and its STC sequence
- **THEN** the STC sequence's PresentationStartTime SHALL remain available for packet lookup
- **AND** chapter time calculation SHALL use MPLS INTime, OUTTime, and mark timestamps
- **AND** chapters SHALL use the PTS/45000 conversion as in existing behavior

### Requirement: Standard BDMV importer contract for directories
The system SHALL route BDMV directory paths to the native C# importer. The existing eac3to-based importer SHALL remain available.

#### Scenario: BDMV directory routes to native importer
- **WHEN** the import service receives a path containing a `BDMV/PLAYLIST` subdirectory
- **THEN** it SHALL route to NativeBdmvImporter (not the eac3to-based importer)

#### Scenario: Single MPLS file within BDMV still works standalone
- **WHEN** a user loads a single `.mpls` file that resides within a BDMV tree
- **THEN** the MPLS importer SHALL handle it as a standalone file
- **AND** CLPI auto-discovery SHALL still activate because the path is within a BDMV structure
