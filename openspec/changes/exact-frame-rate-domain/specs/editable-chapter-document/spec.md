## MODIFIED Requirements

### Requirement: Document time and frame values are deterministic
The document SHALL store time as signed 64-bit 100-nanosecond ticks. Its canonical frame-rate value SHALL be Rational, Approximate, or Unknown. Rational values SHALL contain reduced positive Int64 components. Approximate values SHALL contain positive decimal values. Unknown duration SHALL remain explicit and SHALL NOT impose a zero upper bound.

#### Scenario: Frame conversion uses the shared rounding rule
- **WHEN** a frame value is converted to ticks or ticks to frames
- **THEN** conversion SHALL round to the nearest integer with midpoint away from zero
- **AND** overflow SHALL return a diagnostic failure

#### Scenario: Unknown duration has no upper bound
- **WHEN** a document has unknown duration and a chapter start is edited
- **THEN** validation SHALL NOT treat unknown duration as a zero-length upper bound

#### Scenario: Default rate value is unknown
- **WHEN** the canonical rate value is default-initialized
- **THEN** it SHALL represent Unknown without a usable numeric payload

#### Scenario: Invalid rational bypasses construction
- **WHEN** a default rational or an invalid deserialized rate reaches a public operation
- **THEN** the operation SHALL fail with a structured diagnostic
- **AND** it SHALL NOT divide by zero or reinterpret invalid input as Unknown

## ADDED Requirements

### Requirement: Editable documents preserve frame-rate provenance
The document and its segments SHALL store canonical rates without synthesizing source authority from a numeric projection. Source rates SHALL remain separate from current rates.

#### Scenario: Exact source rate survives adaptation
- **WHEN** an importer provides rational 48000/2002 and its result is adapted to a document and typed public result
- **THEN** the current and source rates SHALL retain rational 24000/1001
- **AND** adaptation SHALL NOT pass the rate through double FPS

#### Scenario: Approximate source rate stays approximate
- **WHEN** a numeric API boundary supplies 23.976 without explicit rational metadata
- **THEN** the document SHALL store Approximate 23.976
- **AND** catalog lookup or display refresh SHALL NOT change it to Rational

#### Scenario: Unknown source survives conversion
- **WHEN** a segment with Unknown source rate is converted using explicit source and target computation rates
- **THEN** its current rate SHALL become the chosen target rate
- **AND** its source rate SHALL remain Unknown after further conversions and history navigation

#### Scenario: Non-rate edit preserves metadata
- **WHEN** a chapter name or timestamp is edited
- **THEN** the document SHALL preserve segment rate kind and source metadata
- **AND** the edit SHALL NOT rebuild a rate from display text or legacy FPS

### Requirement: Aggregate rate resolution includes every contributor
Track and document summaries SHALL be known only when all contributing rates are known and equal by kind and normalized payload. An empty scope SHALL be Unknown. A track without segments SHALL use its explicitly stored rate.

#### Scenario: Known and unknown segments
- **WHEN** a scope contains Rational 24/1 and Unknown segments
- **THEN** its aggregate rate SHALL be Unknown
- **AND** each segment SHALL retain its own rate

#### Scenario: Numeric equality does not erase provenance
- **WHEN** a scope contains Rational 24/1 and Approximate 24
- **THEN** it SHALL NOT report a uniform authoritative rate

#### Scenario: Equivalent ratios agree
- **WHEN** all contributing segments specify rational 24000/1001 or 48000/2002
- **THEN** the aggregate SHALL be Rational 24000/1001

#### Scenario: Selected track differs from another track
- **WHEN** a selected track is Unknown and another track has a known rate
- **THEN** its projection SHALL remain Unknown
- **AND** it SHALL NOT borrow the other track's rate

### Requirement: State comparison includes rate kind and payload
Document equality, candidate review, history, and export fingerprints SHALL compare canonical rates. Presentation text and detection suggestions SHALL NOT participate in content equality.

#### Scenario: Rate kind changes at the same numeric value
- **WHEN** a candidate changes Rational 24/1 to Approximate 24
- **THEN** review SHALL show a rate metadata change
- **AND** Apply, Undo, and Redo SHALL retain the exact recorded kinds
- **AND** canonical content comparison SHALL detect the change

#### Scenario: Equivalent canonical rate is assigned
- **WHEN** a candidate assigns an equivalent reduced rational or a decimal with equivalent scale-normalized value
- **THEN** it SHALL NOT create a false rate change or history node

#### Scenario: Display changes during review
- **WHEN** rounding, precision, tolerance, or language changes while a candidate is active
- **THEN** the candidate's canonical rates and ticks SHALL remain unchanged
- **AND** no content transaction SHALL be created
