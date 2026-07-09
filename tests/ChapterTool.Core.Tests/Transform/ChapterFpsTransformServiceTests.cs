using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Transform;

public sealed class ChapterFpsTransformServiceTests
{
    private readonly ChapterFpsTransformService service = new();

    [Fact]
    public void ChangeFps_preserves_chapter_frame_numbers()
    {
        var info = Sample();

        var result = ChapterFpsTransformService.ChangeFps(info, 24m, 48m);

        Assert.True(result.Success);
        Assert.Equal(TimeSpan.FromSeconds(5), result.Info.Chapters[1].StartTime);
        Assert.Equal(48, result.Info.FramesPerSecond);
    }

    [Fact]
    public void ChangeFps_preserves_frame_span_when_end_exists()
    {
        var info = Sample() with
        {
            Chapters =
            [
                new Chapter(1, TimeSpan.FromSeconds(10), "A", EndTime: TimeSpan.FromSeconds(12))
            ]
        };

        var result = ChapterFpsTransformService.ChangeFps(info, 24m, 48m);

        Assert.True(result.Success);
        Assert.Equal(TimeSpan.FromSeconds(5), result.Info.Chapters[0].StartTime);
        Assert.Equal(TimeSpan.FromSeconds(6), result.Info.Chapters[0].EndTime);
    }

    [Fact]
    public void ChangeFps_invalid_fps_returns_diagnostic_and_preserves_input()
    {
        var info = Sample();

        var result = ChapterFpsTransformService.ChangeFps(info, 0, 24m);

        Assert.False(result.Success);
        Assert.Same(info, result.Info);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "InvalidFrameRate");
    }

    private static ChapterSet Sample() =>
        new(
            "Title",
            "source",
            ChapterImportFormat.Ogm,
            24,
            TimeSpan.FromSeconds(20),
            [
                new Chapter(1, TimeSpan.Zero, "Intro"),
                new Chapter(2, TimeSpan.FromSeconds(10), "Middle")
            ]);
}
