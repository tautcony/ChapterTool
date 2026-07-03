# supporting-ui-platform-services Specification

## Purpose
TBD - created by archiving change rewrite-avalonia-dotnet10. Update Purpose after archive.
## Requirements
### Requirement: Auxiliary Avalonia windows
The system SHALL provide Avalonia preview, log, color settings, about, and updater windows without WinForms dependencies.

#### Scenario: Preview window is reusable
- **WHEN** preview is opened and then closed
- **THEN** it SHALL hide or be reusable on next open and SHALL display current TXT/OGM preview text

#### Scenario: Log window uses log service
- **WHEN** log entries change
- **THEN** the log window SHALL display current log content and copy selected text through clipboard service

#### Scenario: Color settings preserve six slots
- **WHEN** color settings are loaded or saved
- **THEN** the six legacy color slots SHALL retain their documented order

### Requirement: Settings and legacy migration
The system SHALL use typed cross-platform settings while reading compatible legacy configuration.

#### Scenario: Legacy chaptertool settings migrate
- **WHEN** legacy `chaptertool.json` contains saving path, language, location, mkvToolnixPath, or eac3toPath keys
- **THEN** settings SHALL load them and normalize future writes to the new settings model

#### Scenario: Legacy color config migrates
- **WHEN** legacy `color-config.json` exists in compatible locations
- **THEN** theme settings SHALL read valid six-slot color values and ignore invalid values safely

### Requirement: Platform service abstractions
The application SHALL access dialogs, clipboard, shell, process execution, settings, localization, windows, privileges, file association, and native dependencies through injectable services.

#### Scenario: Dialog and clipboard are testable
- **WHEN** a ViewModel needs confirmation, input, error display, or clipboard access
- **THEN** it SHALL call service interfaces rather than WinForms APIs

#### Scenario: Process runner captures failures
- **WHEN** an external process is executed
- **THEN** the process result SHALL include stdout, stderr, exit code, timeout, cancellation, command, and working directory

#### Scenario: Process runner decodes redirected output explicitly
- **WHEN** an external process writes redirected stdout or stderr containing non-ASCII text
- **THEN** the process runner SHALL decode output with an explicit platform-appropriate encoding instead of relying on an interactive terminal code page

### Requirement: External dependency location
The system SHALL centralize discovery and configuration for mkvextract and eac3to while keeping default MP4 chapter reading independent from external tool configuration.

#### Scenario: Configured path wins
- **WHEN** a dependency path exists in migrated settings
- **THEN** tool locator SHALL use it before registry or installation discovery

#### Scenario: Environment path wins before platform install discovery
- **WHEN** no configured dependency path exists and the requested executable exists in the environment/PATH search directories
- **THEN** tool locator SHALL use the environment/PATH executable before probing platform installation locations

#### Scenario: Windows MKVToolNix registry discovery
- **WHEN** `mkvextract` is requested on Windows and no configured or PATH executable is found
- **THEN** tool locator SHALL probe MKVToolNix uninstall registry entries and resolve `mkvextract.exe` from the discovered install directory when present

#### Scenario: macOS MKVToolNix app bundle discovery
- **WHEN** `mkvextract` is requested on macOS and no configured or PATH executable is found
- **THEN** tool locator SHALL probe MKVToolNix app bundles such as `/Applications/MKVToolNix-96.0.app/Contents/MacOS/mkvextract`

#### Scenario: Missing tool is structured
- **WHEN** a tool cannot be found
- **THEN** callers SHALL receive a missing-dependency result rather than a UI prompt from Core

#### Scenario: Unix/Linux discovery relies on PATH search
- **WHEN** `mkvextract` is requested on Linux or another Unix-like platform and no configured path exists
- **THEN** tool locator SHALL search only configured path and environment/PATH directories without probing registry or app-bundle installation locations

#### Scenario: Configured directory path resolves executable
- **WHEN** a configured MKVToolNix path points to a directory rather than an executable file
- **THEN** tool locator SHALL resolve the platform-appropriate executable name (`mkvextract.exe` on Windows, `mkvextract` elsewhere) within that directory

#### Scenario: MP4 dependency does not require tool or native library lookup
- **WHEN** MP4 chapter import is performed by the managed ATL.NET adapter
- **THEN** dependency discovery SHALL NOT require external tool location or `libmp4v2` native library resolution

### Requirement: Windows-only isolation
Windows-specific services SHALL be hidden, degraded, or reported unsupported on non-Windows platforms.

#### Scenario: File association is platform-gated
- **WHEN** `.mpls` file association is requested off Windows
- **THEN** the system SHALL hide the action or return unsupported-platform status

#### Scenario: Elevation is explicit
- **WHEN** an operation requires administrator rights
- **THEN** the app SHALL remain asInvoker by default and request elevation only through privilege service

