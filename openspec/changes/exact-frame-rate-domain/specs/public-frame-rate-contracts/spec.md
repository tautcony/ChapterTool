## ADDED Requirements

### Requirement: Compatibility is confined to established external boundaries
Internal domain models, calculations, host state, and selector/service contracts SHALL use canonical rates. The selected numeric Core/Node APIs and transform-bearing export contract SHALL be removed in 23.x. Manual CLI frame-rate input SHALL remain a supported Approximate input.

#### Scenario: Internal selector supplies a catalog option
- **WHEN** a host selects a catalog option
- **THEN** it SHALL pass the option's canonical rational directly
- **AND** it SHALL NOT adapt a display double or decimal back into an authoritative rate

#### Scenario: Internal-only public helper changes
- **WHEN** a public helper has only repository consumers and no established external contract
- **THEN** its signature SHALL migrate directly
- **AND** compatibility overloads SHALL NOT be retained solely because it is public

#### Scenario: CLI caller supplies a numeric FPS
- **WHEN** a caller supplies positive numeric FPS through `--frame-rate`
- **THEN** the boundary SHALL normalize it once to Approximate
- **AND** internal services SHALL receive only the canonical value

### Requirement: Exact inputs are available to external callers
C# typed APIs and the Node package SHALL accept canonical rational and approximate values. Numeric inputs SHALL NOT be silently interpreted as catalog rationals.

#### Scenario: Explicit rational input
- **WHEN** a caller supplies 48000/2002 explicitly
- **THEN** the operation SHALL use Rational 24000/1001 without a numeric round-trip

#### Scenario: Explicit catalog code
- **WHEN** an exact API selects a valid catalog code
- **THEN** the operation SHALL use the catalog's canonical rate
- **AND** Auto, Reserved, and unknown codes SHALL NOT supply a valid explicit computation rate

#### Scenario: Invalid numeric normalization
- **WHEN** a legacy boundary receives non-finite, negative, required-zero, out-of-range, or non-representable FPS
- **THEN** it SHALL preserve the established argument/validation failure category
- **AND** it SHALL NOT call Core with a clamped, zero, or substituted rate

### Requirement: Node exposes one exact content facade
The package `ChapterTool` entry point SHALL expose typed content operations with lossless rate/tick data. Numeric chapter DTOs and the `.exact`, `.project`, and `.export` compatibility methods SHALL NOT be exposed.

#### Scenario: Caller completes a content workflow
- **WHEN** a caller imports, edits, projects frames, converts FPS, and serializes through `ChapterTool`
- **THEN** each returned content value SHALL preserve canonical rate kind and ticks
- **AND** expressions, shifts, zones, combine/append, and conversion tools SHALL have corresponding typed entry points

#### Scenario: Exact scalar combine would discard mixed rates
- **WHEN** an exact scalar chapter-set combine or append cannot represent its contributors' different rates
- **THEN** it SHALL return a structured conflict
- **AND** it SHALL NOT choose the first rate or flatten required metadata silently

### Requirement: Exact Node wire values are invariant strings
The exact rate DTO SHALL be a discriminated union: rational with numerator/denominator strings, approximate with a decimal value string, or unknown without a numeric payload. Exact times SHALL use signed Int64 tick strings. Null duration SHALL represent Unknown duration; null chapter end SHALL represent an absent end.

#### Scenario: Int64 exceeds JavaScript safe integer
- **WHEN** a valid rate component or tick value exceeds Number.MAX_SAFE_INTEGER
- **THEN** JSON serialization and parsing SHALL preserve its integer string exactly

#### Scenario: Canonical rational serialization
- **WHEN** Rational 48000/2002 is returned
- **THEN** its wire value SHALL be `{"kind":"rational","numerator":"24000","denominator":"1001"}`

#### Scenario: Decimal serialization is locale-independent
- **WHEN** Approximate 23.976 is returned under Chinese, English, or Japanese host culture
- **THEN** its wire value SHALL contain the same invariant decimal string
- **AND** parsing SHALL NOT accept a grouping separator or locale-dependent decimal marker

#### Scenario: Exact wire payload is invalid
- **WHEN** a well-formed JSON request has an unknown kind, missing or conflicting state payload, zero denominator, negative rate, overflow, or rounded-to-fit decimal
- **THEN** the exact API SHALL return a structured rate validation failure
- **AND** it SHALL NOT invoke an operation or infer replacement metadata

#### Scenario: Exact timestamp round-trip
- **WHEN** an exact caller returns startTicks, nullable endTicks, and nullable durationTicks unchanged
- **THEN** Core SHALL receive the same ticks and duration/end availability
- **AND** it SHALL NOT reconstruct them from seconds numbers

### Requirement: Distributed declarations match runtime contracts
Node C# JSON registrations, handwritten TypeScript types, wrapper methods, and emitted package declarations SHALL describe the same exact contract. Public documentation SHALL state rounding and rate authority.

#### Scenario: Package consumer uses typed input
- **WHEN** an installed package consumer compiles and runs a `ChapterTool` call
- **THEN** emitted declarations and the packaged Wasm runtime SHALL accept the documented DTO and return the documented rate/tick shape

#### Scenario: Core consumer uses canonical input
- **WHEN** verification compiles a consumer of the public Core API
- **THEN** it SHALL use canonical rate and timestamp values
- **AND** it SHALL NOT require numeric compatibility scaffolding
