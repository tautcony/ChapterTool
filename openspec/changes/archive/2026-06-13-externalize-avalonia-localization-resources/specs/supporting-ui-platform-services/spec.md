## ADDED Requirements

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
