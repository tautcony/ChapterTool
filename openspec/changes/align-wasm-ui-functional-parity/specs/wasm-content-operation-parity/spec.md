## ADDED Requirements

### Requirement: Shared frame display and conversion bounds remain consistent

Both hosts MUST derive frame text and accuracy from display preferences without content transactions.
Frame edits and signed shifts MUST use the displayed FPS.
Expression preparation MAY supply detected FPS to the selected track when source FPS is absent.
Frame conversion MUST quantize absolute segment boundaries through the chapter conversion calculation.
It MUST preserve segment identities, source FPS metadata, and known document bounds.
Selected-track operations MUST preserve other tracks.

#### Scenario: Source FPS is absent
- **WHEN** either host detects a usable display FPS and the user edits a frame cell
- **THEN** both hosts produce the same chapter time
- **AND** refreshing the display creates no content transaction

#### Scenario: Convert rational FPS
- **WHEN** a reviewed rational-FPS conversion changes a segment timeline
- **THEN** each converted segment duration equals its converted end minus its converted start
- **AND** the known document duration contains every retained segment
- **AND** the operation preserves source FPS metadata and other tracks

### Requirement: Content parameters remain drafts until explicit application

The Web host MUST keep naming, template, numbering, frame-shift, and frame-rate parameters as operation drafts. Parameter changes and candidate preparation MUST NOT change committed chapters, history, format preview, or export. The operation surface MUST identify the current-track scope and excluded separator rows.

#### Scenario: Choose automatic names or load a template
- **WHEN** the user changes the naming draft or loads a valid template
- **THEN** the operation surface shows the draft and scope
- **AND** committed names, preview, export, and history remain unchanged

#### Scenario: Change timing parameters
- **WHEN** the user selects a target frame rate or edits a frame-shift draft
- **THEN** the host prepares a read-only candidate for the current track
- **AND** timing changes require a separate Apply action

### Requirement: Application commits the exact reviewed candidate

The Web host MUST show relevant before/after values and complete change counts. Apply MUST be enabled only for a valid, changed, current candidate. Apply MUST commit the reviewed candidate once without recalculation. Repeated submission MUST create at most one transaction. Cancel, Close, and Escape before submission MUST discard the draft and candidate without content changes.

#### Scenario: Apply and undo a naming candidate
- **WHEN** the user applies reviewed generated or template names
- **THEN** the committed values match the displayed candidate
- **AND** one transaction contains all changes and one Undo restores them
- **AND** Redo and export use stored content without replaying the operation

#### Scenario: Candidate is invalid or unchanged
- **WHEN** a candidate fails validation or equals its base document
- **THEN** Apply remains disabled and the surface explains the reason
- **AND** the document and history cursor remain unchanged

#### Scenario: Cancel a prepared conversion
- **WHEN** the user cancels, closes, or presses Escape before applying a frame-rate candidate
- **THEN** no chapter time, frame rate, segment metadata, or history node changes

### Requirement: Candidate identity protects document and track scope

Candidate application MUST validate the document session, target track, draft revision, and base version. A stale candidate MUST require explicit refresh and another review. Failed application MUST preserve the draft and report the failure. Applying MUST prevent duplicate edits or dismissal until the outcome is known.

#### Scenario: History or track changes during review
- **WHEN** the committed version or selected track changes after candidate preparation
- **THEN** the old candidate cannot commit
- **AND** explicit refresh prepares a new candidate without applying it

#### Scenario: Base changes during submission
- **WHEN** the document changes between the readiness check and transaction commit
- **THEN** the transaction rejects the candidate without partial changes
- **AND** the surface retains parameters for correction or refresh

### Requirement: Preferences never replay content operations

Saving settings or changing output, appearance, or frame-display preferences MUST NOT apply naming, numbering, expression, shift, or conversion drafts. Display refresh MUST read committed content. A successful naming or numbering operation MUST clear its active one-shot intent. Retained template text MUST require a new explicit operation before reuse.

#### Scenario: Save appearance after manually renaming a chapter
- **WHEN** the user applies automatic names, manually renames one chapter, and saves theme or font settings
- **THEN** the manually edited name remains unchanged in the table and export
- **AND** settings save creates no content history node

