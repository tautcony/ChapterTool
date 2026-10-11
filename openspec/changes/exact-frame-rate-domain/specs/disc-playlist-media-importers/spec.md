## ADDED Requirements

### Requirement: Importers preserve source-driven frame-rate authority
Importers SHALL report Rational rates only when source video metadata or existing format interpretation establishes that rate. Timestamp clocks and display matches SHALL NOT establish video FPS. Import projections SHALL carry canonical rates without a double round-trip.

#### Scenario: MPLS code maps to exact rate
- **WHEN** an MPLS play item contains a recognized primary-video rate code
- **THEN** its import projection SHALL retain the catalog's reduced rational rate

#### Scenario: Unknown MPLS code remains unknown
- **WHEN** an MPLS play item has an absent, reserved, or unrecognized video rate code
- **THEN** it SHALL retain Unknown
- **AND** it SHALL NOT infer FPS from chapter positions

#### Scenario: Aggregate MPLS contains mixed rates
- **WHEN** an MPLS or BDMV playlist contains differing or missing applicable play-item rates
- **THEN** adaptation SHALL preserve per-item rates and boundaries
- **AND** a scalar aggregate SHALL NOT claim the first recognized rate for the playlist

#### Scenario: Primary video metadata conflicts
- **WHEN** applicable primary-video metadata supplies conflicting rates
- **THEN** the importer SHALL retain uncertainty and report a diagnostic where that conflict affects projection
- **AND** it SHALL NOT silently use the first stream's rate

#### Scenario: IFO rate authority is independent of timing source
- **WHEN** a supported IFO title uses the existing NTSC or PAL interpretation
- **THEN** it SHALL report Rational 30000/1001 or Rational 25/1 respectively
- **AND** IFO versus NAV timing SHALL preserve that rate and the original imported timestamp calculation

#### Scenario: Source clock differs from video rate
- **WHEN** XPL timeBase/tickBase, CUE 75-unit timing, or a media time_base is available without video-rate metadata
- **THEN** the chapter video rate SHALL be Unknown
- **AND** the importer SHALL NOT assign the clock scale or a hard-coded 24 FPS as source video rate

#### Scenario: Numeric video metadata lacks rational authority
- **WHEN** a source explicitly supplies only a decimal video rate
- **THEN** the importer SHALL mark it Approximate
- **AND** document adaptation SHALL retain its value and kind

#### Scenario: Import timestamps are not corrected by rate metadata
- **WHEN** an importer assigns canonical rate metadata
- **THEN** it SHALL preserve the established source timing calculation
- **AND** it SHALL NOT snap chapters to frames or compensate drift
