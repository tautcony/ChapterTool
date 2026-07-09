using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Editing;

public sealed class ChapterEditingServiceTests
{
    private readonly ChapterEditingService service = new(new ChapterTimeFormatter());

    [Fact]
    public void EditTime_resets_values_over_one_day()
    {
        var result = service.EditTime(Sample(), 1, "25:00:00.000");

        Assert.Equal(TimeSpan.Zero, result.ChapterSet.Chapters[1].StartTime);
    }

    [Fact]
    public void EditFrame_uses_current_fps()
    {
        var result = service.EditFrame(Sample(), 1, "240 frames", 24);

        Assert.Equal(TimeSpan.FromSeconds(10), result.ChapterSet.Chapters[1].StartTime);
        Assert.Equal("240", result.ChapterSet.Chapters[1].FramesInfo);
        Assert.Equal(FrameAccuracy.Accurate, result.ChapterSet.Chapters[1].FrameAccuracy);
    }

    [Theory]
    [InlineData("999999999999999999999999999999999999999 frames", 24)]
    [InlineData("99999999999999999999999999999 frames", 0.000000001)]
    public void EditFrame_returns_diagnostic_for_overflowing_frame_text(string text, decimal framesPerSecond)
    {
        var sample = Sample();

        var result = service.EditFrame(sample, 1, text, framesPerSecond);

        Assert.Equal(sample.Chapters[1].StartTime, result.ChapterSet.Chapters[1].StartTime);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.InvalidFrameText);
    }

    [Fact]
    public void Delete_first_chapter_shifts_remaining_times_to_zero()
    {
        var result = service.Delete(Sample(), new HashSet<int> { 0 });

        Assert.Equal(TimeSpan.Zero, result.ChapterSet.Chapters[0].StartTime);
        Assert.Equal(1, result.ChapterSet.Chapters[0].DisplayNumber);
    }

    [Fact]
    public void InsertBefore_inserts_new_chapter_and_renumbers()
    {
        var result = service.InsertBefore(Sample(), 1);

        Assert.Equal("New Chapter", result.ChapterSet.Chapters[1].Name);
        Assert.Equal([1, 2, 3, 4], result.ChapterSet.Chapters.Select(static c => c.DisplayNumber).ToArray());
    }

    [Fact]
    public void ApplyTemplate_replaces_names_in_order_and_preserves_missing()
    {
        var result = service.ApplyTemplate(Sample(), "One\nTwo");

        Assert.Equal("One", result.ChapterSet.Chapters[0].Name);
        Assert.Equal("Two", result.ChapterSet.Chapters[1].Name);
        Assert.Equal("End", result.ChapterSet.Chapters[2].Name);
    }

    [Fact]
    public void ApplyOrderShift_normalizes_negative_shift_to_zero()
    {
        var result = service.ApplyOrderShift(Sample(), -2);

        Assert.Equal([1, 2, 3], result.ChapterSet.Chapters.Select(static chapter => chapter.DisplayNumber).ToArray());
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.OrderShiftNormalized);
    }

    [Fact]
    public void ApplyOrderShift_uses_non_separator_output_order()
    {
        var info = Sample() with
        {
            Chapters =
            [
                new Chapter(1, TimeSpan.Zero, "Intro"),
                Chapter.Separator(),
                new Chapter(2, TimeSpan.FromSeconds(10), "Middle")
            ]
        };

        var result = service.ApplyOrderShift(info, 2);

        Assert.Equal([3, 0, 4], result.ChapterSet.Chapters.Select(static chapter => chapter.DisplayNumber).ToArray());
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ShiftFramesForward_subtracts_shift_and_removes_negative_chapters()
    {
        var result = service.ShiftFramesForward(Sample(), 240, 24);

        Assert.Equal(TimeSpan.Zero, result.ChapterSet.Chapters[0].StartTime);
        Assert.Equal(TimeSpan.FromSeconds(10), result.ChapterSet.Chapters[1].StartTime);
        Assert.Equal([1, 2], result.ChapterSet.Chapters.Select(static chapter => chapter.DisplayNumber).ToArray());
    }

    [Fact]
    public void CreateZones_uses_selected_row_frame_ranges()
    {
        var info = Sample() with
        {
            Chapters =
            [
                new Chapter(1, TimeSpan.Zero, "Intro", "0"),
                new Chapter(2, TimeSpan.FromSeconds(10), "Middle", "240"),
                new Chapter(3, TimeSpan.FromSeconds(20), "End", "480")
            ]
        };

        var result = service.CreateZones(info, new HashSet<int> { 0, 1 }, 24);

        Assert.Equal("--zones 0,239,/240,479,", result.Zones);
        Assert.Empty(result.Diagnostics);
    }

    private static ChapterSet Sample() =>
        new(
            "Title",
            "source",
            ChapterImportFormat.Ogm,
            24,
            TimeSpan.FromSeconds(30),
            [
                new Chapter(1, TimeSpan.FromSeconds(5), "Intro"),
                new Chapter(2, TimeSpan.FromSeconds(10), "Middle"),
                new Chapter(3, TimeSpan.FromSeconds(20), "End")
            ]);
}
