## MODIFIED Requirements

### Requirement: Shared frame display and conversion bounds remain consistent
Both hosts MUST derive frame text and accuracy from canonical rates and display preferences without content transactions. Frame edits and signed shifts MUST use the explicit computation choice or a uniform known target-scope rate. Detection MUST remain a display suggestion. Frame conversion MUST quantize absolute segment boundaries through the chapter conversion calculation. It MUST preserve segment identities, source rate metadata, and known document bounds. Selected-track operations MUST preserve other tracks.

#### Scenario: Source FPS is absent
- **WHEN** either host detects a usable display suggestion while source/current rate is absent
- **THEN** frame editing MUST require an explicit computation rate
- **AND** refreshing the suggested display MUST create no content transaction

#### Scenario: Convert rational FPS
- **WHEN** a reviewed rational-FPS conversion changes a segment timeline
- **THEN** each converted segment duration MUST equal its converted end minus its converted start
- **AND** the known document duration MUST contain every retained segment
- **AND** the operation MUST preserve source rate metadata and other tracks

#### Scenario: Explicit rate enables the same edit in both hosts
- **WHEN** the user selects the same canonical rate and edits the same frame value on unknown-rate content in both hosts
- **THEN** both hosts MUST produce the same candidate ticks
- **AND** neither host MUST upgrade the original Unknown source rate

#### Scenario: Auto preserves a known approximate rate
- **WHEN** a track already carries a known Approximate rate and Auto is selected
- **THEN** both hosts MUST preserve that rate for calculations
- **AND** a nearby catalog match MUST NOT supply rational authority

## ADDED Requirements

### Requirement: Portable rate workflow verification covers uncertainty and precision
Both hosts MUST preserve rate kind, ticks, selection identity, candidate lifecycle, and export prerequisites for Rational, Approximate, Unknown, and mixed content. Verification MUST include real window/browser interactions, localization, and stateful layouts.

#### Scenario: Matching host workflow sequence
- **WHEN** verification loads the same inputs, selects rates, edits frames, reviews/applies a conversion, performs Undo/Redo, and exports in both hosts
- **THEN** committed ticks, current/source rates, and deterministic output MUST match

#### Scenario: Current rate is custom
- **WHEN** a non-catalog rational or approximate rate is loaded and reselected across track and language changes
- **THEN** both hosts MUST retain its canonical identity
- **AND** existing rows MUST refresh in Chinese, English, and Japanese

#### Scenario: Display settings change during pending review
- **WHEN** rounding, precision, tolerance, or repeating-decimal display changes
- **THEN** both hosts MUST preserve the reviewed candidate and history position
- **AND** changing the computation-rate draft MUST invalidate the corresponding prepared candidate

#### Scenario: Stateful responsive layouts
- **WHEN** verification exercises empty, valid, invalid, pending, and stale rate workflows at default, wide, and narrow sizes
- **THEN** primary actions and validation MUST remain reachable
- **AND** time/frame values MUST remain unclipped in normal, editor, and preview cells
- **AND** screenshot evidence MUST accompany behavior assertions
