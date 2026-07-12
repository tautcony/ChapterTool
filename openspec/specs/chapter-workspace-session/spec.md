# chapter-workspace-session Specification

## Purpose
Define the explicit chapter workspace/session boundary that owns GUI chapter state, async revisions, projection state, and export preferences.

## Requirements

### Requirement: Explicit chapter workspace owns session lifecycle
The Avalonia shell SHALL own loaded chapter session state through an explicit workspace/session abstraction rather than an ad-hoc collection of independent fields on the main window ViewModel.

#### Scenario: Workspace exists after construction
- **WHEN** the main window ViewModel is constructed for normal GUI use
- **THEN** it SHALL hold or be composed with a single chapter workspace/session object that owns source path metadata, clip session state, the current editable chapter set, projection options, and export preferences used by commands

#### Scenario: Load replaces workspace session atomically
- **WHEN** a source load succeeds
- **THEN** the workspace SHALL replace path metadata, clip session, current chapter set, and derived row projection inputs as one coherent session update
- **AND** partial leftovers from the previous session's clip-combine flags or split backup state SHALL NOT remain active

#### Scenario: Failed load does not corrupt the active session
- **WHEN** a source load fails after a previous successful session exists
- **THEN** the previous successful workspace session SHALL remain the active session
- **AND** status/progress feedback SHALL still report the failure

### Requirement: Async operations commit only to the current workspace revision
Load, append, and other asynchronous session-mutating operations SHALL be associated with a workspace revision (or equivalent operation generation). Progress updates and final results from an operation SHALL be applied only when that operation still targets the current revision; stale operations SHALL NOT overwrite path, clip session, chapter set, rows, status, or progress of a newer session.

#### Scenario: Newer load supersedes an in-flight older load
- **WHEN** a source load is still running and a newer source load starts
- **THEN** the shell/workspace SHALL advance the active operation revision for the newer load
- **AND** when the older load later completes successfully or fails, it SHALL NOT overwrite the newer load's path, clip session, chapter rows, status, or progress

#### Scenario: Late progress from an older load is ignored
- **WHEN** an older in-flight load reports intermediate progress after a newer load has become current
- **THEN** that late progress SHALL NOT update the visible progress value or progress status for the current session

#### Scenario: Stale append does not overwrite a newer session
- **WHEN** an append-MPLS operation is in flight against a session and a newer successful load replaces the workspace session before the append completes
- **THEN** the late append result SHALL NOT mutate the newer session's path, clip session, combine state, chapter rows, status, or progress
- **AND** existing regression coverage for "older append does not overwrite newer load" SHALL remain a mandatory unit-test scenario through the decomposition

#### Scenario: Append validates session identity as well as operation revision
- **WHEN** append-MPLS completes
- **THEN** it SHALL apply only if both the operation revision is still current and the session identity expected at append start still matches the active workspace session (or equivalent typed session token)
- **AND** a combine/restore or load that replaced the session during the append SHALL cause the late append result to be discarded

#### Scenario: Cancellation does not commit a partial session mutation
- **WHEN** a load or append operation is cancelled through its cancellation token before a successful commit
- **THEN** the workspace SHALL NOT apply a partial path/clip/chapter replacement from that cancelled operation
- **AND** any already-current newer session SHALL remain unchanged

#### Scenario: Failed stale operation does not clobber current status incorrectly
- **WHEN** an older load or append fails after a newer load has become current
- **THEN** the failure diagnostics from the stale operation SHALL NOT replace the newer session's committed path and chapter rows
- **AND** the shell MAY ignore the stale failure for status or may log it without treating it as the active session result

### Requirement: Clip session is a typed mode, not multi-flag state
The workspace SHALL represent multi-clip selection and combined-clip mode as an explicit typed session state with pure transitions.

#### Scenario: Multi-clip source loads in split mode
- **WHEN** a multi-entry MPLS or DVD IFO group is loaded successfully
- **THEN** the clip session SHALL be in split/multi-clip mode with selectable entries and a selected index
- **AND** combine availability SHALL be derived from that mode and entry formats rather than a separate sticky boolean that can desynchronize

#### Scenario: Combine transitions to combined mode
- **WHEN** the user combines a multi-clip session successfully
- **THEN** the clip session SHALL enter combined mode that retains the original multi-entry group for restore
- **AND** the current editable chapter set SHALL be the combined chapter set
- **AND** the UI combine affordance SHALL present as checked/combined

#### Scenario: Restore transitions back to split mode
- **WHEN** the user clears combine on a combined session
- **THEN** the clip session SHALL restore the original multi-entry group and selected entry rows
- **AND** combined-only backup state SHALL be cleared
- **AND** the UI combine affordance SHALL present as unchecked/split

