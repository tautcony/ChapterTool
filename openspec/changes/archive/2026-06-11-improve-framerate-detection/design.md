## Algorithm Decision: Precision-Weighted Score

### Status quo

`FrameRateService.Detect` today computes:

```
score(option) = count(chapter where |frames - round(frames)| < tolerance)
```

For chapters at `t = 0s, 1s, 2s, 3s` and tolerance `0.01`, every integer-multiple frame rate (24, 25, 50, 60) scores 4. The first valid option (Fps23976) wins by iteration order, not by fit.

### Decision

Use a two-key score:

```
primary  = -sum(min(|frames - round(frames)|, tolerance))   # higher is better
secondary = count(chapter where |frames - round(frames)| < tolerance)
```

Sort by `(primary, secondary)` descending. The clamped deviation prevents a single wildly-off chapter from dominating the result, and the secondary key keeps the existing semantics for cases where deviations all clamp to `tolerance` (i.e. genuine ties).

This was preferred over:

- **Pure cumulative deviation** (no clamp): one bad chapter (e.g. a manually inserted off-grid mark) would push the detector toward a wrong rate that happens to fit that single mark.
- **Drop-frame timecode awareness**: out of scope; existing options (23.976, 29.97, 59.94) already cover the common NTSC drop frame rates and the heuristic does not need to model SMPTE drop semantics.
- **Multi-pass with clustering**: complex, no evidence the user base has chapter sets where this matters.

### Confidence Mapping

```
total_clamped_deviation = sum(min(|frames - round(frames)|, tolerance))
average_deviation       = total_clamped_deviation / valid_chapter_count

High    when average_deviation < tolerance / 4 AND accurate_count == valid_chapter_count
Medium  when average_deviation < tolerance     AND accurate_count >= valid_chapter_count / 2
Low     otherwise
```

Empty chapter sets (no non-separator chapters) report `Low` and the default `Fps23976`, matching the current fallback.

## API Shape

```csharp
public sealed record FrameRateDetectionResult(
    FrameRateOption Option,
    int AccurateChapterCount,
    int EvaluatedChapterCount,
    decimal CumulativeDeviation,
    FrameRateConfidence Confidence);

public enum FrameRateConfidence
{
    Low,
    Medium,
    High,
}

// IFrameRateService
FrameRateDetectionResult DetectDetailed(ChapterInfo info, decimal tolerance);
FrameRateOption Detect(ChapterInfo info, decimal tolerance); // delegates to DetectDetailed
```

`Detect` stays for backwards compatibility and continues to return only `FrameRateOption`. The pre-existing `UpdateFrames` Auto path keeps using `Detect`.

## UI Decision: Add Auto to ComboBox

### Status quo

`MainWindow.axaml` lists seven explicit rates. The ViewModel's `ComboIndexFor` maps `LegacyMplsCode` to a 0-based index using `LegacyMplsCode - 1`, which means `Auto` (LegacyMplsCode 0) maps to index `-1`. The XAML never shows Auto because it's not in the items list.

### Decision

Insert `Auto` as the first ComboBox item (index 0) and shift the rest by one:

```
Index 0: Auto
Index 1: 24000 / 1001
Index 2: 24000 / 1000
…
Index 7: 60000 / 1001
```

Update the index↔option mapping:

```
ComboIndexFor(option) =
    option.LegacyMplsCode == 0 ? 0 :        // Auto
    option.IsValid              ? option.LegacyMplsCode :   // shift +1
                                  -1;       // RESERVED unmapped

FrameRateOptionForComboIndex(index) =
    index == 0 ? FrameRateOptions[0] :          // Auto
    index >= 1 ? FrameRateOptions[index] :      // 1:1 mapping into Options[]
                 null;
```

This avoids changing `LegacyMplsCode` on the option records (which is observable behavior used elsewhere).

### When the user picks Auto

`MainWindowViewModel.ApplyFrameInfo` already detects when the selected option is Auto (`LegacyMplsCode == 0`) and runs detection inside `UpdateFrames`. We extend it to:

1. Call `DetectDetailed` directly when Auto is selected.
2. Update `currentInfo` with the detected fps via `UpdateFrames`.
3. Emit a status-text update: `Detected {DisplayName} ({Confidence})`.
4. Log the same string to the application log.
5. Keep `selectedFrameRateOption = Auto` so the ComboBox stays on Auto (the detected rate is shown in StatusText, not by switching the selector).

This keeps Auto sticky — re-running the calculation after edits will re-detect, which matches user expectations for an "auto" mode.

### Backwards compatibility

The legacy MPLS code (1–7) is unchanged on `FrameRateOption` records, so any persistence using LegacyMplsCode keeps working. Only the ComboBox index mapping changes, and that mapping is internal to `MainWindowViewModel`.

`SelectedFrameRateIndex` defaults to `0` in the ViewModel today, which used to mean `Fps23976`. After this change, `0` means `Auto`. To preserve the old behavior on first run we keep the ViewModel's initial assignment (`selectedFrameRateOption = frameRateService.Options[0]`) — `Options[0]` is already Auto, so the default is `Auto` from day one. Existing tests assert `SelectedFrameRateIndex == 1` after a load with valid fps, which corresponds to the new Auto-shifted Fps23976 row, so most tests are stable.

## Risk: ApplyFrameInfo Re-entrance

`ApplyFrameInfo` already handles `LegacyMplsCode == 0` by calling `FindByValue`. After this change we also detect when the user *explicitly* picks Auto vs. when Auto is the implicit default after a fresh load. We disambiguate by checking the source of the selection:

- ViewModel internal default after load → `FindByValue` (unchanged).
- ComboBox change to Auto from the user → `DetectDetailed`.

We thread this through the `SetFrameOptions(int frameRateIndex, bool roundFrames)` entry point that the View calls when the ComboBox changes. This method already exists; we extend it to flag user intent.
