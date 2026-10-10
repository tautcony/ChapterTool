## ADDED Requirements

### Requirement: Authoritative frame calculations preserve tick and rational precision

When a typed timestamp and authoritative rational frame rate are available, frame values MUST be calculated from timestamp ticks and the rate numerator and denominator. Calculation, rounding, fixed-place display, and accuracy comparison MUST NOT first convert ticks to decimal seconds or replace the rational rate with a decimal approximation. The calculation MUST use bounded integer arithmetic that can represent intermediate products without overflow. An approximate or unknown rate MUST retain the existing numeric calculation path and MUST NOT claim exact rational provenance.

#### Scenario: Calculate an exact fractional frame value
- **WHEN** a timestamp is 52.094 seconds and its authoritative frame rate is 24000/1001
- **THEN** the frame value is derived from ticks multiplied by 24000 over ticks-per-second multiplied by 1001
- **AND** rounding and decimal presentation use that same exact rational value

#### Scenario: Compare values using their own rational rates
- **WHEN** an expression preview has before and candidate snapshots with different rational frame rates
- **THEN** each frame value is calculated from its own timestamp and rate
- **AND** neither side is recalculated with the other side's rate

#### Scenario: Retain approximate rate behavior
- **WHEN** only an approximate decimal frame rate is available
- **THEN** the system uses the existing ordinary numeric calculation and presentation
- **AND** it does not infer a rational rate or recurring decimal

### Requirement: Auto frame rate retains one effective rate across the workflow

When Auto is selected, Avalonia and Web MUST keep the Auto selection visible and retain the detected frame-rate option as the effective rate for calculations. They MUST preserve its authoritative rational rate through frame presentation, editing, expression preview, frame-rate conversion, and frame shifts. Automatic detection MUST run in integer, fixed-place, and full-precision display modes.

#### Scenario: Display a detected rational rate with full precision
- **WHEN** Auto detects 24000/1001 from chapter timestamps while full precision is selected
- **THEN** the selected option remains Auto
- **AND** frame values use the detected numerator and denominator
- **AND** a provable recurring value can use its compact repeating-decimal presentation

#### Scenario: Shift frames while Auto remains selected
- **WHEN** the user previews or applies a frame shift in Auto mode
- **THEN** the offset uses the detected effective frame rate and its rational basis
- **AND** the resulting timestamp uses integer tick arithmetic without converting the rate to a rounded decimal

### Requirement: Repeating decimal presentation is controlled only in Settings

Avalonia and Web MUST expose three frame display modes: integer rounding, a configured number of decimal places, and no truncation. They MUST expose one localized “显示小数循环节” checkbox in the existing General frame-display settings group. The preference MUST default to enabled. The main workflow, chapter header, chapter rows, and expression review MUST NOT contain a repeating-decimal selector or toggle. The checkbox MUST be enabled only in no-truncation mode and MUST retain its value when the mode changes. The main-window rounding control and Settings mode MUST stay synchronized.

#### Scenario: Open Settings and inspect the chapter grid
- **WHEN** the user opens Settings with no-truncation mode selected
- **THEN** the frame-display group exposes the repeating-decimal checkbox
- **AND** its helper text explains compact recurring decimals and the ordinary numeric fallback
- **AND** the chapter grid has no additional display control

#### Scenario: Keep the preference when the display mode changes
- **WHEN** the user selects integer rounding or fixed decimal places
- **THEN** the checkbox becomes unavailable without changing its stored value
- **WHEN** the user selects no truncation
- **THEN** the checkbox becomes available with its previous value
- **AND** the main-window rounding control reflects the selected mode

### Requirement: Settings retain the existing host lifecycle

The new preference MUST follow the existing host settings lifecycle. Avalonia MUST live-apply, explicitly save, and restore the loaded or saved value on discard. Web MUST keep edits in its draft until Save succeeds. Reset MUST restore the enabled default through that host's existing reset behavior. Changing the preference MUST NOT change chapter content, create history, or rerun an expression.

#### Scenario: Live-apply and discard in Avalonia
- **WHEN** the user changes the preference in Avalonia Settings
- **THEN** committed and visible candidate frame presentation refreshes without writing settings
- **WHEN** the user discards that unsaved settings change
- **THEN** presentation returns to the saved preference
- **AND** chapter content, selection, candidate identity, and history remain unchanged

#### Scenario: Save a Web draft
- **WHEN** the user changes the preference in Web Settings and successfully saves
- **THEN** the saved preference becomes active on the chapter grid
- **AND** Cancel or a persistence failure before a successful Save leaves the preceding active preference intact

### Requirement: Exact repeating decimals use a continuous overline

In no-truncation mode with the preference enabled, the system MUST display a provable rational frame value with a repetend of at most six digits as sign, integer digits, decimal point, complete non-repeating prefix, and one overlined repetend. The overline MUST cover only the shortest repeating block. The presentation MUST preserve leading zeros in prefixes and repetends. An exact integer MUST omit its zero fractional tail. A finite decimal MUST use ordinary numeric formatting without an overline.

#### Scenario: Render a purely recurring value
- **WHEN** the authoritative timestamp is 52.094 seconds and the frame rate is 24000/1001
- **THEN** the cell displays integer digits 1249 and fractional digits 006993
- **AND** one continuous overline covers all six fractional digits
- **AND** the underlying value remains unchanged

#### Scenario: Render a non-repeating prefix
- **WHEN** the timestamp becomes 52.0942 seconds at 24000/1001 fps
- **THEN** the cell displays integer digits 1249, prefix 0, and repetend 117882
- **AND** the overline covers 117882 and does not cover the prefix 0

