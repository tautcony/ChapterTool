using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Platform;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.Tests.ViewModels;

public sealed class ToolWindowViewModelTests
{
    [Fact]
    public async Task TextToolRefreshAndClearUseCallbacks()
    {
        var text = "one";
        var cleared = false;
        var vm = new TextToolViewModel(() => text, new TextToolOptions { ClearAction = () => cleared = true });

        text = "two";
        await vm.RefreshCommand.ExecuteAsync();
        await vm.ClearCommand.ExecuteAsync();

        Assert.Equal(string.Empty, vm.Text);
        Assert.True(cleared);
    }

    [Fact]
    public void TextToolFormatsJsonAndBuildsHighlightLines()
    {
        var owner = CreateOwner();
        owner.SaveFormat = ChapterExportFormat.Json;
        var vm = new TextToolViewModel(() => "{\"name\":\"Intro\",\"time\":1}", new TextToolOptions { FormatSelector = new TextToolFormatSelector(owner) });

        Assert.Contains(Environment.NewLine, vm.Text, StringComparison.Ordinal);
        Assert.Contains(vm.Lines.SelectMany(line => line.Spans), span => span.Kind == TextToolSpanKind.Name);
        Assert.Contains(vm.Lines.SelectMany(line => line.Spans), span => span.Kind == TextToolSpanKind.Number);
    }

    [Fact]
    public void TextToolFormatsXmlAndBuildsHighlightLines()
    {
        var owner = CreateOwner();
        owner.SaveFormat = ChapterExportFormat.Xml;
        var vm = new TextToolViewModel(() => "<Chapters><ChapterAtom><ChapterUID>1</ChapterUID></ChapterAtom></Chapters>", new TextToolOptions { FormatSelector = new TextToolFormatSelector(owner) });

        Assert.Contains(Environment.NewLine, vm.Text, StringComparison.Ordinal);
        Assert.Contains(vm.Lines.SelectMany(line => line.Spans), span => span.Kind == TextToolSpanKind.Name);
        Assert.Contains(vm.Lines.SelectMany(line => line.Spans), span => span.Kind == TextToolSpanKind.String);
    }

    [Fact]
    public void TextToolFormatSelectorUpdatesOwnerAndRefreshesPreviewKind()
    {
        var owner = CreateOwner();
        var vm = new TextToolViewModel(owner.BuildPreview, new TextToolOptions { FormatSelector = new TextToolFormatSelector(owner) })
            {
                SelectedFormatIndex = (int)ChapterExportFormat.Json
            };

        Assert.Equal(ChapterExportFormat.Json, owner.SaveFormat);
        Assert.Equal(TextToolKind.Json, vm.Kind);
        Assert.True(vm.CanSelectFormat);
        Assert.False(vm.CanClear);
        Assert.Contains("QPFile", vm.FormatOptions);
        Assert.Contains("Chapter2Qpfile", vm.FormatOptions);
        Assert.Equal(10, vm.FormatOptions.Count);
    }

    [AvaloniaFact]
    public void TextToolRefreshesWhenLiveLogServiceAddsEntry()
    {
        var logService = new ApplicationLogPanelProvider();
        var logger = logService.CreateLogger("ChapterTool.Tests");
        var vm = new TextToolViewModel(
            () => logService.Format(),
            new TextToolOptions { LiveRefreshService = logService });

        logger.LogInformation("Live event");
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Live event", vm.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task TextToolMarshalsLiveLogRefreshFromBackgroundThread()
    {
        var logService = new ApplicationLogPanelProvider();
        var logger = logService.CreateLogger("ChapterTool.Tests");
        var threadIds = new List<int>();
        var vm = new TextToolViewModel(
            () =>
            {
                threadIds.Add(Environment.CurrentManagedThreadId);
                return logService.Format();
            },
            new TextToolOptions { LiveRefreshService = logService });
        var uiThreadId = Environment.CurrentManagedThreadId;

        await Task.Run(() => logger.LogInformation("Background event"));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Background event", vm.Text, StringComparison.Ordinal);
        Assert.Equal(uiThreadId, threadIds[^1]);
    }

    [Fact]
    public async Task ColorSettingsToolPersistsSixNormalizedSlots()
    {
        var store = new FakeThemeSettingsStore();
        var vm = new ColorSettingsViewModel(store);
        vm.Slots[0].Value = "#abcdef";
        vm.Slots[1].Value = "invalid";

        await vm.SaveCommand.ExecuteAsync();

        Assert.Equal("#ABCDEF", store.Current.BackChange);
        Assert.Equal(ThemeColorSettings.Default.TextBack, store.Current.TextBack);
    }

    [Fact]
    public async Task ExpressionTemplateAndForwardShiftToolsApplyToOwner()
    {
        var owner = CreateOwner();
        await owner.LoadCommand.ExecuteAsync("movie.txt");

        var expression = new ExpressionToolViewModel(owner) { Expression = "t + 1", ApplyExpression = true };
        await expression.ApplyCommand.ExecuteAsync(expression);
        var template = new TemplateNamesToolViewModel(owner) { UseTemplateNames = true };
        await template.ApplyCommand.ExecuteAsync(template);
        var forward = new ForwardShiftToolViewModel(owner) { Frames = 24 };
        await forward.ApplyCommand.ExecuteAsync(forward);

        Assert.Equal("t + 1", owner.Expression);
        Assert.True(owner.ApplyExpression);
        Assert.True(owner.UseTemplateNames);
        Assert.False(owner.AutoGenerateNames);
        Assert.Equal("00:00:05.000", owner.Rows[0].TimeText);
    }

    private static MainWindowViewModel CreateOwner()
    {
        var formatter = new ChapterTimeFormatter();
        var logService = new ApplicationLogPanelProvider();
        return new MainWindowViewModel(
            new FakeLoadService(new ChapterImportResult(
                true,
                [new ChapterInfoGroup("movie.txt", [new ChapterSourceOption("0", "movie", new ChapterInfo("movie.txt", "movie.txt", 0, "OGM", 24, TimeSpan.FromSeconds(10), [new Chapter(1, TimeSpan.FromSeconds(5), "Intro")]))])],
                [])),
            new FakeSaveService(),
            new ChapterEditingService(formatter),
            new ChapterSegmentService(),
            new FakeWindowService(),
            formatter,
            logService,
            TestApplicationLogger.Create<MainWindowViewModel>(logService));
    }

    private sealed class FakeThemeSettingsStore : ISettingsStore<ThemeColorSettings>
    {
        public ThemeColorSettings Current { get; private set; } = ThemeColorSettings.Default;

        public ValueTask<ThemeColorSettings> LoadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Current);

        public ValueTask SaveAsync(ThemeColorSettings settings, CancellationToken cancellationToken)
        {
            Current = settings;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeLoadService(ChapterImportResult result) : IChapterLoadService
    {
        public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken) => ValueTask.FromResult(result);
    }

    private sealed class FakeSaveService : IChapterSaveService
    {
        public ValueTask<ChapterExportResult> SaveAsync(ChapterInfo info, ChapterExportOptions options, string? directory, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ChapterExportResult(true, string.Empty, ".txt", []));
    }

    private sealed class FakeWindowService : IWindowService
    {
        public ValueTask ShowAsync(string windowId, object? parameter, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask HideAsync(string windowId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
