using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;

var formatter = new ChapterTimeFormatter();
Require(formatter.Format(formatter.ParseOrZero("00:00:12.500")) == "00:00:12.500");
var chapter = new Chapter(1, TimeSpan.FromTicks(9007199254740993), "External");
var legacy = new ChapterSet(Title: "Legacy", SourceName: null, ImportFormat: ChapterImportFormat.Ogm,
    FramesPerSecond: 24, Duration: TimeSpan.Zero, Chapters: [chapter]);
var (title, source, format, fps, duration, chapters, exactRate) = legacy;
Require(title == "Legacy" && source is null && format == ChapterImportFormat.Ogm && fps == 24
    && duration == TimeSpan.Zero && chapters.Count == 1 && exactRate is null);
var rational = legacy with { ExactFrameRate = new ChapterFrameRate(48000, 2002) };
Require(rational.FrameRate == FrameRateValue.Rational(24000, 1001));
var assigned = rational with { FramesPerSecond = 25 };
Require(assigned.FrameRate == FrameRateValue.Approximate(25m) && assigned.ExactFrameRate is null);
var editing = new ChapterEditingService(formatter);
Require(editing.EditFrame(legacy, index: 0, text: "24", framesPerSecond: 24m).Diagnostics.Count == 0);
Require(ChapterFpsTransformService.ChangeFps(legacy, sourceFps: 24m, targetFps: 25m).Success);
var catalog = new FrameRateService();
var option = new FrameRateOption(Code: "external", DisplayName: "External", Value: 24m, IsValid: true, LegacyMplsCode: -1);
var (_, _, projectedFps, valid, code, projectedExact) = option;
Require(projectedFps == 24 && valid && code == -1 && projectedExact is null);
var frameResult = catalog.UpdateFrames(rational, catalog.FindByCode("Auto"), true, 0.01m);
var (_, _, selected, resultFps, _) = frameResult;
Require(selected.Rate == rational.FrameRate && resultFps > 23);
var oldResult = new FrameInfoResult(Info: legacy, Chapters: legacy.Chapters, SelectedOption: option,
    FramesPerSecond: 24m, Accuracy: [FrameAccuracy.Neutral]);
Require((oldResult with { FramesPerSecond = 25m }).FramesPerSecond == 25m);
var engine = new ExternalEngine();
var context = new ChapterExpressionContext(Chapter: chapter, Index: 1, Count: 1,
    TimeSeconds: chapter.StartTime.Ticks / (decimal)TimeSpan.TicksPerSecond, FramesPerSecond: 24m, Chapters: [chapter]);
var (_, _, _, seconds, contextFps, _) = context with { FramesPerSecond = 25m };
Require(seconds > 0 && contextFps == 25);
var expression = new ChapterExpressionService(engine).Apply(rational, true, "identity");
Require(expression.Diagnostics.Count == 0 && expression.Info.Chapters[0].StartTime.Ticks == chapter.StartTime.Ticks);
Require(engine.LastFps == rational.FrameRate.ToDecimal());
var serializer = new ChapterExportService(formatter);
var unknown = legacy with { FrameRate = FrameRateValue.Unknown };
Require(serializer.Serialize(unknown, new ChapterSerializationOptions(ChapterExportFormat.Txt)).Success);
Require(serializer.Serialize(unknown, new ChapterSerializationOptions(ChapterExportFormat.Qpfile)
    { ComputationRate = FrameRateValue.Rational(24, 1) }).Success);
Console.WriteLine("Installed canonical and retained Core contracts passed.");

static void Require(bool condition)
{
    if (!condition) throw new Exception("Installed Core frame-rate contract failed.");
}

sealed class ExternalEngine : IChapterExpressionEngine
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
