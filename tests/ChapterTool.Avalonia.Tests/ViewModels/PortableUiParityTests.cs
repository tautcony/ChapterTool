using System.Runtime.Versioning;
using System.Text;
using System.Xml.Linq;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Platform;
using ChapterTool.TestSupport;
using ChapterTool.Wasm.Services;

namespace ChapterTool.Avalonia.Tests.ViewModels;

[SupportedOSPlatform("browser")]
public sealed class PortableUiParityTests
{
    [Theory]
    [InlineData("00001_fch.mpls")]
    [InlineData("00000_HEVC.mpls")]
    [InlineData("00001_Hidan_no_Aria_AA.mpls")]
    public async Task MatchingOperationsUseTheSameCommittedContentHistoryAndExport(string fixture)
    {
        var path = TestRepository.CoreFixture("Importing", "Disc", "Mpls", fixture);
        var bytes = await File.ReadAllBytesAsync(path);
        var service = new WasmChapterService();
        var imported = await service.ImportAsync(path, bytes);
        var formatter = new ChapterTimeFormatter();
        var engine = new LuaExpressionScriptService();
        var logs = new ApplicationLogPanelProvider();
        using var desktop = new MainWindowViewModel(new Loader(imported), new Saver(),
            new ChapterEditingService(formatter), new ChapterSegmentService(), new Tools(), formatter, logs,
            TestApplicationLogger.Create<MainWindowViewModel>(logs), new FrameRateService(),
            new AppLocalizationManager("en-US"), engine, new ChapterExportService(formatter, engine));
        using var web = new WasmWorkspace(service);
        await desktop.LoadCommand.ExecuteAsync(path);
        await web.LoadAsync(path, bytes);
        web.SelectedFrameRateIndex = desktop.SelectedFrameRateIndex;
        AssertRows(desktop, web);

        var historyBeforePreferences = desktop.HistoryEntries.Count;
        desktop.RoundFrames = false;
        web.RoundFrames = false;
        web.ApplyOptionsAndRefresh();
        Assert.Equal(historyBeforePreferences, desktop.HistoryEntries.Count);
        AssertRows(desktop, web);
        desktop.RoundFrames = true;
        web.RoundFrames = true;
        web.ApplyOptionsAndRefresh();

        if (web.ClipOptions.Count > 1)
        {
            await desktop.SelectClipCommand.ExecuteAsync(1);
            web.SelectAdjacentClip(1);
            AssertRows(desktop, web);
        }

        desktop.AutoGenerateNames = true;
        desktop.OrderShift = 2;
        web.ChapterNameModeIndex = 1;
        web.OrderShift = 2;
        var desktopNaming = desktop.ToolSession.ContentOperations.PrepareContentOptions();
        web.PrepareNamingPreview();
        Assert.Equal(TransactionOutcomeKind.Committed, (await desktop.ToolSession.ContentOperations.ApplyAsync(desktopNaming)).Kind);
        Assert.True(await web.ApplyContentPreviewAsync());
        AssertRows(desktop, web);

        var zeroShift = desktop.ToolSession.ContentOperations.PrepareFrameShift(0);
        Assert.True(zeroShift.IsValid);
        Assert.Empty(zeroShift.Differences);
        web.PrepareFrameShiftPreview(0);
        Assert.False(web.ContentPreviewProjection!.HasChanges);
        Assert.False(await web.ApplyContentPreviewAsync());

        await desktop.EditFrameCommand.ExecuteAsync(new ChapterCellEdit(0, "240"));
        var cellDraft = web.BeginCellEdit(0, ChapterCellField.Frame);
        Assert.NotNull(cellDraft);
        Assert.Null(await web.CommitCellEditAsync(cellDraft, "240"));
        Assert.True(desktop.Rows[0].TimeText == web.Rows[0].TimeText, desktop.StatusText);
        AssertRows(desktop, web);

        var nameIndex = Math.Min(1, web.Rows.Count - 1);
        await desktop.EditNameCommand.ExecuteAsync(new ChapterCellEdit(nameIndex, "手動 日本語"));
        var nameDraft = web.BeginCellEdit(nameIndex, ChapterCellField.Name)!;
        Assert.Null(await web.CommitCellEditAsync(nameDraft, "手動 日本語"));
        AssertRows(desktop, web);

        var desktopShift = desktop.ToolSession.ContentOperations.PrepareFrameShift(-1);
        var webShift = web.PrepareFrameShiftPreview(-1)!;
        Assert.Equal(desktopShift.IsValid, webShift.IsValid);
        Assert.Equal(TransactionOutcomeKind.Committed, (await desktop.ToolSession.ContentOperations.ApplyAsync(desktopShift)).Kind);
        Assert.True(await web.ApplyContentPreviewAsync());
        AssertRows(desktop, web);

        var desktopShiftedTicks = desktop.ToolSession.ContentOperations.PrepareContentOptions().Before.Tracks[0].Chapters[0].StartTicks;
        var webShiftedTicks = web.PrepareNamingPreview()!.Before.Tracks[0].Chapters[0].StartTicks;
        Assert.Equal(desktopShiftedTicks, webShiftedTicks);
        var desktopExpression = desktop.ToolSession.ContentOperations.PrepareExpression("t / 2");
        web.PrepareExpressionPreview("t / 2");
        Assert.Equal(TransactionOutcomeKind.Committed, (await desktop.ToolSession.ContentOperations.ApplyAsync(desktopExpression)).Kind);
        Assert.True(await web.ApplyExpressionPreviewAsync());
        AssertRows(desktop, web);

        Assert.Equal(Encoding.UTF8.GetBytes(desktop.BuildPreview()), Encoding.UTF8.GetBytes(web.Preview().Content));
        Assert.Equal(desktop.HistoryEntries.Count, web.HistoryEntries.Count);
        await desktop.UndoCommand.ExecuteAsync();
        await web.UndoAsync();
        AssertRows(desktop, web);
        await desktop.RedoCommand.ExecuteAsync();
        await web.RedoAsync();
        AssertRows(desktop, web);

        var conversionBefore = web.PrepareNamingPreview()!.Before;
        var sourceRate = desktop.ToolSession.ContentOperations.PrepareContentOptions().Before.FrameRate;
        var fps = sourceRate is { } rate ? (decimal)rate.Numerator / rate.Denominator : 24000m / 1001m;
        var desktopConversion = desktop.ToolSession.ContentOperations.PrepareFrameRateConversion(fps, 25m);
        web.SelectedFrameRateIndex = 3;
        var webConversion = web.PrepareFrameRatePreview()!;
        Assert.True(desktopConversion.IsValid, string.Join("; ", desktopConversion.Errors));
        Assert.True(webConversion.IsValid, string.Join("; ", webConversion.Errors));
        Assert.Equal(TransactionOutcomeKind.Committed, (await desktop.ToolSession.ContentOperations.ApplyAsync(desktopConversion)).Kind);
        Assert.True(await web.ApplyContentPreviewAsync());
        AssertRows(desktop, web);
        var conversionAfter = web.PrepareNamingPreview()!.Before;
        if (conversionBefore.Tracks.Length > 1)
        {
            Assert.Same(conversionBefore.Tracks[0], conversionAfter.Tracks[0]);
        }
        Assert.Equal(Encoding.UTF8.GetBytes(desktop.BuildPreview()), Encoding.UTF8.GetBytes(web.Preview().Content));
        Assert.Equal(desktop.HistoryEntries.Count, web.HistoryEntries.Count);
        var historyCount = web.HistoryEntries.Count;
        for (var index = 0; index < web.SaveFormats.Count; index++)
        {
            desktop.SaveFormat = ChapterExportFormats.AtIndex(index);
            web.SaveFormatIndex = index;
            var exported = web.Preview();
            Assert.True(exported.Success, exported.Message);
            if (desktop.SaveFormat == ChapterExportFormat.Xml)
            {
                var desktopXml = XDocument.Parse(desktop.BuildPreview());
                var webXml = XDocument.Parse(exported.Content);
                foreach (var xml in new[] { desktopXml, webXml })
                {
                    xml.Descendants().Where(element => element.Name.LocalName is "ChapterUID" or "EditionUID").Remove();
                }
                Assert.True(XNode.DeepEquals(desktopXml, webXml));
            }
            else
            {
                Assert.Equal(Encoding.UTF8.GetBytes(desktop.BuildPreview()), Encoding.UTF8.GetBytes(exported.Content));
            }
        }
        Assert.Equal(historyCount, web.HistoryEntries.Count);
        Assert.Equal(historyCount, desktop.HistoryEntries.Count);
    }

