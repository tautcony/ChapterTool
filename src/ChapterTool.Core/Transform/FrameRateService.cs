using System.Globalization;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

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

    public IReadOnlyList<FrameRateOption> Options => FrameRateOptions;

    public FrameRateOption FindByValue(decimal framesPerSecond)
    {
        return FrameRateOptions.FirstOrDefault(option =>
            option.IsValid && Math.Abs(option.Value - framesPerSecond) < 0.00001m)
            ?? FrameRateOptions[0];
    }

    public FrameRateOption Detect(ChapterInfo info, decimal tolerance) =>
        DetectDetailed(info, tolerance).Option;

    public FrameRateDetectionResult DetectDetailed(ChapterInfo info, decimal tolerance)
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

        foreach (var option in FrameRateOptions.Where(static option => option.IsValid))
        {
            var deviation = 0m;
            var accurateCount = 0;
            foreach (var chapter in info.Chapters.Where(static chapter => !chapter.IsSeparator))
            {
                var frames = CalculateFrames(chapter, option.Value);
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
                bestOption = option;
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

    public FrameInfoResult UpdateFrames(
        ChapterInfo info,
        FrameRateOption option,
        bool round,
        decimal tolerance)
    {
        var selectedOption = round && option.LegacyMplsCode == 0
            ? Detect(info, tolerance)
            : option;

        if (!selectedOption.IsValid)
        {
            selectedOption = FrameRateOptions[1];
        }

        var chapters = info.Chapters
            .Select(chapter => chapter with
            {
                FramesInfo = FormatFrames(chapter, selectedOption.Value, round, tolerance)
            })
            .ToList();

        var updatedInfo = info with
        {
            FramesPerSecond = (double)selectedOption.Value,
            Chapters = chapters
        };

        return new FrameInfoResult(updatedInfo, chapters, selectedOption, selectedOption.Value);
    }

    private static string FormatFrames(Chapter chapter, decimal framesPerSecond, bool round, decimal tolerance)
    {
        var frames = CalculateFrames(chapter, framesPerSecond);
        if (!round)
        {
            return frames.ToString(CultureInfo.InvariantCulture);
        }

        var rounded = ChapterRounding.RoundToInt64(frames);
        var marker = Math.Abs(frames - rounded) < tolerance ? "K" : "*";
        return $"{rounded.ToString(CultureInfo.InvariantCulture)} {marker}";
    }

    private static decimal CalculateFrames(Chapter chapter, decimal framesPerSecond)
    {
        return (decimal)chapter.Time.TotalSeconds * framesPerSecond;
    }
}
