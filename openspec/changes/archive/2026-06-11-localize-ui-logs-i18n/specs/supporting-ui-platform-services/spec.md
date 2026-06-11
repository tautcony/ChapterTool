## ADDED Requirements

### Requirement: Avalonia localization resources are complete
The application SHALL package complete Simplified Chinese, English, and Japanese localization resources for Avalonia UI, prompts, and user-facing message formatting.

#### Scenario: Supported cultures have matching keys
- **WHEN** localization resources are validated by tests
- **THEN** the Simplified Chinese, English, and Japanese resource sets SHALL contain the same required keys

#### Scenario: Localized format strings accept required arguments
- **WHEN** a localized message key defines formatting arguments
- **THEN** every supported culture SHALL provide a compatible format string for those arguments

#### Scenario: UTF-8 resources remain valid
- **WHEN** localized Chinese or Japanese resources are read, built, or packaged
- **THEN** visible text SHALL remain valid UTF-8 and SHALL NOT contain mojibake such as `杞藉叆` or `淇濆瓨`

### Requirement: Localization is an Avalonia presentation service
The application SHALL use an Avalonia-facing localization manager for UI resource selection and code-side message lookup instead of relying on the historical Core `ILocalizationService` implementation.

#### Scenario: Composition provides one localization manager
- **WHEN** the Avalonia application composition root constructs the main window and auxiliary windows
- **THEN** they SHALL share a single localization manager instance for current culture, resource lookup, formatting, and culture-change notifications

#### Scenario: Core remains presentation-language independent
- **WHEN** Core services import, transform, edit, or export chapters
- **THEN** they SHALL NOT depend on Avalonia localization resources or current UI culture to perform domain operations

#### Scenario: Historical localization service is removed or isolated
- **WHEN** the Avalonia app is built after localization migration
- **THEN** the historical dictionary-based `ILocalizationService` path SHALL NOT be required for Avalonia UI text, status text, or application log rendering

### Requirement: Application logs are localizable structured entries
The application log service SHALL store user-facing log events as structured message keys with arguments and optional technical details.

#### Scenario: Log window formats entries in active language
- **WHEN** the log window displays log entries
- **THEN** each user-facing log message SHALL be formatted using the active UI language resource set at display time

#### Scenario: Existing log entries refresh after language switch
- **WHEN** log entries already exist and the user changes the UI language
- **THEN** the log window SHALL display those entries in the newly active language while preserving timestamps and technical details

#### Scenario: Technical details remain available
- **WHEN** a log event includes paths, external process output, exception text, diagnostic messages, or other troubleshooting details
- **THEN** the localized log message SHALL retain those details without translating or discarding them

### Requirement: User prompts are localizable messages
The application SHALL format user-facing prompts through localization resources and SHALL keep prompt metadata separate from rendered text where practical.

#### Scenario: Dialog request text is localized
- **WHEN** a dialog request is created for confirmation, input, warning, or error display
- **THEN** its visible title, message, and action captions SHALL be resolved from the active UI language resource set

#### Scenario: Unsupported feature prompts are localized
- **WHEN** a platform-gated or unavailable feature is shown to the user
- **THEN** the visible unsupported-feature prompt SHALL be localized while retaining any technical platform detail separately

#### Scenario: Prompt text refreshes after language switch
- **WHEN** a prompt-producing ViewModel action runs after the user changes UI language
- **THEN** newly produced prompt text SHALL use the newly active language

### Requirement: Language settings migrate to explicit culture tags
The application SHALL persist supported UI languages as explicit culture tags while preserving legacy blank/default settings behavior.

#### Scenario: Blank legacy language uses Simplified Chinese
- **WHEN** settings contain a blank language value
- **THEN** localization SHALL treat it as Simplified Chinese for resource selection

#### Scenario: Saved language is explicit
- **WHEN** the user saves a UI language selection
- **THEN** settings SHALL store one of `zh-CN`, `en-US`, or `ja-JP`

#### Scenario: Unsupported saved language falls back safely
- **WHEN** settings contain an unsupported language tag
- **THEN** localization SHALL fall back to Simplified Chinese and SHALL keep the application usable
