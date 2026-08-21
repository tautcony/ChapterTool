using System.Globalization;
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
        new("Fps23976", "24000 / 1001", 24000m / 1001m, true, 1),
        new("Fps24", "24000 / 1000", 24m, true, 2),
        new("Fps25", "25000 / 1000", 25m, true, 3),
        new("Fps2997", "30000 / 1001", 30000m / 1001m, true, 4),
        new("Reserved", "RESER / VED", 0m, false, 5),
        new("Fps50", "50000 / 1000", 50m, true, 6),
        new("Fps5994", "60000 / 1001", 60000m / 1001m, true, 7)
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
        return FrameRateOptions.FirstOrDefault(entry =>
            entry.IsValid && Math.Abs(entry.Value - framesPerSecond) < 0.00001m)
            ?? FrameRateOptions[0];
    }

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
        var bestDeviation = decimal.MaxValue;
        var bestAccurateCount = -1;

        foreach (var entry in FrameRateOptions.Where(static entry => entry.IsValid))
        {
            var deviation = 0m;
            var accurateCount = 0;
            foreach (var chapter in info.Chapters.Where(static chapter => !chapter.IsSeparator))
            {
                var frames = CalculateFrames(chapter, entry.Value);
                var rounded = ChapterRounding.RoundToInt64(frames);
                var delta = Math.Abs(frames - rounded);
                deviation += Math.Min(delta, tolerance);
                if (delta < tolerance)
                {
                    accurateCount++;
                }
            }

            if (deviation < bestDeviation
                || (deviation == bestDeviation && accurateCount > bestAccurateCount))
            {
                bestDeviation = deviation;
                bestAccurateCount = accurateCount;
                bestOption = entry;
            }
        }

        var averageDeviation = bestDeviation / evaluated;
        var confidence = ClassifyConfidence(averageDeviation, bestAccurateCount, evaluated, tolerance);
        return new FrameRateDetectionResult(bestOption, bestAccurateCount, evaluated, bestDeviation, confidence);
    }

    private static FrameRateConfidence ClassifyConfidence(
        decimal averageDeviation,
        int accurateCount,
        int evaluatedCount,
        decimal tolerance)
    {
        if (averageDeviation < tolerance / 4m && accurateCount == evaluatedCount)
        {
            return FrameRateConfidence.High;
        }

        if (averageDeviation < tolerance && accurateCount * 2 >= evaluatedCount)
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
        var selectedOption = decimalPlaces == 0 && option.LegacyMplsCode == 0
            ? Detect(info, tolerance)
            : option;

        if (!selectedOption.IsValid)
        {
            selectedOption = FrameRateOptions[1];
        }

        var frameDisplays = info.Chapters
            .Select(chapter => FormatFrames(chapter, selectedOption.Value, decimalPlaces, tolerance))
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

    private static FrameDisplay FormatFrames(Chapter chapter, decimal framesPerSecond, int decimalPlaces, decimal tolerance)
    {
        var frames = CalculateFrames(chapter, framesPerSecond);
        if (decimalPlaces < 0)
        {
            return new FrameDisplay(frames.ToString(CultureInfo.InvariantCulture), FrameAccuracy.Neutral);
        }

        if (decimalPlaces > 0)
        {
            return new FrameDisplay(frames.ToString($"F{Math.Clamp(decimalPlaces, 1, 6)}", CultureInfo.InvariantCulture), FrameAccuracy.Neutral);
        }

        var rounded = ChapterRounding.RoundToInt64(frames);
        var accuracy = Math.Abs(frames - rounded) < tolerance ? FrameAccuracy.Accurate : FrameAccuracy.Inexact;
        return new FrameDisplay(rounded.ToString(CultureInfo.InvariantCulture), accuracy);
    }

    private static decimal CalculateFrames(Chapter chapter, decimal framesPerSecond)
    {
        return (decimal)chapter.StartTime.TotalSeconds * framesPerSecond;
    }

    private sealed record FrameDisplay(string Text, FrameAccuracy Accuracy);
}
