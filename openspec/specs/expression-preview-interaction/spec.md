# expression-preview-interaction Specification

## Purpose
TBD - created by archiving change unify-expression-preview-interaction. Update Purpose after archive.
## Requirements
### Requirement: Both hosts provide one expression review surface
Avalonia and Web MUST provide an independent expression tool with the editor and chapter comparison in the same surface. Both hosts MUST show the current track name, participating chapter count, preset selector, multiline editor, preview summary, and persistent Cancel and Apply changes actions. The committed chapter table MUST remain unchanged during preparation.

#### Scenario: Open the expression tool in either host
- **WHEN** the user opens the expression tool in Avalonia or Web
- **THEN** the tool displays the current-track scope and the stored expression parameters
- **AND** a nonempty expression starts a read-only preview for that scope
- **AND** excluded separator rows are identified separately from participating chapters

#### Scenario: Select and edit a multiline preset
- **WHEN** the user selects a multiline preset and then changes its text
- **THEN** the editor preserves line breaks and indentation
- **AND** the preset selection becomes Custom
- **AND** the tool refreshes its preview without changing the document or history

### Requirement: Preview groups differences by chapter
Both hosts MUST show one comparison entry per chapter in track order, matched by stable identity. Each entry MUST expose the chapter number and name, before value, after value, and signed time delta. Changed-only filtering and time units MUST be the defaults. Users MUST be able to view all chapters, switch to frame values, and inspect frame details. The preview MUST remain read-only.

#### Scenario: Inspect a five-second shift
- **WHEN** a valid candidate shifts a chapter from 00:01:00 to 00:01:05
- **THEN** one entry shows both times and a positive five-second delta
- **AND** its frame-information changes are grouped with that chapter

#### Scenario: Filter or change units
- **WHEN** the user switches between changed and all chapters or between time and frame units
- **THEN** only the presentation changes
- **AND** candidate identity, target scope, and application content remain unchanged
- **AND** no expression evaluation is required for the display switch

#### Scenario: Inspect a large result
- **WHEN** a candidate contains 1,000 changed chapters
- **THEN** counts cover all 1,000 chapters
- **AND** every changed chapter is reachable through scrolling or paging
- **AND** the preview does not silently truncate differences to eight entries

### Requirement: Summary distinguishes persisted effects
The preview MUST distinguish time changes, frame-information updates, and document or track property changes. Affected-chapter totals MUST count unique identities. Overlapping categories MUST NOT be added as if they were disjoint. Complete candidate equality MUST determine no-change behavior.

#### Scenario: Frame data changes without time changes
- **WHEN** the candidate updates three chapters' frame data and leaves all chapter times unchanged
- **THEN** the summary explicitly states that chapter times are unchanged
- **AND** it identifies the three frame-information updates
- **AND** the comparison does not claim that three times moved

#### Scenario: One chapter changes in two categories
- **WHEN** the same chapter changes time and frame accuracy
- **THEN** it counts as one affected chapter
- **AND** both categories remain inspectable

#### Scenario: Frame-rate detection changes metadata
- **WHEN** preparation adds a frame rate or modifies segment or document metadata
- **THEN** a separate property section displays before and after values
- **AND** it identifies the actual owner of each property, including effects beyond the selected track
- **AND** those changes remain visible even if no chapter time changes

#### Scenario: Only properties change
- **WHEN** a valid current candidate changes a persisted property but no chapter time
- **THEN** the summary names the property change
- **AND** Apply remains available after the complete result is ready

### Requirement: Values use clear localized business formats
Both hosts MUST use localized field labels, units, missing-value states, and diagnostic summaries. They MUST format values from typed snapshots rather than parse technical difference strings. They MUST NOT expose raw enum names or object dumps as normal preview content. Time formatting MUST retain enough precision to distinguish actual changes.

