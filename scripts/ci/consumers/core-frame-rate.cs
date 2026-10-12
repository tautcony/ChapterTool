using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;

namespace ChapterTool.Ci.Consumers;

internal static class Program
{
    private static void Main()
    {
        var formatter = new ChapterTimeFormatter();
        Require(formatter.Format(formatter.ParseOrZero("00:00:12.500")) == "00:00:12.500");
        var chapter = new Chapter(1, TimeSpan.FromTicks(9007199254740993), "External");
        var canonical = new ChapterSet("Canonical", null, ChapterImportFormat.Ogm,
            FrameRateValue.Approximate(24m), TimeSpan.Zero, [chapter]);
        Require(canonical.Title == "Canonical" && canonical.SourceName is null && canonical.ImportFormat == ChapterImportFormat.Ogm
            && canonical.FrameRate == FrameRateValue.Approximate(24m) && canonical.Duration == TimeSpan.Zero && canonical.Chapters.Count == 1);
        var rational = canonical with { FrameRate = FrameRateValue.Rational(24000, 1001) };
        Require(rational.FrameRate.RationalRate == new ChapterFrameRate(24000, 1001));
        var assigned = rational with { FrameRate = FrameRateValue.Approximate(25m) };
        Require(assigned.FrameRate == FrameRateValue.Approximate(25m) && assigned.FrameRate.RationalRate is null);
        var editing = new ChapterEditingService(formatter);
        Require(editing.EditFrame(canonical, index: 0, text: "24", framesPerSecond: FrameRateValue.Approximate(24m)).Diagnostics.Count == 0);
        Require(ChapterFpsTransformService.ChangeFps(canonical, sourceFps: FrameRateValue.Approximate(24m),
            targetFps: FrameRateValue.Approximate(25m)).Success);
        var catalog = new FrameRateService();
        var option = new FrameRateOption("external", "External", FrameRateValue.Approximate(24m), true, -1);
        Require(option.Code == "external" && option.Rate == FrameRateValue.Approximate(24m) && option.IsValid);
        var frameResult = catalog.UpdateFrames(rational, catalog.FindByCode("Auto"), true, 0.01m);
        Require(frameResult.SelectedOption.Rate == rational.FrameRate && frameResult.Rate == rational.FrameRate);
        var updated = new FrameInfoResult(canonical, canonical.Chapters, option, [FrameAccuracy.Neutral]);
        var changed = updated with { SelectedOption = option with { Rate = FrameRateValue.Approximate(25m) } };
        Require(changed.Rate == FrameRateValue.Approximate(25m));
        var engine = new ExternalEngine();
        var context = new ChapterExpressionContext(Chapter: chapter, Index: 1, Count: 1,
            TimeSeconds: chapter.StartTime.Ticks / (decimal)TimeSpan.TicksPerSecond, FramesPerSecond: 24m, Chapters: [chapter]);
        var (_, _, _, seconds, contextFps, _) = context with { FramesPerSecond = 25m };
        Require(seconds > 0 && contextFps == 25);
        var expression = new ChapterExpressionService(engine).Apply(rational, true, "identity");
        Require(expression.Diagnostics.Count == 0 && expression.Info.Chapters[0].StartTime.Ticks == chapter.StartTime.Ticks);
        Require(engine.LastFps == rational.FrameRate.ToDecimal());
        var serializer = new ChapterExportService(formatter);
        var unknown = canonical with { FrameRate = FrameRateValue.Unknown };
        Require(serializer.Serialize(unknown, new ChapterSerializationOptions(ChapterExportFormat.Txt)).Success);
        Require(serializer.Serialize(unknown, new ChapterSerializationOptions(ChapterExportFormat.Qpfile)
            { ComputationRate = FrameRateValue.Rational(24, 1) }).Success);
        Console.WriteLine("Installed canonical Core contracts passed.");
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new Exception("Installed Core frame-rate contract failed.");
    }
}

internal sealed class ExternalEngine : IChapterExpressionEngine
{
    public string EngineId => "external";
    public IReadOnlyList<ChapterExpressionPreset> Presets => [];
    public decimal LastFps { get; private set; }

    public ChapterExpressionEvaluationResult Evaluate(string sourceText, ChapterExpressionContext context)
    {
        LastFps = context.FramesPerSecond;
        return new ChapterExpressionEvaluationResult(true, context.TimeSeconds, []);
    }
}
