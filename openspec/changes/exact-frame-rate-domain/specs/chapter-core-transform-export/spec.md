## ADDED Requirements

### Requirement: Core frame calculations use canonical rates
Core frame calculations SHALL consume canonical rate values. Legacy numeric FPS members SHALL serve only public boundary adaptation or presentation. Internal calculations SHALL NOT read them.

#### Scenario: Rational conversion stays exact until rounding
- **WHEN** an operation uses Rational 24000/1001
- **THEN** it SHALL calculate from timestamp ticks and integer numerator/denominator products
- **AND** it SHALL round only at the declared frame or tick boundary

#### Scenario: Approximate arithmetic retains provenance
- **WHEN** an operation decomposes Approximate 23.976 into a decimal coefficient and scale for integer arithmetic
- **THEN** the numerical calculation SHALL preserve that decimal value until output rounding
- **AND** the result rate SHALL remain Approximate
- **AND** the arithmetic ratio SHALL NOT become authoritative source metadata

#### Scenario: Mixed exact and approximate transform
- **WHEN** an FPS transform has one Rational rate and one Approximate rate
- **THEN** it SHALL preserve both input values through the calculation
- **AND** it SHALL NOT approximate the Rational input to share a decimal path

#### Scenario: Final value exceeds supported bounds
- **WHEN** a frame or tick conversion exceeds signed Int64 bounds
- **THEN** the operation SHALL return an overflow diagnostic
- **AND** it SHALL leave content, history, and output targets unchanged

### Requirement: Computation rates are resolved explicitly
An FPS-dependent operation SHALL use an explicit caller rate or the uniform known rate of its target scope. Detection and display suggestions SHALL NOT supply an implicit computation rate.

#### Scenario: Unknown or mixed scope has no override
- **WHEN** frame editing, shifting, zones, FPS conversion, or constant-FPS export has no explicit rate and no uniform known scope rate
- **THEN** it SHALL return a structured missing-rate or mixed-rate failure
- **AND** it SHALL NOT use a first-segment, detected, or default catalog rate

#### Scenario: Explicit choice resolves an unknown scope
- **WHEN** the caller explicitly selects a valid catalog rate or supplies a typed rate
- **THEN** the operation SHALL use that canonical rate
- **AND** an ordinary frame edit or serialization override SHALL preserve source metadata

#### Scenario: Time-only operation has unknown rate
- **WHEN** a time edit, naming operation, time-only expression, or time-based serialization does not require FPS
- **THEN** Unknown rate SHALL NOT prevent that operation

#### Scenario: Timecode map supplies the frame mapping
- **WHEN** Chapter-to-QPFile receives a valid v2 timecode map and no constant computation rate
- **THEN** it SHALL use the existing map policy without requiring a constant FPS
- **AND** it SHALL NOT invent authoritative FPS from map intervals

### Requirement: Frame conversion boundaries are consistent
Frame editing, FPS transforms, and integer frame output SHALL use nearest-integer rounding with midpoint away from zero at their declared boundaries. Input and display rounding SHALL NOT precede those boundaries.

#### Scenario: Fractional frame cell input
- **WHEN** a caller commits a valid finite decimal frame count with a known computation rate
- **THEN** Core SHALL retain its exact coefficient until conversion to nearest timestamp tick

#### Scenario: FPS transform changes the rate
- **WHEN** a reviewed conversion changes from one canonical rate to another
- **THEN** each source frame position SHALL be quantized once before conversion to target ticks
- **AND** the committed current rate SHALL equal the target kind and payload
- **AND** original source rates SHALL remain unchanged

#### Scenario: Segment duration derives from transformed bounds
- **WHEN** an FPS conversion transforms a segment
- **THEN** its absolute start and end SHALL use the same conversion rule as chapter times
- **AND** its duration SHALL equal converted end minus converted start
- **AND** identities, other tracks, and valid document bounds SHALL be preserved

#### Scenario: Same rate conversion
- **WHEN** source and target rates have equal canonical values
- **THEN** an explicit FPS transform SHALL retain its documented source-frame quantization
- **AND** it SHALL produce no history node only when both resulting ticks and metadata are unchanged

#### Scenario: Same numeric rate with different kind
- **WHEN** source and target are Rational 24/1 and Approximate 24
- **THEN** review SHALL include the changed rate kind
- **AND** an explicit FPS transform SHALL retain its documented time quantization
- **AND** a metadata-only assignment SHALL preserve ticks

#### Scenario: Frame export uses current ticks
- **WHEN** QPFile or celltimes serializes chapters
- **THEN** it SHALL calculate integer frames from ticks and the captured computation rate
- **AND** it SHALL NOT reuse frame text or TimeSpan.TotalSeconds
- **AND** serialization SHALL NOT mutate the document

### Requirement: Lua exposes rate uncertainty and compatibility projections
Lua SHALL retain numeric fps globals for known resolved rates and SHALL expose rate kind and rational text components. Lua arithmetic SHALL remain floating point. Core arithmetic outside Lua SHALL use canonical rates and ticks.

#### Scenario: Known rational Lua context
- **WHEN** a script runs with Rational 24000/1001
- **THEN** fps and chapter.fps SHALL be numeric compatibility projections
- **AND** fpsRateKind SHALL identify Rational
- **AND** fpsNumeratorText and fpsDenominatorText SHALL contain the reduced integer strings
- **AND** the safe numeric component globals SHALL contain 24000 and 1001

#### Scenario: Rational components exceed numeric precision
- **WHEN** a rational component cannot be represented safely by the Lua numeric domain
- **THEN** the text component globals SHALL preserve it
- **AND** the numeric numerator/denominator pair SHALL be nil

#### Scenario: Approximate Lua context
- **WHEN** a script runs with an Approximate rate
- **THEN** fps SHALL contain its numeric projection and fpsRateKind SHALL identify Approximate
- **AND** rational component globals SHALL be nil

#### Scenario: Unknown Lua context
- **WHEN** an unknown-rate script evaluates t + 1
- **THEN** it SHALL be allowed with nil fps and chapter.fps
- **AND** rate-dependent built-in presets SHALL require an explicit rate
- **AND** custom arithmetic using absent FPS SHALL fail with a structured diagnostic

#### Scenario: Script returns unchanged projected time
- **WHEN** a script returns the same numeric time supplied to it
- **THEN** Core SHALL preserve the original timestamp ticks
- **AND** it SHALL NOT commit a rounded seconds projection

#### Scenario: Authoring and execution share context
- **WHEN** a user analyzes or completes a script using the new rate globals
- **THEN** symbol metadata, help, validation, and execution SHALL describe the same rate kinds and availability
