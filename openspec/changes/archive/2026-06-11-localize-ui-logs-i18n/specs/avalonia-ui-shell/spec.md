## ADDED Requirements

### Requirement: Avalonia UI text is localized through resources
The Avalonia UI shell SHALL render user-facing static text through Avalonia localization resources for Simplified Chinese, English, and Japanese.

#### Scenario: Main window static text uses localized resources
- **WHEN** the main window is rendered in any supported UI language
- **THEN** visible labels, button content, menu headers, tooltips, DataGrid headers, tab labels, and option captions SHALL come from localized resources rather than hard-coded mixed-language literals

#### Scenario: Secondary tool static text uses localized resources
- **WHEN** preview, log, color settings, language, expression, template names, zones, or forward-shift tools are opened in any supported UI language
- **THEN** window titles, labels, buttons, placeholders, and option captions SHALL come from localized resources for the active language

#### Scenario: Runtime language switch refreshes visible resources
- **WHEN** the user changes the UI language from the language tool
- **THEN** open Avalonia views SHALL refresh localized static text without requiring an application restart where Avalonia resource refresh is supported

#### Scenario: Unsupported language falls back predictably
- **WHEN** settings contain an unsupported or blank UI language value
- **THEN** the UI shell SHALL use the Simplified Chinese resource set and SHALL NOT render localization keys as normal visible text

### Requirement: UI prompts and state messages are localized by semantic key
The Avalonia shell SHALL represent user-facing prompts, status, and command feedback as semantic localized messages instead of storing hard-coded English or Chinese display strings in ViewModels.

#### Scenario: Load status is localized
- **WHEN** a source is loaded successfully
- **THEN** `StatusText` SHALL display the localized active-language equivalent of the loaded-chapter count message with the chapter count formatted into the message

#### Scenario: Save status is localized
- **WHEN** chapters are saved successfully or saving fails
- **THEN** `StatusText` SHALL display a localized success or failure message in the active UI language

#### Scenario: Dialog prompts are localized
- **WHEN** the shell displays confirmation, error, unsupported-feature, empty-state, placeholder, or command-feedback prompts
- **THEN** each visible prompt title, message, and action caption SHALL use the active UI language resource set

#### Scenario: Frame-rate detection status is localized
- **WHEN** auto frame-rate detection updates status text
- **THEN** the status message SHALL be localized while preserving the detected frame-rate display name and confidence value

#### Scenario: Technical diagnostics are mapped for users
- **WHEN** a known diagnostic code reaches the Avalonia shell
- **THEN** the shell SHALL display a localized user-facing summary for that code and retain the original diagnostic message as technical detail for logs

### Requirement: Language tool supports the target languages
The Avalonia language tool SHALL allow users to choose Simplified Chinese, English, and Japanese with localized display names.

#### Scenario: Language options are complete
- **WHEN** the language tool is opened
- **THEN** it SHALL show Simplified Chinese, English, and Japanese options with stable culture tags `zh-CN`, `en-US`, and `ja-JP`

#### Scenario: Language selection persists
- **WHEN** the user applies a language selection
- **THEN** the selected culture tag SHALL be persisted to settings and applied to the current application localization manager
