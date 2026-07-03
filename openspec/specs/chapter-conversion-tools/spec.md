# chapter-conversion-tools Specification

## Purpose
Defines UI-independent conversion tools for restored legacy auxiliary workflows that produce chapter-derived text outputs.

## Requirements
### Requirement: Celltimes export conversion
The system SHALL provide a UI-independent conversion that exports chapter start frames in celltimes format compatible with the legacy `GetCelltimes()` workflow.

#### Scenario: Celltimes exports non-separator start frames
- **WHEN** a chapter set is converted to celltimes with a valid frame rate
- **THEN** the output SHALL contain one integer frame number per non-separator chapter start in chapter order
- **AND** separator rows SHALL NOT produce celltimes lines

#### Scenario: Celltimes uses compatibility rounding
- **WHEN** a chapter start time falls on a frame boundary rounding edge
- **THEN** the generated frame number SHALL use the Core legacy compatibility rounding policy

#### Scenario: Invalid celltimes frame rate fails structurally
- **WHEN** a caller requests celltimes conversion with an invalid or zero frame rate
- **THEN** the conversion SHALL return a structured failure diagnostic instead of producing misleading frame numbers

