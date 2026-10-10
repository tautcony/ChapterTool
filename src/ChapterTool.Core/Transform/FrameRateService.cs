using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>
/// Provides frame rate lookup, detection, and frame metadata operations.
/// </summary>
public sealed class FrameRateService : IFrameRateService
{
    private static readonly FrameRateOption[] FrameRateOptions =
    [
        new("Auto", "Auto", 0m, false, 0),
        new("Fps23976", "24000 / 1001", 24000m / 1001m, true, 1, new ChapterFrameRate(24000, 1001)),
        new("Fps24", "24000 / 1000", 24m, true, 2, new ChapterFrameRate(24, 1)),
        new("Fps25", "25000 / 1000", 25m, true, 3, new ChapterFrameRate(25, 1)),
        new("Fps2997", "30000 / 1001", 30000m / 1001m, true, 4, new ChapterFrameRate(30000, 1001)),
        new("Reserved", "RESER / VED", 0m, false, 5),
        new("Fps50", "50000 / 1000", 50m, true, 6, new ChapterFrameRate(50, 1)),
        new("Fps5994", "60000 / 1001", 60000m / 1001m, true, 7, new ChapterFrameRate(60000, 1001))
    ];

    /// <summary>
    /// Gets the available options.
    /// </summary>
    public IReadOnlyList<FrameRateOption> Options => FrameRateOptions;

    /// <summary>
    /// Executes the FindByValue operation.
    /// </summary>
    /// <param name="framesPerSecond">The frame rate in frames per second.</param>
    /// <returns>The operation result.</returns>
    public FrameRateOption FindByValue(decimal framesPerSecond)
    {
        var match = FrameRateOptions.FirstOrDefault(entry =>
            entry.IsValid && Math.Abs(entry.Value - framesPerSecond) < 0.00001m)
            ?? FrameRateOptions[0];

        // A tolerant numeric match does not prove which rational rate the caller intended.
        return match with { ExactRate = null };
    }

    /// <summary>Finds a supported frame rate by its stable code and retains its exact rational rate.</summary>
    public FrameRateOption FindByCode(string code) =>
        FrameRateOptions.FirstOrDefault(entry => string.Equals(entry.Code, code, StringComparison.Ordinal))
        ?? FrameRateOptions[0];

    /// <summary>
    /// Executes the Detect operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="tolerance">The maximum frame deviation treated as accurate.</param>
    /// <returns>The operation result.</returns>
    public FrameRateOption Detect(ChapterSet info, decimal tolerance) =>
        DetectDetailed(info, tolerance).Option;

    /// <summary>
    /// Executes the DetectDetailed operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="tolerance">The maximum frame deviation treated as accurate.</param>
    /// <returns>The operation result.</returns>
    public FrameRateDetectionResult DetectDetailed(ChapterSet info, decimal tolerance)
    {
        var defaultOption = FrameRateOptions[1];
        var evaluated = info.Chapters.Count(static chapter => !chapter.IsSeparator);
        if (evaluated == 0)
        {
            return new FrameRateDetectionResult(defaultOption, 0, 0, 0m, FrameRateConfidence.Low);
        }

        var bestOption = defaultOption;
        var bestDeviation = ExactFrameMath.FromDecimal(decimal.MaxValue);
        var bestAccurateCount = -1;

        foreach (var entry in FrameRateOptions.Where(static entry => entry.IsValid))
        {
            var deviation = ExactFrameMath.Reduce(0, 1);
            var accurateCount = 0;
            foreach (var chapter in info.Chapters.Where(static chapter => !chapter.IsSeparator))
            {
                var frames = CalculateFrames(chapter, entry);
                var rounded = ExactFrameMath.Reduce((System.Numerics.BigInteger)frames.RoundAwayFromZero(), 1);
                var delta = (frames - rounded).Abs();
                deviation += ExactFrameMath.Min(delta, ExactFrameMath.FromDecimal(tolerance));
                if (delta.IsWithin(tolerance))
                {
                    accurateCount++;
                }
            }

            var comparison = deviation.CompareTo(bestDeviation);
            if (comparison < 0
                || (comparison == 0 && accurateCount > bestAccurateCount))
            {
                bestDeviation = deviation;
                bestAccurateCount = accurateCount;
                bestOption = entry;
            }
        }

        var averageDeviation = bestDeviation / evaluated;
        var confidence = ClassifyConfidence(averageDeviation, bestAccurateCount, evaluated, tolerance);
        return new FrameRateDetectionResult(bestOption, bestAccurateCount, evaluated, bestDeviation.ToDecimal(), confidence);
    }

