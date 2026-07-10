using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using LuaExpressionScriptService = ChapterTool.Core.Transform.Expressions.Lua.LuaExpressionScriptService;

namespace ChapterTool.Core.Tests.Exporting;

public sealed class ChapterOutputProjectionServiceTests
{
    private readonly ChapterOutputProjectionService service = new(new LuaExpressionScriptService());

    [Fact]
    public void Projection_applies_lua_before_numbering_and_names_without_mutating_source()
    {
        var source = Sample();
        var originalFirst = source.Chapters[0];

        var result = service.Project(
            source,
            new ChapterExportOptions(
                ChapterExportFormat.Txt,
                AutoGenerateNames: true,
                OrderShift: 2,
                ApplyExpression: true,
                Expression: "t + index"));

        Assert.Equal(TimeSpan.FromSeconds(10), source.Chapters[0].StartTime);
        Assert.Same(originalFirst, source.Chapters[0]);
        Assert.Empty(result.Diagnostics);
        Assert.Equal([3, 4, 5], result.OutputChapters.Select(static chapter => chapter.DisplayNumber));
        Assert.Equal(["Chapter 01", "Chapter 02", "Chapter 03"], result.OutputChapters.Select(static chapter => chapter.Name));
        Assert.Equal([11, 22, 33], result.OutputChapters.Select(static chapter => (int)chapter.StartTime.TotalSeconds));
        Assert.Equal("264", result.OutputChapters[0].FramesInfo);
        Assert.True(result.OutputChapters[0].FrameAccuracy is FrameAccuracy.Accurate);
    }

    [Fact]
    public void Projection_preserves_separators_and_excludes_them_from_output_chapters()
    {
        var separator = Chapter.Separator("---");
        var info = Sample() with
        {
            Chapters = [Sample().Chapters[0], separator, Sample().Chapters[1]]
        };

        var result = service.Project(
            info,
            new ChapterExportOptions(ChapterExportFormat.Txt, ApplyExpression: true, Expression: "index"));

        Assert.Equal(3, result.Info.Chapters.Count);
        Assert.True(result.Info.Chapters[1].IsSeparator);
        Assert.Equal(0, result.Info.Chapters[1].DisplayNumber);
        Assert.Equal(2, result.OutputChapters.Count);
        Assert.Equal([1, 2], result.OutputChapters.Select(static chapter => (int)chapter.StartTime.TotalSeconds));
    }

    [Fact]
    public void Projection_keeps_original_time_for_failed_lua_and_continues_numbering_and_naming()
    {
        var result = service.Project(
            Sample(),
            new ChapterExportOptions(
                ChapterExportFormat.Txt,
                AutoGenerateNames: true,
                ApplyExpression: true,
                Expression: "return bad()"));

        Assert.Equal(3, result.Diagnostics.Count);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal(ChapterDiagnosticCode.InvalidExpressionLuaRuntime, diagnostic.Code));
        Assert.Equal([10, 20, 30], result.OutputChapters.Select(static chapter => (int)chapter.StartTime.TotalSeconds));
        Assert.Equal(["Chapter 01", "Chapter 02", "Chapter 03"], result.OutputChapters.Select(static chapter => chapter.Name));
    }

    [Fact]
    public void Projection_normalizes_lua_time_and_reports_diagnostic()
    {
        var result = service.Project(
            Sample(),
            new ChapterExportOptions(ChapterExportFormat.Txt, ApplyExpression: true, Expression: "-1"));

        Assert.Equal(3, result.Diagnostics.Count);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal(ChapterDiagnosticCode.InvalidExpressionTime, diagnostic.Code));
        Assert.All(result.OutputChapters, chapter => Assert.Equal(TimeSpan.Zero, chapter.StartTime));
    }

    private static ChapterSet Sample() =>
        new(
            "Movie",
            "movie.mkv",
            ChapterImportFormat.Ogm,
            24,
            TimeSpan.FromMinutes(1),
            [
                new Chapter(1, TimeSpan.FromSeconds(10), "Intro"),
                new Chapter(2, TimeSpan.FromSeconds(20), "Middle"),
                new Chapter(3, TimeSpan.FromSeconds(30), "End")
            ]);
}
