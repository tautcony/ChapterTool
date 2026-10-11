## ADDED Requirements

### Requirement: CLI supports explicit exact and approximate computation rates
The CLI SHALL keep its numeric --frame-rate option and SHALL add --frame-rate-ratio N/D through DotMake.CommandLine. The options SHALL be mutually exclusive. Numeric input SHALL become Approximate. Ratio input SHALL become Rational.

#### Scenario: Numeric option remains compatible
- **WHEN** convert receives a valid --frame-rate 23.976
- **THEN** it SHALL normalize the input once to Approximate 23.976 before Core processing
- **AND** it SHALL NOT substitute Rational 24000/1001

#### Scenario: Exact ratio option is accepted
- **WHEN** convert receives --frame-rate-ratio 48000/2002
- **THEN** it SHALL pass Rational 24000/1001 to the one-shot expression/export context

#### Scenario: Conflicting options are rejected
- **WHEN** both frame-rate options are supplied
- **THEN** validation SHALL report the conflict to stderr and exit with code 1
- **AND** it SHALL NOT import input or create output

#### Scenario: Malformed or invalid supplied rate
- **WHEN** either option supplies non-positive, non-finite, unrepresentable, malformed, or out-of-range data
- **THEN** validation SHALL exit with code 1 and report the failure
- **AND** it SHALL NOT silently use a source, detected, or default rate

#### Scenario: No override preserves exact import
- **WHEN** no override is supplied and the imported source has Rational 30000/1001
- **THEN** Core expression preparation and frame export SHALL receive that same Rational rate

#### Scenario: No override and unknown source
- **WHEN** a constant-FPS export needs a rate absent from the source
- **THEN** convert SHALL fail structurally with instructions to supply an explicit rate
- **AND** time-only conversion SHALL remain available

#### Scenario: Override does not retime chapters
- **WHEN** convert supplies a ratio override without an expression
- **THEN** the override SHALL affect frame computation only
- **AND** it SHALL NOT perform an implicit FPS time transform

### Requirement: CLI inspection explains rate provenance
Inspect SHALL remain terminal text. It SHALL preserve selection identifiers and the legacy numeric FPS projection while adding rate kind and canonical value labels.

#### Scenario: Inspect reports exact rate
- **WHEN** inspect reports an authoritative rate
- **THEN** its text SHALL include the reduced rational and its rate kind

#### Scenario: Inspect reports approximate or unknown rate
- **WHEN** inspect reports an Approximate or Unknown rate
- **THEN** the text SHALL identify that classification
- **AND** it SHALL NOT present a rounded value as authoritative

#### Scenario: Stdout conversion remains content-only
- **WHEN** convert succeeds with --stdout
- **THEN** stdout SHALL contain only exported chapter content
- **AND** rate labels and diagnostics SHALL NOT be inserted into that content
