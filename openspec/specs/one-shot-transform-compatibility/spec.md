# one-shot-transform-compatibility Specification

## Purpose

Defines the boundary between explicit chapter content operations and pure serialization.

## Requirements

### Requirement: Core and Node serialization writes prepared content
Core and Node serialization APIs SHALL serialize the supplied chapter snapshot. They SHALL NOT accept transform-bearing export options or replay content operations.

#### Scenario: Caller serializes prepared content
- **WHEN** a caller serializes a chapter snapshot
- **THEN** serialization SHALL preserve the supplied chapter values
- **AND** it SHALL NOT apply expressions, naming, numbering, frame-rate changes, or other content transforms

#### Scenario: Caller transforms content before serialization
- **WHEN** a caller needs an expression, naming, numbering, or frame-rate operation
- **THEN** the caller SHALL apply the explicit operation to content first
- **AND** it SHALL serialize the resulting snapshot without replaying that operation

### Requirement: CLI expressions remain explicit conversion inputs
CLI `--expression` and `--expression-preset` SHALL remain structured DotMake command options. The CLI SHALL apply the selected expression once before serialization.

#### Scenario: CLI converts with an expression
- **WHEN** `convert` is called with an expression or preset
- **THEN** the CLI SHALL apply the selected operation once before serialization
- **AND** each supported format SHALL serialize the resulting snapshot

#### Scenario: CLI rejects conflicting expression options
- **WHEN** both expression options are supplied
- **THEN** CLI validation SHALL fail before import
- **AND** it SHALL use its normal non-zero validation result