#### Scenario: Registry is not a default settings dependency
- **WHEN** settings such as language, save directory, color slots, and external tool paths are loaded or saved
- **THEN** the application SHALL use typed cross-platform files by default and SHALL only use registry-specific integration behind explicitly Windows-gated services

### Requirement: Localization and resources
The Avalonia application SHALL support default Chinese and `en-US` resources without WinForms `ApplyResources`.

#### Scenario: Language loads from settings
- **WHEN** settings contain blank language or `en-US`
- **THEN** localization SHALL apply the matching resource set and persist changes through settings

#### Scenario: Assets are packaged
- **WHEN** the app is built or published
- **THEN** icons and required images SHALL be available through Avalonia-compatible resource packaging

### Requirement: Application composition root
The Avalonia application SHALL centralize construction of services, ViewModels, and windows in application startup composition.

#### Scenario: Main window is resolved from composition
- **WHEN** the application starts normally
- **THEN** `App` SHALL resolve `MainWindow` and its dependencies from the composition root rather than constructing the application object graph across `App`, `MainWindow`, and service constructors

#### Scenario: Services are substitutable in tests
- **WHEN** tests construct the main shell or ViewModels
- **THEN** dialog, clipboard, shell, settings, window, process, external tool, native dependency, load, save, frame-rate, editing, and importer services SHALL be replaceable through registered interfaces or factories

#### Scenario: Composition validates required registrations
- **WHEN** a composition smoke test resolves the main window, primary ViewModels, window service, and importer registry
- **THEN** missing required services SHALL be detected before user workflows are exercised manually

### Requirement: Secondary windows use dedicated views and ViewModels
Auxiliary UI tools SHALL be implemented as dedicated Avalonia views with ViewModels instead of large imperatively generated control trees.

#### Scenario: Window service displays views
- **WHEN** preview, log, color, language, expression, template, zones, or forward-shift tools are opened
- **THEN** the window service SHALL show the corresponding view and coordinate owner/result behavior without constructing that tool's internal controls inline

#### Scenario: Secondary window behavior is bindable
- **WHEN** a secondary tool displays state, accepts input, or invokes actions
- **THEN** the tool SHALL use ViewModel properties and commands that can be unit tested without generating its visual tree imperatively

#### Scenario: Converted windows keep lifecycle behavior
- **WHEN** a secondary window is opened, closed, and reopened
- **THEN** it SHALL preserve the documented reusable, modal, or result-returning behavior for that tool

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

### Requirement: Typed settings cover editable application preferences
The settings system SHALL persist all settings exposed by the unified settings panel through typed cross-platform stores while preserving legacy migration behavior.

#### Scenario: Existing settings still load
- **WHEN** existing `appsettings.json` files or migrated `chaptertool.json` files omit newly added settings fields
- **THEN** the settings store SHALL load successfully and use defaults matching the current application startup behavior

#### Scenario: Workflow defaults persist
- **WHEN** the user saves default save format or default XML language from the settings panel
- **THEN** those defaults SHALL be written to typed settings and applied when a new main window ViewModel loads settings

#### Scenario: Theme colors remain compatible
- **WHEN** appearance settings are saved from the settings panel
- **THEN** the six theme color slots SHALL continue using the existing theme settings store and legacy color slot order

### Requirement: External tool settings are editable and verifiable
The application SHALL allow users to configure, clear, and verify external tool paths used by current import workflows.

#### Scenario: Configured paths preserve locator precedence
- **WHEN** MKVToolNix/mkvextract, eac3to, ffprobe, or ffmpeg path settings are saved
- **THEN** the external tool locator SHALL use those configured values before environment or platform discovery according to the existing precedence rules

#### Scenario: Cleared paths restore discovery
- **WHEN** a configured external tool path is cleared in settings
- **THEN** the external tool locator SHALL fall back to environment and platform discovery rather than retaining the old override

#### Scenario: Directory values resolve executable names
- **WHEN** a configured tool setting points to a directory supported by the locator
- **THEN** validation and runtime lookup SHALL resolve the platform-appropriate executable name from that directory

#### Scenario: Tool validation returns structured status
- **WHEN** the settings panel tests an external tool setting
- **THEN** the validation result SHALL indicate found, missing, invalid path, or unsupported status without showing a Core-layer UI dialog

### Requirement: Settings panel services are injectable
Settings-related UI behavior SHALL use injectable services for settings stores, file and directory picking, localization, tool location, platform support, and shell operations.

#### Scenario: Settings ViewModel is testable
- **WHEN** tests construct the settings panel ViewModel
- **THEN** settings stores, picker behavior, tool locator, localizer, and platform capability checks SHALL be replaceable with fakes