    private static FrameRateConfidence ClassifyConfidence(
        ExactFrameMath averageDeviation,
        int accurateCount,
        int evaluatedCount,
        decimal tolerance)
    {
        if (averageDeviation.IsWithin(tolerance / 4m) && accurateCount == evaluatedCount)
        {
            return FrameRateConfidence.High;
        }

        if (averageDeviation.IsWithin(tolerance) && accurateCount * 2 >= evaluatedCount)
        {
            return FrameRateConfidence.Medium;
        }

        return FrameRateConfidence.Low;
    }

    /// <summary>
    /// Executes the UpdateFrames operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="option">The requested frame rate option.</param>
    /// <param name="round">Whether frame values should be rounded for display.</param>
    /// <param name="tolerance">The maximum frame deviation treated as accurate.</param>
    /// <returns>The operation result.</returns>
    public FrameInfoResult UpdateFrames(
        ChapterSet info,
        FrameRateOption option,
        bool round,
        decimal tolerance) => UpdateFrames(info, option, round ? 0 : -1, tolerance);

    /// <summary>Calculates frame numbers with a fixed decimal-place format.</summary>
    /// <param name="info">The chapter data to inspect.</param>
    /// <param name="option">The selected frame rate option.</param>
    /// <param name="decimalPlaces">The number of decimal places. Zero rounds to integers.</param>
    /// <param name="tolerance">The acceptable frame deviation tolerance.</param>
    public FrameInfoResult UpdateFrames(
        ChapterSet info,
        FrameRateOption option,
        int decimalPlaces,
        decimal tolerance)
    {
        var selectedOption = option.LegacyMplsCode == 0
            ? Detect(info, tolerance)
            : option;

        if (!selectedOption.IsValid)
        {
            selectedOption = FrameRateOptions[1];
        }

        var frameDisplays = info.Chapters
            .Select(chapter => FormatFrames(chapter, selectedOption, decimalPlaces, tolerance))
            .ToArray();
        var chapters = info.Chapters
            .Select((chapter, index) => chapter with
            {
                FramesInfo = frameDisplays[index].Text,
                FrameAccuracy = frameDisplays[index].Accuracy
            })
            .ToList();

        var updatedInfo = info with
        {
            FramesPerSecond = (double)selectedOption.Value,
            Chapters = chapters
        };

        return new FrameInfoResult(updatedInfo, chapters, selectedOption, selectedOption.Value,
            [.. frameDisplays.Select(static display => display.Accuracy)]);
    }

    private static FrameDisplay FormatFrames(Chapter chapter, FrameRateOption option, int decimalPlaces, decimal tolerance)
    {
        var frames = CalculateFrames(chapter, option);
        if (decimalPlaces < 0)
        {
            var raw = frames.ToDecimal().ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
            return new FrameDisplay(raw.Contains('.', StringComparison.Ordinal) ? raw : raw + ".0", FrameAccuracy.Neutral);
        }

        if (decimalPlaces > 0)
        {
            return new FrameDisplay(frames.FormatFixed(Math.Clamp(decimalPlaces, 1, 6)), FrameAccuracy.Neutral);
        }

        var rounded = frames.RoundAwayFromZero();
        var accuracy = (frames - ExactFrameMath.Reduce(rounded, 1)).Abs().IsWithin(tolerance)
            ? FrameAccuracy.Accurate : FrameAccuracy.Inexact;
        return new FrameDisplay(rounded.ToString(System.Globalization.CultureInfo.InvariantCulture), accuracy);
    }

    private static ExactFrameMath CalculateFrames(Chapter chapter, FrameRateOption option) =>
        option.ExactRate is { } rate
            ? ExactFrameMath.FromTicks(chapter.StartTime.Ticks, rate)
            : ExactFrameMath.FromTicks(chapter.StartTime.Ticks, option.Value);

    private sealed record FrameDisplay(string Text, FrameAccuracy Accuracy);
}