#### Scenario: Distinguish missing values from zero
- **WHEN** original frame information is absent and the result is frame zero
- **THEN** the original is labeled Not calculated and the result is labeled zero frames
- **AND** an unknown frame rate is labeled Not set rather than zero

#### Scenario: Compare frame rates
- **WHEN** before and after frame values use different frame rates
- **THEN** each side displays its own frame-rate basis
- **AND** the preview omits an incompatible numeric frame delta
- **AND** rational rates remain inspectable, such as 24000/1001

#### Scenario: Display a sub-millisecond change
- **WHEN** two distinct times would have identical millisecond display text
- **THEN** the preview increases displayed precision enough to distinguish them
- **AND** the delta is not displayed as zero

#### Scenario: Display accuracy and additional fields
- **WHEN** frame accuracy or another persisted field changes
- **THEN** its business label and before/after meaning are accessible in details
- **AND** accuracy text reflects the actual tolerance policy
- **AND** an unrecognized field is not silently omitted from review

### Requirement: Live preview has explicit readiness states
Both hosts MUST expose unavailable, empty, waiting, computing, ready, unchanged, invalid, stale, applying, and failed states with an understandable status. Apply MUST be disabled except for a valid, changed, current candidate. Cancel and Apply MUST remain visible. Input changes MUST immediately invalidate the previous candidate.

#### Scenario: Empty or unchanged input
- **WHEN** there is no participating chapter, the script is empty, or the complete candidate equals its base
- **THEN** the tool explains the applicable reason and disables Apply
- **AND** it creates no document history entry
- **AND** empty input is not silently evaluated as an identity expression

#### Scenario: Explicit identity expression has side effects
- **WHEN** the user enters `t` explicitly and preparation changes persisted frame information
- **THEN** the tool displays those effects and evaluates readiness from the complete candidate
- **AND** it does not report no changes solely because times are equal

#### Scenario: Input replaces a ready preview
- **WHEN** the user changes the script after a valid preview
- **THEN** Apply becomes disabled before the next calculation completes
- **AND** any retained previous values are labeled as an old preview
- **AND** only the latest draft revision can become ready

#### Scenario: Input-method composition and late results
- **WHEN** input composition is active or a result arrives after the tool closes
- **THEN** incomplete composition does not trigger evaluation
- **AND** a closed tool ignores the late result without modifying the document or reopening

### Requirement: Errors reject the complete operation
Both hosts MUST show current evaluation, validation, timeout, and cancellation outcomes without offering partial application. Error locations MUST be shown when available and MUST NOT be invented. Warnings MUST be distinguishable from blocking errors.

#### Scenario: One chapter fails
- **WHEN** evaluation produces a negative time for chapter eight after seven successful calculations
- **THEN** the tool shows the error and disables Apply for the entire candidate
- **AND** no chapter or history state changes

### Requirement: Apply commits only the reviewed candidate
Both hosts MUST submit the displayed candidate without reevaluating the expression in the Apply action. Eligibility MUST bind to the same draft revision, target scope, document session, and base version. The transaction boundary MUST reject stale candidates and deduplicate repeated submission of the same candidate.

#### Scenario: Confirm a reviewed result
- **WHEN** the user applies a valid current preview
- **THEN** the committed values exactly match the displayed candidate
- **AND** the application path does not evaluate the expression again
- **AND** one atomic history transaction contains all candidate changes

#### Scenario: Refresh after a stale base
- **WHEN** document content, history position, or target scope changes after preparation
- **THEN** the old candidate cannot commit
- **AND** the tool remains open with the script and an Update preview action
- **WHEN** the user updates the preview
- **THEN** a new candidate is shown for inspection
- **AND** applying it requires another explicit confirmation

#### Scenario: Change races with submission
- **WHEN** the base changes after the UI readiness check but before the transaction commits
- **THEN** the transaction rejects the candidate
- **AND** the tool enters stale state rather than committing a newly calculated replacement

