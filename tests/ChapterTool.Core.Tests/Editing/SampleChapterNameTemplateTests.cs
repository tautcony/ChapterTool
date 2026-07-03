using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Editing;

public sealed class SampleChapterNameTemplateTests
{
    [Fact]
    public void ChapterNameSampleAppliesUtf8TemplateInOrder()
    {
        var service = new ChapterEditingService(new ChapterTimeFormatter());
        const string template = """
                                Avant
                                OP
                                A Part
                                B Part
                                ED
                                C Part
                                """;

        var result = service.ApplyTemplate(Sample(), template);

        Assert.Equal(
            ["Avant", "OP", "A Part", "B Part", "ED", "C Part"],
            result.ChapterInfo.Chapters.Select(static chapter => chapter.Name));
    }

    private static ChapterInfo Sample() =>
        new(
            "Title",
            "source",
            0,
            "OGM",
            24,
            TimeSpan.FromMinutes(6),
            Enumerable.Range(1, 6)
                .Select(index => new Chapter(index, TimeSpan.FromMinutes(index - 1), $"Chapter {index:00}"))
                .ToArray());
}