#### Scenario: Remove an exact zero tail
- **WHEN** the exact frame value is 9792
- **THEN** compact presentation displays 9792 without a decimal point
- **AND** it reserves fractional layout space without adding digits

#### Scenario: Preserve a negative fractional value
- **WHEN** a supported frame value has a negative sign and an absolute value below one
- **THEN** its sign remains visible before the integer zero
- **AND** the overline applies only to its repetend

### Requirement: Cycle detection uses authoritative provenance and bounded work

The formatter MUST use typed frame values or timestamps with their authoritative rational rate basis. It MUST NOT infer an infinite cycle from a repeated substring, rounded decimal rate, localized rate label, or tolerant rate match. It MUST use repeated remainders to identify a cycle. It MUST stop after at most 512 long-division steps. A longer-than-six-digit cycle, an exhausted budget, or unavailable exact provenance MUST use the existing ordinary numeric display policy without an overline.

#### Scenario: A numeric string contains apparent repetition
- **WHEN** a frame value is available only as finite numeric text with repeated digits and a rounding tail
- **THEN** the formatter preserves the ordinary numeric presentation
- **AND** it does not assert an exact recurring value

#### Scenario: A cycle exceeds the display limit
- **WHEN** a provable frame value has a repetend longer than six digits
- **THEN** the cell uses ordinary numeric formatting and existing precision preferences
- **AND** it does not overline a truncated block
- **AND** the exact fraction remains accessible in existing value details

### Requirement: Before and after values share the same presentation policy

Committed values, visible original values, and candidate values MUST use one presentation policy. Both comparison sides MUST use the existing integer mode when rounding is active. Both sides MUST use the same selected decimal policy when rounding is inactive. Each side MUST retain its own value, frame-rate basis, and semantic accuracy state. Display formatting MUST NOT determine candidate equality, hide a typed change, or manufacture an unchanged-cell arrow.

#### Scenario: Compare the selected fractional shift
- **WHEN** a candidate applies t + 0.0002 to 52.094 seconds at 24000/1001 fps in no-truncation mode with the preference enabled
- **THEN** the original shows 1249 with overlined fractional block 006993
- **AND** the candidate shows 1249, prefix 0, and overlined block 117882
- **AND** an arrow separates the values without converting either side to an integer

#### Scenario: Compare an integer original with a fractional candidate
- **WHEN** the candidate changes the exact frame value 9792 to 9792 plus 24/5005
- **THEN** the original shows 9792
- **AND** the candidate shows 9792, prefix 0, and overlined block 047952
- **AND** both sides remain in the same unrounded presentation mode

#### Scenario: Disable recurring presentation
- **WHEN** the user disables the preference while rounding is inactive
- **THEN** both sides use the existing ordinary numeric format and decimal-place preference
- **AND** neither side keeps an overline
- **AND** candidate identity, readiness, and content remain unchanged

#### Scenario: The frame value did not change
- **WHEN** there is no candidate or the candidate has no frame-value change
- **THEN** the frame cell displays one value
- **AND** it does not add an arrow or duplicate the value

#### Scenario: A candidate changes the frame-rate basis
- **WHEN** the candidate uses a different frame-rate basis from the original
- **THEN** each side is formatted from its own typed value and rate basis
- **AND** existing rate details remain available
- **AND** the original is not recalculated using the candidate's rate

### Requirement: Decimal frame cells use stable accessible alignment

Frame cells MUST use the semantic monospace font. Integer digits MUST align to the right of their reserved area. Fractional digits MUST align to the left of their reserved area. Each comparison side MUST align decimal points vertically across rows. Before and after areas MUST have equal width with a stable arrow area between them. Exact integers MUST retain empty fractional space. Existing narrow layouts MUST preserve both values through labeled accessibility content, two-line comparison, or table scrolling.

#### Scenario: Scan varying integer lengths
- **WHEN** rows contain 0, 1249, 9792, and 13215 with different fractional content
- **THEN** decimal-point positions align within each side
- **AND** arrow positions remain stable
- **AND** the overline does not change row height or clip the glyphs

#### Scenario: Resize or enlarge the font
- **WHEN** the user uses supported default, wide, and narrow widths or enlarges the monospace font
- **THEN** values, overlines, and arrows remain readable without overlap
- **AND** the existing frame header and semantic colors remain intact

### Requirement: Presentation preserves inspection editing copy and export

Value details MUST expose the plain numeric expansion with its displayed precision and the exact fraction when available. Accessible descriptions MUST distinguish original and candidate values and identify the non-repeating prefix and cycle. Frame editing and ordinary copy MUST use plain numeric text. Display notation MUST NOT enter domain fields, candidate snapshots, history, or serializers.

#### Scenario: Inspect and copy a compact value
- **WHEN** the user inspects or copies a compact recurring frame value
- **THEN** details expose the numeric expansion and exact fraction
- **AND** ordinary copy contains numeric text without overline glyphs, parentheses, or ellipses

#### Scenario: Enter and leave an unchanged edit
- **WHEN** the user edits a compact frame cell and accepts its unchanged numeric editor value
- **THEN** the exact underlying value remains unchanged
- **AND** no transaction is created merely to replace display notation

#### Scenario: Export or undo after a display change
- **WHEN** the user changes the preference and exports, applies an existing candidate, or uses Undo or Redo
- **THEN** the resulting content and history match the same actions before the presentation change
- **AND** exported frame numbers do not contain display notation

#### Scenario: Distinguish missing frames from zero
- **WHEN** one comparison side has no calculated frame information and the other has zero frames
- **THEN** the missing side retains its localized missing-value label
- **AND** the zero side displays 0
