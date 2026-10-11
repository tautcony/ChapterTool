## ADDED Requirements

### Requirement: Compatibility is confined to established external boundaries
Internal domain models, calculations, host state, and selector/service contracts SHALL migrate directly to canonical rates. Manual numeric inputs and established external API signatures and result fields SHALL retain compatible boundary adapters or projections through major version 23.

#### Scenario: Internal selector supplies a catalog option
- **WHEN** a host selects a catalog option
- **THEN** it SHALL pass the option's canonical rational directly
- **AND** it SHALL NOT adapt a display double or decimal back into an authoritative rate

#### Scenario: Internal-only public helper changes
- **WHEN** a public helper has only repository consumers and no established external contract
- **THEN** its signature SHALL migrate directly
- **AND** compatibility overloads SHALL NOT be retained solely because it is public

#### Scenario: Existing numeric caller uses an external API
- **WHEN** an established caller supplies positive numeric FPS through a retained signature
- **THEN** the boundary SHALL normalize it once to Approximate
- **AND** internal services SHALL receive only the canonical value

#### Scenario: Legacy external metadata reports no FPS
- **WHEN** a retained metadata contract supplies its established zero FPS sentinel
- **THEN** adaptation SHALL create Unknown
- **AND** zero supplied as a required operation rate SHALL fail validation

#### Scenario: Retained C# numeric metadata is changed
- **WHEN** a retained external constructor or record update assigns numeric FPS to previously rational content
- **THEN** the new canonical current rate SHALL be Approximate
- **AND** stale exact metadata SHALL NOT remain independently active

#### Scenario: Deprecated output is read
- **WHEN** an established external result exposes its numeric FPS member
- **THEN** that member SHALL be computed from the canonical value
- **AND** it SHALL be documented as a compatibility projection
- **AND** internal operations SHALL NOT use it as a calculation source

### Requirement: Exact inputs are available to external callers
C# typed APIs and the exact Node facade SHALL accept canonical rational and approximate values. Numeric inputs SHALL NOT be silently interpreted as catalog rationals.

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

#### Scenario: Invalid numeric model construction or update
- **WHEN** a retained `ChapterSet` constructor or numeric `FramesPerSecond` init assignment receives an invalid rate
- **THEN** it SHALL throw an argument exception at assignment
- **AND** this confirmed compatibility exception SHALL replace deferred export or expression validation of that invalid model
- **AND** Try factories and operation request boundaries SHALL still return structured diagnostics

### Requirement: Node has separate legacy and exact facades
The existing Node facade SHALL retain its signatures and numeric DTO shapes. The additive tool.exact facade SHALL expose typed content operations with lossless rate/tick data. Both facades SHALL share canonical Core behavior.

#### Scenario: Existing caller changes returned numbers
- **WHEN** a legacy caller changes a returned chapterSet.framesPerSecond or chapter.startTimeSeconds value before resubmitting it
- **THEN** the legacy facade SHALL honor that numeric change
- **AND** it SHALL NOT ignore it because of newly added higher-priority canonical fields

#### Scenario: Exact caller completes a content workflow
- **WHEN** a caller imports, edits, projects frames, converts FPS, and serializes through tool.exact
- **THEN** each returned content value SHALL preserve canonical rate kind and ticks
- **AND** expressions, shifts, zones, combine/append, and conversion tools SHALL have corresponding typed entry points

#### Scenario: Legacy response is reused
- **WHEN** a numeric legacy result is submitted as input
- **THEN** its rate SHALL be Approximate or Unknown according to legacy metadata
- **AND** documentation SHALL state that legacy round-trips do not guarantee source authority or full tick precision

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
Node C# JSON registrations, handwritten TypeScript types, wrapper methods, and emitted package declarations SHALL describe the same legacy and exact contracts. Public migration documentation SHALL state rounding, authority, and compatibility limits.

#### Scenario: Package consumer uses exact input
- **WHEN** an installed package consumer compiles and runs an exact-facade call
- **THEN** emitted declarations and the packaged Wasm runtime SHALL accept the documented DTO and return the documented rate/tick shape

#### Scenario: Established Core consumer is compiled and run
- **WHEN** verification compiles a consumer of retained external signatures
- **THEN** those boundary contracts SHALL remain usable
- **AND** new internal signatures SHALL NOT require repository compatibility scaffolding