#### Scenario: Duplicate submission or retry
- **WHEN** a double-click or request retry submits the same reviewed candidate more than once
- **THEN** its stable submission identity permits at most one commit
- **AND** the applying state disables editing, Apply, and dismissal until the outcome is known

#### Scenario: Recover from an application failure
- **WHEN** application fails without committing
- **THEN** the tool preserves the draft and reports the reason
- **AND** it enables correction or retry only when that action is valid

### Requirement: Both hosts share cancellation and success behavior
Cancel, Close, and outer Escape MUST discard pending work, restore the entry expression parameters, and close the surface without modifying the document or history. Backdrop clicks MUST NOT dismiss it. Successful application MUST close it and return the user to committed chapters with an accurate summary. Undo and redo MUST use stored document states.

#### Scenario: Cancel a modified draft
- **WHEN** the user changes a preset or script and invokes Cancel, Close, or outer Escape before submission
- **THEN** the candidate and pending calculations are discarded
- **AND** reopening shows the expression, preset, and source metadata from before that canceled edit
- **AND** no additional confirmation dialog or document transaction occurs

#### Scenario: Apply and undo all effects
- **WHEN** the user applies a candidate that changes times, frame data, and metadata
- **THEN** the tool closes and the chapter table shows the committed result
- **AND** table position and chapter selection are preserved where their identities remain available
- **AND** one Undo restores all of those effects
- **AND** Redo restores the same result without rerunning Lua
- **AND** export uses committed values without rerunning the expression

#### Scenario: Deliberately apply the same script again
- **WHEN** the user reopens the tool after applying `t + 5`
- **THEN** retained parameters prepare a new preview from the current committed document
- **AND** another explicit Apply creates a separate five-second shift and history transaction

### Requirement: Review remains responsive and accessible
Wide surfaces MUST use a readable comparison table. Narrow surfaces MUST reflow into labeled chapter entries without losing before/after values or actions. Both hosts MUST provide accessible labels, keyboard navigation, theme-aware emphasis, and focus restoration. Meaning MUST NOT depend on color alone.

#### Scenario: Narrow and short Web viewports
- **WHEN** the tool opens at 320px width, 390×844, or 844×390 with long script and diagnostic text
- **THEN** content remains readable without page-level horizontal overflow
- **AND** Close, Cancel, and Apply do not overlap and remain reachable by actual interaction
- **AND** touch targets are at least 44 by 44 CSS pixels
- **AND** a mobile keyboard does not permanently obscure dismissal or application

#### Scenario: Resize Avalonia and enlarge text
- **WHEN** the user resizes the desktop tool between default, wide, and minimum supported sizes or enlarges text
- **THEN** chapter values and the footer remain accessible without overlapping controls
- **AND** long localized labels remain readable or can be expanded

#### Scenario: Use editing keys and close the tool
- **WHEN** the script editor has focus
- **THEN** Enter inserts a newline and Ctrl/Cmd+Z operates on the script draft
- **AND** a completion popup consumes Escape before the tool does
- **AND** background shortcuts and drops cannot modify the document through the active modal surface
- **WHEN** the tool closes
- **THEN** focus returns to its opener

#### Scenario: Announce preview updates
- **WHEN** a new preview becomes ready
- **THEN** assistive technology receives a concise status summary rather than every comparison row
- **AND** the tool does not move focus to Apply or away from the editor

### Requirement: Cross-host verification covers shared semantics
Implementation MUST verify the same preview fixtures and transaction outcomes in both hosts. Layout evidence MUST include default, wide, and narrow surfaces. Screenshots MUST supplement behavior assertions rather than replace them.

#### Scenario: Verify the selected design
- **WHEN** this change is checked for completion
- **THEN** automated coverage verifies time changes, frame-only changes, property-only changes, invalid results, stale results, cancellation, duplicate submission, and one-step undo
- **AND** Avalonia Headless and Web browser checks verify rendered workflows and accessible actions
- **AND** screenshots under `artifacts/` record the supported layouts

