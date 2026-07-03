## Why

The current `FrameRateService.Detect` algorithm uses a simple per-chapter "is the frame integer within tolerance?" count. It has three concrete problems for users:

1. **Tie-breaking favors the first valid option**, so when multiple frame rates score equally (e.g. integer-second timestamps fit 24, 25, 50, and 60 fps), the result is always 23.976 fps. Users loading a 25-fps PAL DVD with sparse chapter marks see the wrong default.
2. **No precision signal** — a chapter that is 0.0001 frames off scores the same as one that is 0.014 frames off (with default tolerance 0.01). The algorithm cannot tell "this is exactly 25 fps" apart from "this is approximately 25 fps".
3. **Auto detection is hidden from the UI**. The `Auto` option exists in `FrameRateService.Options` but `MainWindow.axaml` only renders the seven valid rates. Auto detection only fires implicitly when the user toggles "round frames" with no explicit fps choice, which is a non-obvious workflow.

The detection result also doesn't surface to the user: there is no diagnostic, status text, or tooltip indicating which frame rate was detected or how confident the detector is.

## What Changes

- Replace the count-based scorer in `FrameRateService.Detect` with a precision-weighted scorer that prefers the frame rate with the smallest cumulative deviation from integer frame positions, and falls back to count when deviations are equal.
- Add a `FrameRateDetectionResult` record that exposes the chosen `FrameRateOption`, the cumulative deviation, the per-chapter accurate-count, and a `Confidence` enum (`High`, `Medium`, `Low`).
- Add `IFrameRateService.DetectDetailed(ChapterInfo, decimal tolerance)` returning `FrameRateDetectionResult`. Existing `Detect` keeps its signature and delegates to `DetectDetailed`.
- Expose the `Auto` option as the first item in the frame-rate ComboBox of `MainWindow.axaml`. When the user picks `Auto`, the ViewModel runs `DetectDetailed` and applies the detected rate, then writes a diagnostic of the form `Detected {DisplayName} (confidence: {Confidence})` to the log.
- Update `MainWindowViewModel.ApplyFrameInfo` so the detection result is also surfaced in `StatusText` after a successful auto detection.

## Capabilities

### Modified Capabilities

- `chapter-core-transform-export`: `FrameRateService` adds the precision-weighted detection algorithm and `DetectDetailed` API. The `Detect` requirement gains scenarios for tie-breaking and confidence reporting.
- `avalonia-ui-shell`: `MainWindow` frame-rate ComboBox adds an `Auto` row and surfaces detection feedback through status text and the application log.

## Impact

- Core: extends `FrameRateService` and `IFrameRateService`; adds `FrameRateDetectionResult` record and `FrameRateConfidence` enum.
- Avalonia: updates `MainWindow.axaml` (ComboBox items) and `MainWindowViewModel` (status text, command-bound auto-detection path).
- Tests: extends `FrameRateServiceTests` for tie-breaking and confidence; extends `MainWindowViewModelTests` for the Auto selection path and detection diagnostics.