#### Scenario: Append MPLS updates the retained original group
- **WHEN** append-MPLS succeeds while the session is combined or about to be combined
- **THEN** the retained original multi-entry group SHALL include the appended entries
- **AND** the active combined chapter set SHALL reflect the append result without inventing a third parallel clip store

#### Scenario: Frame and edit updates write back to the correct clip ownership
- **WHEN** the user edits chapters or updates frame info
- **THEN** the workspace SHALL write the resulting chapter set back into the active clip session according to mode (selected split entry vs combined entry)
- **AND** callers SHALL NOT need a separate `belongsToSelectedClip` boolean to decide ownership

### Requirement: Projection state is owned by the workspace
Naming mode, order shift, expression application, expression text/preset/source metadata, and last-successful expression projection cache SHALL be owned by a projection state surface on the workspace.

#### Scenario: Projection options feed rows and export identically
- **WHEN** projection options change or the current chapter set changes
- **THEN** chapter rows and preview/save projection inputs SHALL be derived from the same workspace projection state

#### Scenario: Invalid mid-edit expression keeps last successful projection
- **WHEN** expression application is enabled and the current expression is temporarily invalid while a previous successful projection exists
- **THEN** displayed rows MAY continue showing the last successful projected output until a valid projection replaces it
- **AND** the diagnostic path SHALL still surface the current expression failure

#### Scenario: Expression state mutation does not thrash intermediate refreshes
- **WHEN** several expression-related fields are updated as one apply operation
- **THEN** the workspace/ViewModel SHALL avoid intermediate row refreshes that use partially updated expression fields

### Requirement: Export preferences are a workspace snapshot
Save format, XML language, text encoding, BOM emission, and effective save-directory resolution inputs SHALL be readable as an export-preference snapshot from the workspace for save and preview.

#### Scenario: Preview and save read one preference snapshot
- **WHEN** the user previews or saves chapters
- **THEN** both paths SHALL read export preferences from the workspace snapshot rather than reconstructing ad-hoc option bags from unrelated UI controls

#### Scenario: Session save format is distinct from settings defaults where required
- **WHEN** live settings apply a default save format after startup
- **THEN** the workspace session save format SHALL follow the already-documented live-apply policy (startup applies default format; live settings do not forcibly reset the in-session format)
- **AND** that policy SHALL be implemented through the workspace preference surface rather than duplicated owner-specific branches in multiple ViewModels

### Requirement: Secondary tools consume narrow workspace ports
Secondary tool ViewModels SHALL depend on narrow workspace or shell ports for the capabilities they need, not on the full main-window ViewModel type for unrelated session fields.

#### Scenario: Expression tool uses an expression port
- **WHEN** the expression tool is constructed
- **THEN** it SHALL depend on an expression/session port that can read and apply expression script state and diagnostics formatting
- **AND** it SHALL NOT require access to unrelated main-window commands such as clip combine or zones

#### Scenario: Settings live-apply uses a preference sink
- **WHEN** the settings tool applies runtime-safe preferences live
- **THEN** it SHALL call a preference-sink/workspace API that applies language, save directory, output defaults, frame tolerance, and related session preferences
- **AND** it SHALL NOT need the entire main-window command surface to do so

#### Scenario: Preview format selector uses an export-format port
- **WHEN** the preview tool changes output format for preview rendering
- **THEN** it SHALL update export format through a narrow export-preference port
- **AND** preview content SHALL still match save projection rules for the same preferences

### Requirement: Workspace transitions are testable without Avalonia controls
Clip mode transitions, load/replace, append, restore, projection refresh, and export-preference snapshot construction SHALL be unit-testable without constructing Avalonia windows or controls.

#### Scenario: Combine and restore are covered by pure session tests
- **WHEN** unit tests drive combine and restore transitions on a multi-entry MPLS-like session
- **THEN** they SHALL assert typed mode, retained original entries, current chapter set, and derived combine capability without opening UI

#### Scenario: Load replacement clears previous combined mode
- **WHEN** unit tests load a combined session and then load a different successful source
- **THEN** the new session SHALL not retain the previous combined backup or checked combine state

#### Scenario: Concurrent load and append races are unit-tested
- **WHEN** unit tests overlap an older load with a newer load, or an in-flight append with a newer load, using deterministic controlled load services
- **THEN** they SHALL assert that only the current revision's path, rows, and combine state remain after late completions
- **AND** late progress from superseded operations SHALL NOT change the current progress state
