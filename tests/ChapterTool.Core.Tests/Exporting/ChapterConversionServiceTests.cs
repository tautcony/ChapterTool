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
        var result = ChapterConversionService.ToCelltimes(Sample(), 24m);

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

        var result = ChapterConversionService.ToCelltimes(info, 24m);
        var invalid = ChapterConversionService.ToCelltimes(info, 0);

        Assert.Equal("2", result.Content);
        Assert.False(invalid.Success);
        Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Code == "InvalidFrameRate");
    }

    [Fact]
    public void ToCelltimes_null_info_throws()
    {
        Assert.Throws<ArgumentNullException>(() => ChapterConversionService.ToCelltimes(null!, 24m));
    }

    [Fact]
    public void ToCelltimes_empty_chapters_succeeds_with_empty_content()
    {
        var result = ChapterConversionService.ToCelltimes(Sample() with { Chapters = [] }, 24m);

        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.Content);
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
    public void ChapterTextToQpfile_zero_fps_with_timecodes_succeeds()
    {
        const string text = "CHAPTER01=00:00:00.050";
        const string timecodes = """
            # timecode format v2
            0
            41.708
            83.417
            """;

        var result = service.ChapterTextToQpfile(text, 0m, timecodes);

        Assert.True(result.Success);
        Assert.Equal("2 I", result.Content);
    }

    [Theory]
    [InlineData("00:00:00.000", "0 I")]
    [InlineData("00:00:00.050", "2 I")]
    [InlineData("00:00:00.200", "3 I")]
    public void ChapterTextToQpfile_maps_timecode_boundaries(string chapterTime, string expected)
    {
        var text = $"CHAPTER01={chapterTime}";
        const string timecodes = """
            # timecode format v2
            0
            41.708
            83.417
            """;

        var result = service.ChapterTextToQpfile(text, 0m, timecodes);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Content);
    }

    [Theory]
    [InlineData("00:00:00.000", "0")]
    [InlineData("00:00:00.041", "1")]
    [InlineData("00:00:00.062", "1")]
    [InlineData("00:00:00.063", "2")]
    public void Celltimes_rounds_frame_boundaries(string time, string expected)
    {
        var info = Sample() with
        {
            Chapters = [new Chapter(1, ServiceTime(time), "Boundary")]
        };

        var result = ChapterConversionService.ToCelltimes(info, 24m);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Content);
        return;

        static TimeSpan ServiceTime(string value) => new ChapterTimeFormatter().ParseOrZero(value);
    }

    [Fact]
    public void ChapterTextToQpfile_invalid_input_returns_diagnostic()
    {
        var result = service.ChapterTextToQpfile("not chapters", 24m);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "InvalidChapterText");
    }

    private static ChapterSet Sample() =>
        new(
            "Title",
            "source",
            ChapterImportFormat.Ogm,
            24,
            TimeSpan.FromSeconds(30),
            [
                new Chapter(1, TimeSpan.Zero, "Intro"),
                Chapter.Separator(),
                new Chapter(2, TimeSpan.FromSeconds(10), "Middle")
            ]);
}
