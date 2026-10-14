## Purpose

Defines the canonical public frame-rate and chapter-content contracts for Core and Node consumers. Public operations preserve explicit rate provenance and timestamp ticks without legacy numeric projections.

## Requirements

### Requirement: Public content APIs use canonical rate and timestamp values
Public Core and Node content APIs SHALL use `FrameRateValue` or the exact Node rate union for rates, and timestamp ticks or exact tick strings for content time. They SHALL NOT expose numeric FPS or seconds compatibility projections, constructors, deconstructors, or operation overloads.

#### Scenario: Core consumer constructs chapter content
- **WHEN** a caller constructs or updates a chapter set
- **THEN** the caller SHALL supply its canonical frame rate
- **AND** timestamps SHALL remain `TimeSpan` ticks or document ticks without conversion through floating-point seconds

#### Scenario: Node consumer imports and edits content
- **WHEN** a caller completes a content workflow through the Node package
- **THEN** its public input and result types SHALL use exact rate kinds and integer tick strings
- **AND** no numeric seconds/FPS facade SHALL be exported

#### Scenario: Caller needs an approximate frame rate
- **WHEN** a caller has an approximate numeric measurement
- **THEN** it SHALL explicitly construct an Approximate `FrameRateValue` or exact Node rate value at the boundary
- **AND** the system SHALL NOT infer Rational provenance from the numeric value

### Requirement: Frame-rate selection exposes MPLS identity by format meaning
Selectable frame-rate options SHALL expose their Blu-ray MPLS rate code as `MplsFrameRateCode`. The code SHALL preserve the same values and reserved/custom behavior across Core, Avalonia, WASM, and Node DTOs.

#### Scenario: Catalog option is selected
- **WHEN** desktop or browser UI maps a displayed frame-rate option
- **THEN** it SHALL preserve the option's existing MPLS code mapping and reserved entry behavior
- **AND** public DTOs SHALL use the `mplsFrameRateCode` field name
