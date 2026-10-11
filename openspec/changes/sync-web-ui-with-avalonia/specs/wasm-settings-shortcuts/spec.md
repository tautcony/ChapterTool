## MODIFIED Requirements

### Requirement: Settings drafts have explicit save and discard behavior

The Web settings tool MUST compare normalized drafts with its entry snapshot. Valid draft changes MUST apply runtime preferences immediately without persistence or content transactions. Save MUST validate and persist one aggregate before closing. A validation or persistence failure MUST preserve the tool, draft, and last valid live preferences. Reset MUST update the draft and its valid live preview without persistence. Intentional Cancel MUST restore the entry runtime snapshot and close. Default output preferences MUST remain separate from an active document's session output choices.

#### Scenario: Save valid settings
- **WHEN** the user changes and saves a valid settings draft
- **THEN** the runtime preview is active before Save and one aggregate is persisted on Save
- **AND** the settings tool closes without changing chapter content or history

#### Scenario: Save fails
- **WHEN** storage rejects settings persistence
- **THEN** the tool remains open with a recoverable localized error and the draft
- **AND** active preferences and shortcuts retain their last valid live values
- **AND** chapter content and history remain unchanged

#### Scenario: Reset then cancel
- **WHEN** the user resets settings and invokes Cancel
- **THEN** saved values remain unchanged and active preferences return to the entry snapshot
- **AND** reopening restores the entry saved values