#### Scenario: Browsing uses platform abstractions
- **WHEN** the user browses for a save directory or external tool path
- **THEN** the settings panel SHALL use Avalonia/platform file picker abstractions rather than direct WinForms or registry APIs

#### Scenario: Settings status is localizable
- **WHEN** settings validation, save, reset, or unsupported-platform feedback is displayed
- **THEN** visible messages SHALL be formatted through the active localization resources

### Requirement: Application diagnostics use standard logging middleware
The application SHALL route diagnostic logging through standard .NET logging abstractions backed by a structured logging provider instead of using a hand-written in-memory list as the primary logging mechanism.

#### Scenario: Services log through injectable logging abstractions
- **WHEN** Avalonia, Infrastructure, or platform services need to record diagnostic information
- **THEN** they SHALL use injectable logging abstractions or adapters backed by `Microsoft.Extensions.Logging`

#### Scenario: Logging backend is configured in composition
- **WHEN** the Avalonia application starts normally
- **THEN** the composition root SHALL configure the logging backend, providers, minimum levels, and lifecycle ownership in one place

#### Scenario: Core remains logging-backend independent
- **WHEN** Core services are built or tested
- **THEN** they SHALL NOT depend on Serilog, Avalonia, file-system log sinks, or another concrete logging backend

### Requirement: Application logs are severity-classified structured events
Application log entries SHALL include severity level, category, timestamp, structured message state, and optional technical detail so troubleshooting can distinguish routine workflow events from warnings and failures.

#### Scenario: Routine workflow events use information level
- **WHEN** a source is loaded, chapters are saved, a source option is selected, or frame information is updated successfully
- **THEN** the event SHALL be recorded at `Information` level with concise structured context

#### Scenario: Recoverable diagnostics use warning level
- **WHEN** an import, export, external dependency, or media lookup produces a recoverable diagnostic
- **THEN** the event SHALL be recorded at `Warning` level with the diagnostic code, message, location, and relevant technical detail

#### Scenario: Failures use error level
- **WHEN** an operation fails, an exception is observed, or an external process cannot be invoked successfully
- **THEN** the event SHALL be recorded at `Error` level with exception or failure detail sufficient for debugging

#### Scenario: Verbose implementation detail is filtered
- **WHEN** an event is only useful for developer-level tracing
- **THEN** it SHALL be recorded at `Debug` or `Trace` level and SHALL NOT appear in the default user-facing log window unless configured to do so

### Requirement: Log window is backed by a bounded logging sink
The log window SHALL display recent application log events captured from the standard logging pipeline while preserving clear, copy, localization, and refresh behavior.

#### Scenario: Log window receives captured events
- **WHEN** a log event passes the configured UI sink filter
- **THEN** the log service SHALL expose it to the log window with timestamp, severity, message key or rendered message, arguments, category, and technical detail

#### Scenario: Log window clear does not disable logging
- **WHEN** the user clears the log window
- **THEN** recent in-memory entries SHALL be removed while the logging backend continues recording subsequent events

#### Scenario: In-memory entries are bounded
- **WHEN** many log events are recorded during a session
- **THEN** the UI log sink SHALL retain only a bounded recent set to avoid unbounded memory growth

### Requirement: Diagnostic logs are persisted locally
The Avalonia application SHALL write structured diagnostic logs to a local rolling file sink suitable for troubleshooting after the application exits.

#### Scenario: File logs survive application exit
- **WHEN** the application records workflow events, warnings, or errors and then exits
- **THEN** those events SHALL be available in local log files under the application's user data or settings area

#### Scenario: File logs are bounded by retention
- **WHEN** log files roll across multiple runs
- **THEN** the logging backend SHALL enforce retention or size limits so logs do not grow without bound

#### Scenario: Sensitive transport is not introduced
- **WHEN** logs are recorded
- **THEN** the application SHALL NOT upload logs or send telemetry to remote services as part of this change

### Requirement: Avalonia localization resources are externalized
The Avalonia application SHALL store translated UI, prompt, status, and user-facing log strings in culture-specific .NET resource assets rather than hand-written C# translation dictionaries.

#### Scenario: Resources load from compiled resource assets
- **WHEN** the Avalonia localization manager resolves a supported UI culture
- **THEN** it SHALL load localized values from compiled `.resx` resources through .NET resource infrastructure
- **AND** it SHALL NOT require translated string literals to be maintained in C# dictionary initializers

#### Scenario: Resource validation uses compiled resources
- **WHEN** localization resource tests validate supported cultures
- **THEN** they SHALL inspect the compiled resource sets used by the application
- **AND** they SHALL verify key parity, placeholder parity, and valid Chinese/Japanese text without asserting over source file text

#### Scenario: Fallback behavior remains stable
- **WHEN** a supported resource set is missing a key or settings contain an unsupported language tag
- **THEN** localization SHALL use the Simplified Chinese fallback value and keep the application usable
