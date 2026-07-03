## ADDED Requirements

### Requirement: Auto frame-rate detection in the UI
The Avalonia main window SHALL expose an `Auto` entry as the first item in the frame-rate selector and SHALL surface detection feedback when Auto is the active selection.

#### Scenario: Auto entry appears in the frame-rate selector
- **WHEN** the main window renders
- **THEN** the frame-rate ComboBox SHALL include `Auto` as its first item, followed by the documented seven valid frame-rate rows in the same order as before

#### Scenario: Auto detection updates status text
- **WHEN** the user picks `Auto` and the chapter set has at least one non-separator chapter
- **THEN** the ViewModel SHALL run frame-rate detection and SHALL update `StatusText` to a string of the form `Detected {DisplayName} (confidence: {Confidence})` that reflects the chosen frame rate and the confidence band

#### Scenario: Auto remains selected after detection
- **WHEN** Auto detection completes
- **THEN** `SelectedFrameRateIndex` SHALL stay on the Auto row so subsequent edits or refreshes re-run the detector

#### Scenario: Manual frame-rate choice does not emit detection status
- **WHEN** the user picks any non-Auto frame-rate row
- **THEN** the ViewModel SHALL NOT overwrite `StatusText` with a `Detected` message, and SHALL apply the manually selected frame rate directly