#### Scenario: Change rounding or output encoding
- **WHEN** the user changes rounding, frame precision, tolerance, format, XML language, encoding, or BOM
- **THEN** the corresponding presentation or output preference changes
- **AND** chapter content and content history remain unchanged
- **AND** a pending content draft is not applied

#### Scenario: Save default format during an active session
- **WHEN** the user saves a new default format while a document has a selected session format
- **THEN** the active session format remains selected
- **AND** the saved default applies at the next application startup

### Requirement: Signed frame shifts use shared timing validation

Both hosts MUST accept integer frame-shift drafts from -1000000 through 1000000. The Web host MUST review shifts using the same current-track scope and FPS basis as Avalonia. Zero MUST produce an unchanged candidate. Invalid times, overflow, or missing required FPS MUST reject the whole candidate.

#### Scenario: Review a valid negative shift
- **WHEN** a shift of minus one frame leaves every participating chapter time valid
- **THEN** both hosts show the same candidate times and related timing fields
- **AND** the Web document changes only after Apply

#### Scenario: Shift would make a chapter time negative
- **WHEN** a signed shift would violate a chapter timing invariant
- **THEN** the entire candidate is invalid
- **AND** neither host commits partial chapter changes

#### Scenario: Zero or out-of-range shift
- **WHEN** the user enters zero or a value outside the supported integer range
- **THEN** zero is reported as unchanged and an out-of-range value has a validation error
- **AND** neither value creates a history node

### Requirement: Numbering controls reflect the existing Core policy

Both hosts MUST expose numbering offsets from 0 through 1000. Zero MUST remain the neutral draft value. Negative, fractional, and out-of-range UI input MUST NOT be advertised as a valid numbering operation. Applying a nonzero offset MUST use the Core numbering result and separator handling.

#### Scenario: Apply a positive offset
- **WHEN** the user previews and applies an offset of two to a track with ordinary chapters and separators
- **THEN** ordinary chapter numbers match the Core candidate in both hosts
- **AND** separator numbering follows the existing Core policy

#### Scenario: Enter a negative offset in either host
- **WHEN** the user attempts to enter a negative numbering offset
- **THEN** the control prevents acceptance or reports a validation error
- **AND** no operation silently treats that input as a supported negative offset

### Requirement: Portable operations produce equivalent host outcomes

Verification MUST use matching inputs, target scopes, operations, and output preferences in both hosts. It MUST compare relevant committed document fields, one-step Undo/Redo outcomes, and serialized output. Web command enabled states and option labels MUST expose the same portable prerequisites.

#### Scenario: Run the same operation sequence
- **WHEN** tests apply naming, numbering, frame edit, signed shift, conversion, and expression fixtures through both hosts
- **THEN** relevant committed values and exported bytes match for the same preferences
- **AND** each accepted operation has the expected atomic history behavior

#### Scenario: Present localized XML choices
- **WHEN** the user changes UI language with XML output selected
- **THEN** Web XML choices expose localized language names and stable language codes
- **AND** the selected code remains unchanged and the selector is disabled for non-XML output

### Requirement: Portable tool surfaces remain localized and accessible

All new or changed Web operation, editor, settings, shortcut, and log controls MUST use shared locale resources. Icon actions MUST have accessible names. Modal focus containment and restoration MUST remain intact. Default, wide, narrow, narrow-short, and landscape layouts MUST keep primary actions reachable. Behavior assertions MUST accompany screenshot evidence.

#### Scenario: Switch among supported locales
- **WHEN** the user selects English, Chinese, or Japanese and opens a changed tool
- **THEN** labels, statuses, validation, and accessible names resolve for that locale
- **AND** chapter text and operation identity remain unchanged

#### Scenario: Use the changed tools at supported viewport sizes
- **WHEN** browser checks run at 1280x800, 1920x1080, 390x844, 390x640, and 844x390
- **THEN** changed tools and primary actions can be reached and activated without page-level horizontal overflow
- **AND** closing each modal returns focus to a valid opener or workspace target
- **AND** screenshots record layouts in addition to those interaction assertions
