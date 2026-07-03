## ADDED Requirements

### Requirement: Main-window selectors expose readable display content
The Avalonia main window SHALL render clip and XML language selector options with user-readable display content while preserving the existing underlying selection values used by commands, import/export, settings, and shortcuts.

#### Scenario: Clip selector displays main content with remarks
- **WHEN** a source load result contains multiple clip, playlist, program-chain, or edition options
- **THEN** the clip selector SHALL display each option with the primary source content first and secondary details such as chapter count as remark-style supporting content
- **AND** selecting an option SHALL continue to update `SelectedClipIndex` and the current chapter rows exactly as before

#### Scenario: XML language selector displays readable language names
- **WHEN** XML language selection is shown
- **THEN** the selector SHALL display each language option with both the language code and a readable language name
- **AND** changing the selector SHALL continue to update `XmlLanguage` to the selected ISO code used for XML export
