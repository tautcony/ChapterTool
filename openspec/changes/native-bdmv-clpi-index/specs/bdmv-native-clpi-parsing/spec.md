## ADDED Requirements

### Requirement: CLPI file parsing
The system SHALL parse Blu-ray `.clpi` (Clip Information) files to extract STC sequence timing, program stream metadata, and entry point maps. CLPI files SHALL be automatically discovered from the BDMV directory structure based on the MPLS file path and clip names; manual specification SHALL NOT be required. A missing or unparseable CLPI file SHALL NOT cause parsing failure or interruption.

#### Scenario: CLPI is auto-discovered from BDMV structure
- **WHEN** an MPLS file at path `.../BDMV/PLAYLIST/nnnnn.mpls` references clip `00001`
- **THEN** the importer SHALL attempt to find and parse `.../BDMV/CLIPINF/00001.clpi` automatically
- **AND** no user action SHALL be required to enable CLPI-based timing enhancement

#### Scenario: CLPI auto-discovery is silent when outside BDMV structure
- **WHEN** an MPLS file is loaded from a path that is not within a BDMV directory tree
- **THEN** CLPI discovery SHALL be skipped silently without producing any diagnostic
- **AND** MPLS chapter import SHALL proceed with its existing PTS accumulation algorithm

#### Scenario: Missing CLPI within BDMV structure degrades gracefully
- **WHEN** the BDMV root is successfully identified but a CLPI file for a specific clip is missing or unparseable
- **THEN** chapter import SHALL continue using INTime/OUTTime PTS delta accumulation for that clip
- **AND** an info-level diagnostic SHALL be recorded noting the unavailable CLPI

#### Scenario: STC metadata remains available for packet lookup
- **WHEN** CLPI SequenceInfo is available for a PlayItem's referenced clip and STC sequence
- **THEN** the STC sequence's PresentationStartTime, SPNSTCStart, and EP map records SHALL remain accessible for packet lookup
- **AND** chapter timestamps SHALL use MPLS INTime, OUTTime, and mark timestamps without adding PresentationStartTime
- **AND** chapter timestamps SHALL remain consistent with the existing PTS/45000 conversion contract

#### Scenario: Valid CLPI extracts ClipInfo
- **WHEN** a valid `.clpi` file is parsed
- **THEN** ClipInfo (ClipStreamType, ApplicationType, TSRecordingRate, NumberOfSourcePackets) SHALL be accessible as structured records
- **AND** DurationFromPackets SHALL derive a TimeSpan from NumberOfSourcePackets and TSRecordingRate

#### Scenario: Valid CLPI extracts SequenceInfo
- **WHEN** a valid `.clpi` file with SequenceInfo is parsed
- **THEN** ATC sequences and their nested STC sequences SHALL be accessible
- **AND** each STC sequence SHALL expose PCRPID, SPNSTCStart, PresentationStartTime, and PresentationEndTime
- **AND** FindSTCSequence(byte stcId) SHALL locate the correct STC sequence across all ATC sequences

#### Scenario: Valid CLPI extracts ProgramInfo
- **WHEN** a valid `.clpi` file with ProgramInfo is parsed
- **THEN** program sequences with their StreamPIDs and StreamCodingInfo entries SHALL be accessible
- **AND** StreamCodingInfo SHALL expose VideoFormat, FrameRate, VideoAspect, AudioFormat, SampleRate, and LanguageCode where applicable

#### Scenario: Valid CLPI extracts CPI
- **WHEN** a valid `.clpi` file with a non-empty CPI section is parsed
- **THEN** EP stream entries and their coarse/fine EP map entries SHALL be accessible
- **AND** an empty CPI section (Length=0) SHALL produce an empty entry list without error

#### Scenario: CLPI bounds reject oversized inputs
- **WHEN** a CLPI file declares section lengths or entry counts exceeding configured finite limits
- **THEN** parsing SHALL fail closed with a structured parse failure rather than allocating unbounded buffers or iterating oversized collections
