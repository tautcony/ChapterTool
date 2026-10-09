# one-shot-transform-compatibility Specification

## Purpose
TBD - created by archiving change unified-editing-08-compatibility-adapters. Update Purpose after archive.
## Requirements
### Requirement: Compatibility APIs transform once then serialize current content
Compatibility entry points SHALL convert legacy transform options into one explicit content operation and pass its result to pure serialization. Reusing transformed content SHALL require the pure serializer and SHALL NOT reapply transforms.

#### Scenario: Legacy Core export includes an expression option
- **WHEN** a caller uses the compatibility export entry point with an expression option
- **THEN** the expression SHALL run once for that call before serialization
- **AND** the result SHALL match applying the operation explicitly then serializing

#### Scenario: Caller serializes already transformed content
- **WHEN** a caller supplies already transformed content to the pure serializer
- **THEN** the serializer SHALL preserve its values without replaying any transform

### Requirement: CLI expression options remain one-shot conversion inputs
CLI `--expression` and `--expression-preset` SHALL remain structured DotMake command options and SHALL be applied before pure serialization.

#### Scenario: CLI converts with an expression
- **WHEN** `convert` is called with an expression or preset
- **THEN** the selected expression SHALL run once and each supported format SHALL serialize the transformed snapshot

#### Scenario: CLI rejects conflicting expression options
- **WHEN** both expression options are supplied
- **THEN** CLI validation SHALL fail before import and SHALL use its normal non-zero validation result

### Requirement: Compatibility lifetime and deprecation are explicit
Public Core and Node.js adapters SHALL document their compatibility behavior and migration path. They SHALL NOT retain transform rules in an interactive session. Deprecated transform-bearing options SHALL follow the declared version policy.

#### Scenario: Adapter is called repeatedly
- **WHEN** a stateless adapter is called for separate conversions
- **THEN** each call SHALL apply only the options supplied for that call and SHALL NOT create interactive history

