## ADDED Requirements

### Requirement: Browser shortcuts use the shared action catalog

The Web host MUST use stable action identifiers from the shared shortcut catalog. It MUST expose load, save, reload, preview, log, previous clip, next clip, insert, delete, Undo, and Redo actions with their catalog defaults and applicable fixed aliases. A configurable Shortcuts settings page MUST support recording, clearing, per-action reset, and saving editable mappings.

#### Scenario: Load default shortcuts
- **WHEN** no browser shortcut overrides exist
- **THEN** Web routes catalog defaults and applicable fixed aliases to available commands
- **AND** visible hints use that same mapping

#### Scenario: Customize or clear an editable action
- **WHEN** the user records or clears an editable action and saves valid settings
- **THEN** runtime routing and visible hints use the saved value
- **AND** a replaced default no longer triggers the action unless it is a fixed alias or remains assigned

#### Scenario: Reset one action
- **WHEN** the user resets one shortcut draft
- **THEN** that action returns to its catalog default
- **AND** other shortcut drafts remain unchanged

### Requirement: Shortcut validation protects usable mappings

The Web host MUST validate canonical gestures without depending on Avalonia key types. It MUST reject invalid, duplicate, fixed-alias-conflicting, or unsupported captured gestures with localized errors. It MUST keep the preceding active mapping until valid settings are saved. Browser-specific restrictions MUST be explicit and MUST preserve existing supported intercepted defaults.

#### Scenario: Duplicate or invalid draft
- **WHEN** two commands share a conflicting gesture or a row contains an invalid gesture
- **THEN** settings marks the invalid rows and prevents Save
- **AND** the active mapping remains usable

#### Scenario: Browser gesture cannot be delivered
- **WHEN** a user attempts to capture an unsupported browser-reserved gesture
- **THEN** the row explains the restriction and does not save it as a functioning shortcut

### Requirement: Routing and default suppression use one active mapping

Dispatch, hints, and application shortcut default suppression MUST derive from the same active mapping. Editable controls MUST keep text-editing gestures. Active dialogs MUST prevent background commands. Unavailable commands MUST not mutate the document.

#### Scenario: Invoke row commands with table focus
- **WHEN** the user presses Insert or Delete with an eligible table selection and no active cell draft
- **THEN** the corresponding Core edit runs once
- **AND** text input focused inside a cell retains its own editing behavior

#### Scenario: Navigate clips
- **WHEN** the user presses the configured previous/next clip gesture with eligible workspace focus
- **THEN** the active clip changes within the valid clip range
- **AND** reaching a range boundary does not wrap or select an invalid index

#### Scenario: Use a customized gesture
- **WHEN** an eligible workspace target receives an active customized application gesture
- **THEN** the mapped command executes and its conflicting browser default is suppressed
- **AND** unrelated gestures are not suppressed as application actions

#### Scenario: Modal or editor owns focus
- **WHEN** an expression editor, settings control, or active dialog owns input
- **THEN** background row, clip, load, and document-history commands do not run
- **AND** local editing and inner dismissal remain available

### Requirement: Shortcut persistence is additive and compatible

The Web settings document MUST retain its current storage key and existing application, theme, and font content. An optional shortcuts child MUST store canonical action mappings. Existing version-one settings without shortcuts MUST load defaults without a rewrite. Unknown actions and malformed stored mappings MUST normalize to a usable mapping without blocking other valid settings.

#### Scenario: Load existing version-one settings
- **WHEN** the stored document contains valid application, theme, and font settings but no shortcuts child
- **THEN** those values are restored and shortcut defaults are active
- **AND** loading alone does not rewrite storage

#### Scenario: Save shortcuts and reopen
- **WHEN** valid shortcut settings are saved and the application is reopened
- **THEN** action IDs and canonical mappings are restored
- **AND** unrelated output and appearance preferences are preserved

### Requirement: Settings drafts have explicit save and discard behavior

The Web settings tool MUST compare normalized drafts with its entry snapshot. Save MUST validate and persist one aggregate before activating saved settings and closing. A validation or persistence failure MUST preserve the tool, draft, and last valid active settings. Reset MUST modify only the draft. Intentional Cancel MUST discard and close.

#### Scenario: Save valid settings
- **WHEN** the user saves a valid settings draft
- **THEN** one aggregate is persisted and the saved runtime preferences become active
- **AND** the settings tool closes without changing chapter content or history

#### Scenario: Save fails
- **WHEN** storage rejects settings persistence
- **THEN** the tool remains open with a recoverable localized error and the draft
- **AND** active preferences and shortcuts retain their last valid values
- **AND** chapter content and history remain unchanged

#### Scenario: Reset then cancel
- **WHEN** the user resets settings and invokes Cancel
- **THEN** saved values and active settings remain unchanged
- **AND** reopening restores the entry saved values

### Requirement: Changed settings require a close decision

Closing the Web settings tool by Close or Escape with changed drafts MUST offer Discard and Keep editing. Unchanged settings MUST close directly. Keep editing MUST preserve draft values and focus. Discard MUST restore the entry active snapshot and close without persistence. This requirement concerns the settings modal, not page-leave handling.

#### Scenario: Keep a changed draft
- **WHEN** the user closes changed settings and selects Keep editing
- **THEN** the settings tool remains open with all draft values
- **AND** focus returns to the settings surface

#### Scenario: Discard through close confirmation
- **WHEN** the user closes changed settings and selects Discard
- **THEN** the entry active settings remain in effect and no storage write occurs
- **AND** the modal closes and restores focus to its opener

#### Scenario: Close unchanged settings
- **WHEN** normalized draft values equal the entry snapshot and the user presses Escape
- **THEN** the tool closes without an extra confirmation
