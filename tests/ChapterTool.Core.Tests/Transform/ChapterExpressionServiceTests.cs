using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;

namespace ChapterTool.Core.Tests.Transform;

public sealed class ChapterExpressionServiceTests
{
    [Fact]
    public void Apply_excludes_separators_from_engine_and_chapter_snapshot()
    {
        var first = new Chapter(1, TimeSpan.FromSeconds(1), "First");
        var separator = Chapter.Separator("Part 2");
        var second = new Chapter(2, TimeSpan.FromSeconds(5), "Second");
        var engine = new RecordingExpressionEngine();
        var service = new ChapterExpressionService(engine);
        var chapterSet = new ChapterSet("Test", null, ChapterImportFormat.Ogm, 24, TimeSpan.FromSeconds(10), [first, separator, second]);

        var result = service.Apply(chapterSet, true, "t");

        Assert.Equal(2, engine.Contexts.Count);
        Assert.All(engine.Contexts, context =>
        {
            Assert.Equal(2, context.Count);
            Assert.Equal(2, context.Chapters.Count);
            Assert.DoesNotContain(context.Chapters, static chapter => chapter.IsSeparator);
            Assert.Same(context.Chapter, context.Chapters[context.Index - 1]);
        });
        Assert.Same(separator, result.Info.Chapters[1]);
    }

    private sealed class RecordingExpressionEngine : IChapterExpressionEngine
    {
        public string EngineId => "recording";

        public IReadOnlyList<ChapterExpressionPreset> Presets => [];

        public List<ChapterExpressionContext> Contexts { get; } = [];

        public ChapterExpressionEvaluationResult Evaluate(string sourceText, ChapterExpressionContext context)
        {
            Contexts.Add(context);
            return new ChapterExpressionEvaluationResult(true, context.TimeSeconds, []);
        }
    }
}
