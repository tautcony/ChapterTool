# P0 Fix Plan (review 2026-08-13)

Status: implemented. The full solution test run passes (965 tests, 0 failures).

This plan covers the 8 high-severity findings from `99-汇总.md`. Each fix keeps the importer contract: malformed input must produce a failed `ChapterImportResult` with diagnostics, not an unhandled exception.

## Scope

| ID | File(s) | Fix |
|----|---------|-----|
| CORE-01 | `src/ChapterTool.Core/Importing/Disc/XplChapterImporter.cs` | Validate `timeBase`/`tickBase` as finite positive values. Validate `tickBaseDivisor > 0`. Add `OverflowException` and `DivideByZeroException` to the catch filter. |
| CORE-02 | `src/ChapterTool.Core/Importing/Cue/CueTextDecoder.cs`, `CueChapterImporter.cs` | Decode strict UTF-8 first. On `DecoderFallbackException`, fall back to permissive UTF-8 decoding and attach a warning diagnostic about a possible legacy encoding. |
| CORE-03 | `src/ChapterTool.Core/Importing/Cue/CueSheetParser.cs` | Replace `int.Parse` with `int.TryParse` for TRACK and INDEX numbers. Compute CUE time from `long` ticks so large minute values cannot overflow. Report `MalformedCueSyntax` on failure. |
| CORE-04 | `src/ChapterTool.Core/Importing/Disc/MplsChapterImporter.cs`, `MplsPlaylistProjection.cs` | Guard all three `uint` subtractions (`OUTTime - INTime` twice, `MarkTimeStamp - offset` once) with compare-before-subtract. Wrapped values clamp to 0. |
| CORE-05 | `src/ChapterTool.Core/Importing/Text/WebVttChapterImporter.cs` | Parse WebVTT timestamps with a spec regex (`[hh…:]mm:ss.ttt`, optional hours, hours ≥ 24 allowed) instead of `TimeSpan.TryParse`. |
| CORE-06 (+CORE-07) | `src/ChapterTool.Core/Transform/ChapterTimeFormatter.cs` | `Format`: round to whole milliseconds through `TimeSpan` reconstruction (correct carry), use total hours (no day loss), clamp negative input to zero. `FormatCue`: carry frame 75 into seconds, use total minutes, clamp negative input. |
| INFRA-01 | `src/ChapterTool.Infrastructure/Importing/Media/FfprobeMediaChapterReader.cs`, `Importing/Matroska/MatroskaChapterImporter.cs` | Normalize the input path with `Path.GetFullPath` before it is passed to the external tool and used for `WorkingDirectory`. |
| UI-01 | `src/ChapterTool.Avalonia.UI/Localization/AppLocalizationManager.cs`, `ViewModels/XmlLanguageDisplay.cs` | The constructor no longer writes `CultureInfo.CurrentCulture`/`CurrentUICulture`. Only `SetCulture` applies the thread culture. `SetCulture` re-applies the thread culture even when the culture name is unchanged, so a polluted thread culture is corrected. `XmlLanguageDisplay` resolves culture display names against the localizer culture instead of the ambient thread culture. |

## Test plan

- `tests/ChapterTool.Core.Tests/Transform/ChapterTimeFormatterTests.cs`: carry at 59.9996 s, > 24 h hours, negative input, CUE frame carry at 994–999 ms.
- `tests/ChapterTool.Core.Tests/Importing/XplImporterTests.cs`: `tickBase="0fps"`, `timeBase="1e400fps"`, oversized time fields return failed results.
- `tests/ChapterTool.Core.Tests/Importing/CueImporterTests.cs`: GBK bytes decode with warning; oversized TRACK/minute values return `MalformedCueSyntax`.
- `tests/ChapterTool.Core.Tests/Importing/MplsImporterTests.cs`: playlist with `OUTTime < INTime` produces clamped (non-wrapped) duration and chapter times.
- `tests/ChapterTool.Core.Tests/Importing/TextImporterTests.cs`: WebVTT `MM:SS.mmm` short format and ≥ 24 h timestamps import.
- `tests/ChapterTool.Infrastructure.Tests`: importer passes an absolute path to the process runner when given a relative path.
- `tests/ChapterTool.Avalonia.Tests`: constructing `AppLocalizationManager` does not change the thread culture; `SetCulture` does.

## Out of scope

P1/P2/P3 findings stay open. They are tracked by the individual review reports.
