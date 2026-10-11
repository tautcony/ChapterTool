## MODIFIED Requirements

### Requirement: Merge replaces current tracks in one transaction
Merge SHALL use the current edited content, preserve chapter names, identities, explicit end times, and required source information, and replace selected tracks with one merged track. It SHALL retain boundaries and media placement without a second chapter backup. It SHALL preserve canonical current and source rates per segment.

#### Scenario: Merge after editing a source track
- **WHEN** the user edits a chapter and then merges its track
- **THEN** the merged track SHALL contain the edited value and the same chapter identity
- **AND** merge SHALL create one undoable transaction

#### Scenario: Merge tracks with different frame rates
- **WHEN** source tracks have different canonical rates, including known plus Unknown or different provenance at equal numeric values
- **THEN** merge SHALL apply an explicit compatible rate policy or report a conflict
- **AND** it SHALL NOT silently interpret every time using the first track rate

#### Scenario: Merge tracks with equivalent rational rates
- **WHEN** all source rates reduce to the same rational
- **THEN** merge SHALL preserve that uniform canonical rate and the per-segment source metadata

## ADDED Requirements

### Requirement: Structural operations preserve canonical segment rates
Split and append SHALL preserve current and source rate kind/payload. Scalar compatibility adapters SHALL report a conflict when their contract cannot retain required mixed-rate segment information.

#### Scenario: Split after conversion
- **WHEN** a converted track is split at its current boundaries
- **THEN** each resulting segment SHALL retain its current target rate and original source rate
- **AND** Unknown source metadata SHALL remain Unknown

#### Scenario: Append a source with different rate
- **WHEN** append adds a source with a different canonical rate
- **THEN** the document SHALL retain each segment's rate
- **AND** affected aggregate summaries SHALL be recomputed across every contributor
- **AND** existing ticks SHALL NOT be reinterpreted

#### Scenario: Legacy scalar projection cannot preserve mixed content
- **WHEN** a stateless scalar operation would discard required segment rate information
- **THEN** it SHALL return a conflict without producing a misleading uniform-rate result
