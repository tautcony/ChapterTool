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
        var rate = FrameRateValue.Rational(24000, 1001);
        var info = new ChapterSet("External", null, ChapterImportFormat.Ogm, rate, TimeSpan.Zero, [chapter]);
        var (title, source, format, frameRate, duration, chapters) = info;
        Require(title == "External" && source is null && format == ChapterImportFormat.Ogm
            && frameRate == rate && duration == TimeSpan.Zero && chapters.Count == 1);

        var editing = new ChapterEditingService(formatter);
        Require(editing.EditFrame(info, index: 0, text: "24", framesPerSecond: rate).Diagnostics.Count == 0);
        var changed = ChapterFpsTransformService.ChangeFps(info, sourceFps: rate,
            targetFps: FrameRateValue.Approximate(25m));
        Require(changed.Success && changed.Info.FrameRate == FrameRateValue.Approximate(25m));

        var catalog = new FrameRateService();
        var option = catalog.FindByCode("Fps23976");
        var (code, displayName, optionRate, valid, mplsCode) = option;
        Require(code == "Fps23976" && displayName.Length > 0 && optionRate == rate && valid && mplsCode == 1);
        var frameResult = catalog.UpdateFrames(info, option, round: true, tolerance: 0.01m);
        var (_, displayedChapters, selectedOption, accuracy) = frameResult;
        Require(displayedChapters.Count == 1 && selectedOption.Rate == rate && accuracy.Count == 1);

        var context = new ChapterExpressionContext(chapter, Index: 1, Count: 1,
            TimeSeconds: chapter.StartTime.Ticks / (decimal)TimeSpan.TicksPerSecond,
            FramesPerSecond: rate.ToDecimal(), Chapters: [chapter]);
        Require(context.FramesPerSecond > 23 && context.Chapters.Count == 1);

        var serializer = new ChapterExportService(formatter);
        var unknown = info with { FrameRate = FrameRateValue.Unknown };
        Require(serializer.Serialize(unknown, new ChapterSerializationOptions(ChapterExportFormat.Txt)).Success);
        Require(serializer.Serialize(unknown, new ChapterSerializationOptions(ChapterExportFormat.Qpfile)
            { ComputationRate = FrameRateValue.Rational(24, 1) }).Success);
        Console.WriteLine("Installed canonical Core frame-rate contracts passed.");
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new Exception("Installed Core frame-rate contract failed.");
    }
}
