## ADDED Requirements

### Requirement: Editable documents are immutable snapshots
Core SHALL represent current editable content as an immutable document with ordered tracks and chapters. A host SHALL receive no mutable reference to a published collection.

#### Scenario: Adapting imported chapters
- **WHEN** an importer result is opened as an editable document
- **THEN** the adapter SHALL create one document snapshot with stable document, track, and chapter identities
- **AND** it SHALL NOT retain a second editable chapter set

#### Scenario: Candidate changes are isolated
- **WHEN** a service computes a changed document from a published snapshot
- **THEN** the published snapshot SHALL remain unchanged until the candidate is explicitly accepted

### Requirement: Document time and frame values are deterministic
The document SHALL store time as signed 64-bit 100-nanosecond ticks, known frame rates as reduced positive rational values, and unknown duration or frame rate as absent values.

#### Scenario: Frame conversion uses the shared rounding rule
- **WHEN** a frame value is converted to ticks or ticks to frames
- **THEN** conversion SHALL round to the nearest integer with midpoint away from zero
- **AND** overflow SHALL return a diagnostic failure

#### Scenario: Unknown duration has no upper bound
- **WHEN** a document has unknown duration and a chapter start is edited
- **THEN** validation SHALL NOT treat unknown duration as a zero-length upper bound

### Requirement: Serialization consumes a captured document snapshot
Format serialization SHALL receive the explicit current document snapshot and format-only options. It SHALL NOT execute naming, expression, time-offset, or other interactive transforms.

#### Scenario: Export captures current document values
- **WHEN** a document is serialized after an edit
- **THEN** output SHALL contain the edited value exactly once
- **AND** serializing SHALL NOT change the document