    private static void AssertRows(MainWindowViewModel desktop, WasmWorkspace web)
    {
        Assert.Equal(desktop.Rows.Select(row => (row.Number, row.TimeText, row.Name, row.FramesInfo)),
            web.Rows.Select(row => (row.Number, row.TimeText, row.Name, row.FramesInfo)));
        var desktopDocument = desktop.ToolSession.ContentOperations.PrepareContentOptions().Before;
        var webDocument = web.PrepareNamingPreview()!.Before;
        Assert.Equal((desktopDocument.Title, desktopDocument.SourceName, desktopDocument.ImportFormat, desktopDocument.Duration, desktopDocument.FrameRate),
            (webDocument.Title, webDocument.SourceName, webDocument.ImportFormat, webDocument.Duration, webDocument.FrameRate));
        Assert.Equal(desktopDocument.Tracks.Select(track => track.Name), webDocument.Tracks.Select(track => track.Name));
        Assert.Equal(desktopDocument.Tracks.SelectMany(track => track.Chapters).Select(chapter => chapter.StartTicks),
            webDocument.Tracks.SelectMany(track => track.Chapters).Select(chapter => chapter.StartTicks));
        Assert.Equal(desktopDocument.Tracks.SelectMany(track => track.Segments).Select(segment => (segment.StartTicks, segment.Duration, segment.FrameRate, segment.SourceFrameRate)),
            webDocument.Tracks.SelectMany(track => track.Segments).Select(segment => (segment.StartTicks, segment.Duration, segment.FrameRate, segment.SourceFrameRate)));
    }

    private sealed class Loader(ChapterImportResult result) : IChapterLoadService
    {
        public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken) => ValueTask.FromResult(result);
    }

    private sealed class Saver : IChapterSaveService
    {
        public ValueTask<ChapterExportResult> SaveAsync(ChapterSet info, ChapterExportOptions options,
            string? directory, CancellationToken cancellationToken, string? sourcePath = null) =>
            ValueTask.FromResult(new ChapterExportResult(true, string.Empty, ".txt", []));
    }

    private sealed class Tools : IAuxiliaryToolHost
    {
        public ValueTask<AuxiliaryToolResult> OpenAsync(ToolId toolId, AuxiliaryToolRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AuxiliaryToolResult(AuxiliaryToolResultKind.Opened, toolId));

        public ValueTask<AuxiliaryToolResult> CloseAsync(ToolId toolId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AuxiliaryToolResult(AuxiliaryToolResultKind.Closed, toolId));

        public void Dispose()
        {
        }
    }
}
