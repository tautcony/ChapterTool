## ADDED Requirements

### Requirement: Node inspection is a read-only parent-relative query

The session SHALL provide typed details for every retained history node. A non-root node's details SHALL describe the change from its own parent to that node. Inspection SHALL NOT move the cursor, alter preferred children, change mutation revision, create history nodes, or rerun an operation. One result SHALL use one captured immutable history version.

#### Scenario: Inspect an alternate branch
- **WHEN** the current cursor is on one branch and a query inspects a node on another branch
- **THEN** the query returns that node's changes relative to its parent
- **AND** the current document, cursor, preferred children, revision, and retained nodes remain unchanged

#### Scenario: Inspect while navigation occurs
- **WHEN** history navigation occurs during an inspection query
- **THEN** the query returns a consistent captured result or an explicit cancellation result
- **AND** it does not mix values from different history versions

#### Scenario: Query cannot complete
- **WHEN** the requested node is missing, the session has ended, the caller cancels, or recoverable resource exhaustion prevents inspection
- **THEN** the query reports a typed unavailable, canceled, or resource-failure result
- **AND** it does not publish an intermediate document or mutate history

### Requirement: History details preserve typed and structural differences

Node details SHALL match objects by stable identity and expose changed values with before/after presence. Details SHALL cover editable chapter fields, chapter membership and order, track membership and order, track names, segment membership and order, segment placement and source/media properties, and editable document properties. Absence, null/unknown values, and empty text SHALL remain distinguishable.

#### Scenario: One chapter has several changed fields
- **WHEN** one commit changes a chapter's name, start time, and frame information
- **THEN** details expose all changed fields and their exact before/after values
- **AND** the summary counts one modified chapter

#### Scenario: Chapters are added and deleted
- **WHEN** a commit adds one chapter and deletes another
- **THEN** details identify the added and deleted objects and their available values
- **AND** added and deleted chapters are not also counted as modified

#### Scenario: Order or track membership changes
- **WHEN** retained chapter identities change order or move between retained tracks
- **THEN** details expose their before/after positions and track membership
- **AND** a moved stable identity is not represented as unrelated deletion and addition

#### Scenario: Structural operations create new identities
- **WHEN** merge, split, or append creates or removes track, chapter, or segment identities
- **THEN** details expose the actual retained identities and structural changes
- **AND** the query does not invent correspondence between different identities

#### Scenario: No chapter field changes
- **WHEN** a commit changes only document frame rate, a track name, or segment placement or source metadata
- **THEN** details expose those property changes
- **AND** the summary does not claim that chapter fields changed

#### Scenario: Empty and absent values differ
- **WHEN** a field changes between empty text, unknown/null, and a present value, or its object is added or removed
- **THEN** the typed result preserves each state without collapsing them into the same string

### Requirement: History details retain historical time and frame meaning

Details SHALL preserve exact time values and historical frame-rate context. Signed time differences SHALL use the node's before/after values. Current host display preferences SHALL NOT change historical stored values or operation parameters. Missing historical rate information SHALL remain explicitly unknown.

#### Scenario: Main-window FPS changes after an edit
- **WHEN** the user changes the display FPS and then inspects an earlier frame edit
- **THEN** details retain that edit's stored frame values and available historical rate context
- **AND** the current display FPS is not substituted as the edit's effective rate

#### Scenario: A submillisecond value changes
- **WHEN** an edit changes time by less than the ordinary display precision
- **THEN** the detail result retains the exact before/after values and signed delta

### Requirement: Root inspection exposes the initial document

Root details SHALL expose the initial document overview and ordered read-only chapter values. The root SHALL be identified as the initial state and SHALL NOT have a fabricated parent or change set.

#### Scenario: Inspect the load node
- **WHEN** a user queries the history root after later edits
- **THEN** the result describes the initial document's source, tracks, chapters, and available properties
- **AND** the initial values remain separate from current document values
- **AND** no parent-relative edit is reported

### Requirement: Operation metadata is immutable and atomically committed

History nodes SHALL support a stable operation kind, optional typed captured parameters, and publication time. Available metadata SHALL be committed atomically with the node and its changes. Descriptions SHALL remain diagnostic fallback data. Callers that provide only an operation description SHALL remain supported without description parsing or invented metadata.

#### Scenario: A reviewed expression is applied
- **WHEN** a prepared expression candidate is committed after the live draft changes
- **THEN** the history node retains the script, scope, and effective parameters captured for that candidate
- **AND** it does not retain the later draft values

#### Scenario: A transaction does not commit
- **WHEN** a request is invalid, stale, canceled, fails publication, or makes no canonical change
- **THEN** no node or standalone operation metadata is added

#### Scenario: A transaction is retried or restored
- **WHEN** an idempotent transaction is retried or its node is visited through undo or redo
- **THEN** the original operation metadata and publication time remain unchanged
- **AND** no operation is rerun to rebuild metadata

#### Scenario: A caller has no structured metadata
- **WHEN** a supported caller commits using an operation description only
- **THEN** the node still supports exact change inspection and normal restoration
- **AND** missing parameters or time remain explicitly unavailable

### Requirement: Inspection preserves delta-based session ownership

History SHALL retain delta-based node storage. Inspection SHALL NOT attach a complete reconstructed document to every node or prune retained history. Detail work SHALL support cancellation and remain owned by the current in-memory session. History export and cross-session history persistence SHALL NOT be introduced by this capability change.

#### Scenario: Inspect a deep history
- **WHEN** a session has a long linear path and retained alternate branches
- **THEN** traversal and private reconstruction do not require recursive call depth proportional to history length
- **AND** all retained nodes remain reachable
- **AND** inspection does not add permanent full-document copies to those nodes

#### Scenario: The session ends during inspection
- **WHEN** the document session ends while a detail query runs
- **THEN** its lifetime token cancels outstanding detail work
- **AND** host-owned references to old detail results can be released
