using System.Runtime.Versioning;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;
using ChapterTool.Wasm.Services;

namespace ChapterTool.Wasm.Tests;

[SupportedOSPlatform("browser")]
public sealed class WasmFrameRateChoiceTests
{
    [Theory]
    [InlineData(12345L, 512L)]
    public void Custom_rational_rate_can_be_selected_again_and_auto_keeps_its_authority(long numerator, long denominator)
    {
        var service = new FrameRateService();
        var rate = FrameRateValue.Rational(numerator, denominator);
        var info = CreateChapterSet(rate);

        var choice = service.FindByRate(rate);
        var selected = service.UpdateFrames(info, choice, round: true, tolerance: 0.15m);
        var reselectedChoice = service.FindByRate(rate);
        var reselected = service.UpdateFrames(info, reselectedChoice, round: true, tolerance: 0.15m);
        var automatic = service.UpdateFrames(info, service.Options[0], round: true, tolerance: 0.15m);

        Assert.True(choice.IsValid);
        Assert.Equal(rate, choice.Rate);
        Assert.Equal("12345/512", choice.DisplayName);
        Assert.Equal(choice, reselectedChoice);
        Assert.Equal(rate, selected.Rate);
        Assert.Equal(selected.Rate, reselected.Rate);
        Assert.Equal(rate, automatic.Rate);
        Assert.Equal(rate, automatic.Info.FrameRate);
    }

    [Fact]
    public void Custom_approximate_rate_can_be_selected_again_and_auto_keeps_its_authority()
    {
        var service = new FrameRateService();
        var rate = FrameRateValue.Approximate(23.975m);
        var info = CreateChapterSet(rate);

        var choice = service.FindByRate(rate);
        var selected = service.UpdateFrames(info, choice, round: true, tolerance: 0.15m);
        var reselectedChoice = service.FindByRate(rate);
        var reselected = service.UpdateFrames(info, reselectedChoice, round: true, tolerance: 0.15m);
        var automatic = service.UpdateFrames(info, service.Options[0], round: true, tolerance: 0.15m);

        Assert.True(choice.IsValid);
        Assert.Equal(FrameRateKind.Approximate, choice.Rate.Kind);
        Assert.Equal(23.975m, choice.Rate.ApproximateValue);
        Assert.Equal("23.975", choice.DisplayName);
        Assert.Equal(choice, reselectedChoice);
        Assert.Equal(rate, selected.Rate);
        Assert.Equal(selected.Rate, reselected.Rate);
        Assert.Equal(rate, automatic.Rate);
        Assert.Equal(rate, automatic.Info.FrameRate);
    }

    [Fact]
    public async Task Culture_refresh_keeps_workspace_rate_choice_ticks_and_history_in_en_zh_and_ja()
    {
        var localizer = new WasmLocalizer();
        using var workspace = new WasmWorkspace(new WasmChapterService(), localizer);
        await workspace.LoadSampleAsync();
        workspace.SelectedFrameRateIndex = 1;
        workspace.UpdateRow(0, null, "Edited before language refresh");

        var selectedIndex = workspace.SelectedFrameRateIndex;
        var selectedChoice = Assert.Single(workspace.FrameRateChoices, choice => choice.Index == selectedIndex);
        var selectedRate = workspace.ComputationFrameRate;
        var choices = workspace.FrameRateChoices.ToArray();
        var history = workspace.HistoryEntries.Select(static entry => entry.Id).ToArray();
        var ticks = ReadChapterTicks(workspace);
        var changedEvents = 0;
        workspace.Changed += () => changedEvents++;

        foreach (var culture in new[] { "zh-CN", "ja-JP", "en-US" })
        {
            var eventsBeforeRefresh = changedEvents;
            localizer.SetCulture(culture);

            Assert.True(changedEvents > eventsBeforeRefresh);
            Assert.Equal(selectedIndex, workspace.SelectedFrameRateIndex);
            Assert.Equal(selectedRate, workspace.ComputationFrameRate);
            Assert.Equal(selectedChoice.Option.Rate,
                Assert.Single(workspace.FrameRateChoices, choice => choice.Index == workspace.SelectedFrameRateIndex).Option.Rate);
            Assert.Equal(
                choices.Select(static choice => (choice.Index, choice.Option.Code, choice.Option.Rate)),
                workspace.FrameRateChoices.Select(static choice => (choice.Index, choice.Option.Code, choice.Option.Rate)));
            Assert.Equal(localizer.T("Main.AutoFrameRate"),
                Assert.Single(workspace.FrameRateChoices, choice => choice.Index == 0).DisplayName);
            Assert.Equal(ticks, ReadChapterTicks(workspace));
            Assert.Equal(history, workspace.HistoryEntries.Select(static entry => entry.Id));
        }
    }

    private static ChapterSet CreateChapterSet(FrameRateValue rate) => new(
        "Custom rate",
        null,
        ChapterImportFormat.Unknown,
        rate,
        TimeSpan.FromSeconds(4),
        [
            new Chapter(1, TimeSpan.Zero, "Opening"),
            new Chapter(2, TimeSpan.FromSeconds(2), "Later")
        ]);

    private static long[] ReadChapterTicks(WasmWorkspace workspace)
    {
        var preview = Assert.IsType<Core.Session.ChapterContentPreview>(
            workspace.PrepareExpressionPreview("t"));
        var ticks = preview.Before.Tracks.SelectMany(static track => track.Chapters)
            .Select(static chapter => chapter.StartTicks).ToArray();
        workspace.CancelExpressionPreview();
        return ticks;
    }
}
