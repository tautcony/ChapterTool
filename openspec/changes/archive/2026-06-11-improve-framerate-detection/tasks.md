## 1. Core Detection Algorithm

- [x] 1.1 Add `FrameRateConfidence` enum (`Low`, `Medium`, `High`) in `src/ChapterTool.Core/Transform`.
- [x] 1.2 Add `FrameRateDetectionResult` record (`Option`, `AccurateChapterCount`, `EvaluatedChapterCount`, `CumulativeDeviation`, `Confidence`).
- [x] 1.3 Extend `IFrameRateService` with `DetectDetailed(ChapterInfo info, decimal tolerance)`.
- [x] 1.4 Implement `DetectDetailed` in `FrameRateService` using the precision-weighted score from `design.md`. Skip separator chapters in evaluation (matches existing `IsAccurate`).
- [x] 1.5 Refactor `Detect` to call `DetectDetailed(...).Option`.
- [x] 1.6 Compute `Confidence` per the bands in `design.md`. Empty / no-valid-chapter input returns `Confidence.Low` and the default `Fps23976` option.

## 2. Core Tests

- [x] 2.1 Add `FrameRateServiceTests.DetectDetailed_returns_lowest_deviation_option_when_count_ties` — chapters at integer seconds 0,1,2,3 with tolerance 0.01: result must be Fps24 (smallest cumulative deviation among integer-multiple rates), not Fps23976.
- [x] 2.2 Add `FrameRateServiceTests.DetectDetailed_assigns_high_confidence_for_exact_matches` — 25 fps chapters at 0, 0.04, 0.08 yield `Confidence.High`.
- [x] 2.3 Add `FrameRateServiceTests.DetectDetailed_assigns_medium_confidence_when_some_deviation` — chapters with cumulative deviation between `tolerance/4` and `tolerance` yield `Confidence.Medium`.
- [x] 2.4 Add `FrameRateServiceTests.DetectDetailed_assigns_low_confidence_when_few_chapters_align` — fewer than half the chapters within tolerance yield `Confidence.Low`.
- [x] 2.5 Add `FrameRateServiceTests.DetectDetailed_returns_default_with_low_confidence_for_empty_chapters` — info with zero non-separator chapters returns Fps23976 + Low.
- [x] 2.6 Update `Detect_returns_first_highest_scoring_valid_option_on_tie` to assert the new tie-break behavior (lowest cumulative deviation), not iteration order.

## 3. ViewModel Auto Selection

- [x] 3.1 In `MainWindowViewModel`, change `ComboIndexFor` and `FrameRateOptionForComboIndex` to map index 0 → Auto, index 1 → Fps23976, …, index 7 → Fps5994 (per `design.md`).
- [x] 3.2 Verify `selectedFrameRateOption = frameRateService.Options[0]` on construction still produces `SelectedFrameRateIndex == 0` (Auto) — this is the default before any load.
- [x] 3.3 Add detection-result tracking so the ViewModel emits `Detected ...` status text only on Auto selection.
- [x] 3.4 In `ApplyFrameInfo`, when `selectedFrameRateOption.LegacyMplsCode == 0` (Auto), call `frameRateService.DetectDetailed(currentInfo, 0.01m)` and use the returned `Option` for `UpdateFrames`. Set `StatusText = "Detected {DisplayName} (confidence: {Confidence})"`. Append the same to the application log.
- [x] 3.5 When `LegacyMplsCode != 0`, do NOT update detection state and do NOT alter `StatusText`.
- [x] 3.6 Keep `selectedFrameRateOption` set to Auto after detection (Auto is "sticky" so re-applying the algorithm after edits reruns detection).

## 4. View Updates

- [x] 4.1 Edit `src/ChapterTool.Avalonia/Views/MainWindow.axaml` `FrameRateBox` ComboBox: insert `<ComboBoxItem Content="Auto" />` as the first child.
- [x] 4.2 Verify `OnFrameOptionsChanged` in `MainWindow.axaml.cs` already routes the selection through `ApplyFrameOptionsAndRefreshAsync` → `SetFrameOptions(...)`; no code change expected, but confirm the new index 0 maps to Auto.

## 5. Avalonia Tests

- [x] 5.1 Add `MainWindowViewModelTests.AutoFrameRateRunsDetectionAndUpdatesStatusText` — load fixture with chapters at 25 fps, set frameRateIndex=0 (Auto), refresh, assert detected option is Fps25, StatusText contains `Detected 25000 / 1000`.
- [x] 5.2 Add `MainWindowViewModelTests.ManualFrameRateChoiceDoesNotEmitDetectedStatusText` — set frameRateIndex=3 (Fps25 directly), assert StatusText is the standard `Updated`/load-status form, not a `Detected` message.
- [x] 5.3 Update `LoadUpdatesStateAndClipSelection` (currently asserts `SelectedFrameRateIndex == 1`) to assert the new shifted index of `2` (24fps maps to LegacyMplsCode 2 → ComboIndex 2 in the new mapping).

## 6. Verification

- [x] 6.1 Run `dotnet test tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj --no-restore`.
- [x] 6.2 Run `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore`.
- [x] 6.3 Manual check: launch app, load a 25-fps DVD chapter set, observe Auto selected by default, observe StatusText reads `Detected 25000 / 1000 (...)`.
- [x] 6.4 Run `openspec validate improve-framerate-detection --strict`.
