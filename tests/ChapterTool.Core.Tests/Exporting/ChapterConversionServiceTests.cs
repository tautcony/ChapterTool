using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Exporting;

public sealed class ChapterConversionServiceTests
{
    private readonly ChapterConversionService service = new(new ChapterTimeFormatter());

    [Fact]
    public void Celltimes_exports_non_separator_start_frames()
    {
        var result = service.ToCelltimes(Sample(), 24m);

        Assert.True(result.Success);
        Assert.Equal($"0{Environment.NewLine}240", result.Content);
    }

    [Fact]
    public void Celltimes_uses_compatibility_rounding_and_rejects_invalid_fps()
    {
        var info = Sample() with
        {
            Chapters = [new Chapter(1, TimeSpan.FromSeconds(0.0625), "Half")]
        };

        var result = service.ToCelltimes(info, 24m);
        var invalid = service.ToCelltimes(info, 0);

        Assert.Equal("2", result.Content);
        Assert.False(invalid.Success);
        Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Code == "InvalidFrameRate");
    }

    [Fact]
    public void ChapterTextToQpfile_converts_ogm_chapter_text()
    {
        const string text = """
            CHAPTER01=00:00:00.000
            CHAPTER01NAME=Intro
            CHAPTER02=00:00:10.000
            CHAPTER02NAME=Middle
            """;

        var result = service.ChapterTextToQpfile(text, 24m);

        Assert.True(result.Success);
        Assert.Equal($"0 I{Environment.NewLine}240 I", result.Content);
    }

    [Fact]
    public void ChapterTextToQpfile_uses_timecode_mapping()
    {
        const string text = "CHAPTER01=00:00:00.050";
        const string timecodes = """
            # timecode format v2
            0
            41.708
            83.417
            """;

        var result = service.ChapterTextToQpfile(text, 24m, timecodes);

        Assert.True(result.Success);
        Assert.Equal("2 I", result.Content);
    }

    [Fact]
    public void ChapterTextToQpfile_invalid_input_returns_diagnostic()
    {
        var result = service.ChapterTextToQpfile("not chapters", 24m);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "InvalidChapterText");
    }

    private static ChapterInfo Sample() =>
        new(
            "Title",
            "source",
            0,
            "OGM",
            24,
            TimeSpan.FromSeconds(30),
            [
                new Chapter(1, TimeSpan.Zero, "Intro"),
                new Chapter(-1, Chapter.SeparatorTime, ""),
                new Chapter(2, TimeSpan.FromSeconds(10), "Middle")
            ]);
}
