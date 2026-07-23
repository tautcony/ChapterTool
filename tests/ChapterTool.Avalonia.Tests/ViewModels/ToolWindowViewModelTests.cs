using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.Session.Ports;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.ViewModels.Tools;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Platform;
using ChapterTool.Infrastructure.Services;

namespace ChapterTool.Avalonia.Tests.ViewModels;

public sealed class ToolWindowViewModelTests
{
    [Fact]
    public async Task TextToolRefreshAndClearUseCallbacks()
    {
        var text = "one";
        var cleared = false;

        // ReSharper disable once AccessToModifiedClosure
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
        var vm = new TextToolViewModel(() => "{\"name\":\"Intro\",\"time\":1}", new TextToolOptions { FormatSelector = new TextToolFormatSelector(owner.PortAdapters.ExportPreferences) });

        Assert.Contains(Environment.NewLine, vm.Text, StringComparison.Ordinal);
        Assert.Contains(vm.Lines.SelectMany(line => line.Spans), span => span.Kind == TextToolSpanKind.Name);
        Assert.Contains(vm.Lines.SelectMany(line => line.Spans), span => span.Kind == TextToolSpanKind.Number);
    }

    [Fact]
    public void TextToolFormatsXmlAndBuildsHighlightLines()
    {
        var owner = CreateOwner();
        owner.SaveFormat = ChapterExportFormat.Xml;
        var vm = new TextToolViewModel(() => "<Chapters><ChapterAtom><ChapterUID>1</ChapterUID></ChapterAtom></Chapters>", new TextToolOptions { FormatSelector = new TextToolFormatSelector(owner.PortAdapters.ExportPreferences) });

        Assert.Contains(Environment.NewLine, vm.Text, StringComparison.Ordinal);
        Assert.Contains(vm.Lines.SelectMany(line => line.Spans), span => span.Kind == TextToolSpanKind.Name);
        Assert.Contains(vm.Lines.SelectMany(line => line.Spans), span => span.Kind == TextToolSpanKind.String);
    }

    [Fact]
    public void TextToolFormatSelectorUpdatesOwnerAndRefreshesPreviewKind()
    {
        var owner = CreateOwner();
        var vm = new TextToolViewModel(owner.BuildPreview, new TextToolOptions { FormatSelector = new TextToolFormatSelector(owner.PortAdapters.ExportPreferences) })
            {
                SelectedFormatIndex = ChapterExportFormats.IndexOf(ChapterExportFormat.Json)
            };

        Assert.Equal(ChapterExportFormat.Json, owner.SaveFormat);
        Assert.Equal(TextToolKind.Json, vm.Kind);
        Assert.True(vm.CanSelectFormat);
        Assert.False(vm.CanClear);
        Assert.Contains("QPFile", vm.FormatOptions);
        Assert.DoesNotContain("Chapter2Qpfile", vm.FormatOptions);
        Assert.Equal(9, vm.FormatOptions.Count);
    }

    [Fact]
    public void DisposedLanguageToolStopsRefreshingLocalizedOptions()
    {
        var localizer = new AppLocalizationManager("en-US");
        var owner = CreateOwner(localizer);
        var vm = new LanguageToolViewModel(owner.PortAdapters.Preferences);
        var notifications = 0;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LanguageToolViewModel.Languages))
            {
                notifications++;
            }
        };

        vm.Dispose();
        localizer.SetCulture("zh-CN");

        Assert.Equal(0, notifications);
    }

    [Fact]
    public async Task ExpressionTemplateAndForwardShiftToolsApplyToOwner()
    {
        var owner = CreateOwner();
        await owner.LoadCommand.ExecuteAsync("movie.txt");

        var expression = new ExpressionToolViewModel(owner.PortAdapters.Expression) { Expression = "t + 1", ApplyExpression = true };
        await expression.ApplyCommand.ExecuteAsync(expression);
        var template = new TemplateNamesToolViewModel(owner.PortAdapters.NamingPreferences) { UseTemplateNames = true };
        await template.ApplyCommand.ExecuteAsync(template);
        var forward = new ForwardShiftToolViewModel(owner.PortAdapters.ChapterEdit) { Frames = 24 };
        await forward.ApplyCommand.ExecuteAsync(forward);

        Assert.Equal("t + 1", owner.Expression);
        Assert.True(owner.ApplyExpression);
        Assert.True(owner.UseTemplateNames);
        Assert.False(owner.AutoGenerateNames);
        Assert.Equal("00:00:05.000", owner.Rows[0].TimeText);
    }

    [Fact]
    public void ToolsConstructAgainstNarrowPortsWithoutFullMainWindowSurface()
    {
        var export = new FakeExportPreferencePort { SaveFormatIndex = 0 };
        var naming = new FakeNamingPreferencePort();
        var edit = new FakeChapterEditPort();
        var language = new FakePreferenceSink(new AppLocalizationManager("en-US"));

        var formatSelector = new TextToolFormatSelector(export);
        formatSelector.Apply(ChapterExportFormats.IndexOf(ChapterExportFormat.Json));
        Assert.Equal(ChapterExportFormat.Json, export.SaveFormat);

        var template = new TemplateNamesToolViewModel(naming) { UseTemplateNames = true };
        Assert.True(template.ApplyCommand.CanExecute(template));

        var forward = new ForwardShiftToolViewModel(edit) { Frames = 12 };
        Assert.True(forward.ApplyCommand.CanExecute(forward));

        var languageTool = new LanguageToolViewModel(language);
        Assert.Equal("en-US", languageTool.SelectedLanguage);
        languageTool.Dispose();
    }

    [Fact]
    public void ToolWindowRegistry_registers_known_tool_ids()
    {
        Assert.NotNull(ToolWindowRegistry.Find("preview"));
        Assert.NotNull(ToolWindowRegistry.Find("settings"));
        Assert.NotNull(ToolWindowRegistry.Find("expression"));
        Assert.NotNull(ToolWindowRegistry.Find("language"));
        Assert.Null(ToolWindowRegistry.Find("missing-tool"));
    }


    [Fact]
    public async Task ExpressionToolAppliesLuaPresetAndExternalScriptToOwner()
    {
        var owner = CreateOwner();
        await owner.LoadCommand.ExecuteAsync("movie.txt");
        var scriptPath = Path.Combine(Path.GetTempPath(), $"chaptertool-{Guid.NewGuid():N}.lua");
        await File.WriteAllTextAsync(scriptPath, "t + 2");
        try
        {
            var picker = new FakeFilePicker(scriptPath);
            var expression = new ExpressionToolViewModel(owner.PortAdapters.Expression, picker) { ApplyExpression = true };

            expression.SelectedPresetIndex = expression.Presets.ToList().FindIndex(preset => preset.Id == "round-to-frame");

            Assert.Equal("round-to-frame", expression.SelectedPreset?.Id);
            Assert.Contains("fps", expression.Expression, StringComparison.Ordinal);
            Assert.Equal("Round to nearest frame", expression.ExpressionSourceName);

            await expression.BrowseScriptCommand.ExecuteAsync();
            await expression.ApplyCommand.ExecuteAsync(expression);

            Assert.Equal("t + 2", owner.Expression);
            Assert.True(owner.ApplyExpression);
            Assert.Equal(string.Empty, owner.ExpressionPresetId);
            Assert.Equal(Path.GetFileName(scriptPath), owner.ExpressionSourceName);
            Assert.Equal("00:00:07.000", owner.Rows[0].TimeText);
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    [Fact]
    public async Task ExpressionToolValidatesHandwrittenScriptWhenApplied()
    {
        var owner = CreateOwner(new AppLocalizationManager("en-US"));
        await owner.LoadCommand.ExecuteAsync("movie.txt");
        var expression = new ExpressionToolViewModel(owner.PortAdapters.Expression)
        {
            Expression = "return (",
            ApplyExpression = true
        };

        await expression.ApplyCommand.ExecuteAsync(expression);

        Assert.Equal("return (", owner.Expression);
        Assert.True(owner.ApplyExpression);
        Assert.Contains("Lua expression syntax error", owner.StatusText, StringComparison.Ordinal);
        Assert.Contains("Lua expression syntax error", expression.StatusText, StringComparison.Ordinal);
        Assert.Contains(owner.LogService.Entries, static entry => entry.MessageKey == "Log.Diagnostic" && Equals(entry.Arguments?["code"], "LuaExpression.CompileFailed"));
    }

    private static MainWindowViewModel CreateOwner(IAppLocalizer? localizer = null)
    {
        var formatter = new ChapterTimeFormatter();
        var expressionEngine = new LuaExpressionScriptService();
        var logService = new ApplicationLogPanelProvider();
        return new MainWindowViewModel(
            new FakeLoadService(new ChapterImportResult(
                true,
                [new ChapterImportSource("movie.txt", [new ChapterImportEntry("0", "movie", new ChapterSet("movie.txt", "movie.txt", ChapterImportFormat.Ogm, 24, TimeSpan.FromSeconds(10), [new Chapter(1, TimeSpan.FromSeconds(5), "Intro")]))])],
                [])),
            new FakeSaveService(),
            new ChapterEditingService(formatter),
            new ChapterSegmentService(),
            new FakeWindowService(),
            formatter,
            logService,
            TestApplicationLogger.Create<MainWindowViewModel>(logService),
            new FrameRateService(),
            localizer ?? new AppLocalizationManager("en-US"),
            expressionEngine,
            new ChapterExportService(formatter, expressionEngine));
    }

    private sealed class FakeLoadService(ChapterImportResult result) : IChapterLoadService
    {
        public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken) => ValueTask.FromResult(result);
    }

    private sealed class FakeSaveService : IChapterSaveService
    {
        public ValueTask<ChapterExportResult> SaveAsync(ChapterSet info, ChapterExportOptions options, string? directory, CancellationToken cancellationToken, string? sourcePath = null) =>
            ValueTask.FromResult(new ChapterExportResult(true, string.Empty, ".txt", []));
    }

    private sealed class FakeFilePicker(string luaScriptPath) : IFilePickerService
    {
        public ValueTask<string?> PickSourceAsync(CancellationToken cancellationToken) => ValueTask.FromResult<string?>(null);

        public ValueTask<string?> PickMplsAsync(CancellationToken cancellationToken) => ValueTask.FromResult<string?>(null);

        public ValueTask<string?> PickChapterNameTemplateAsync(CancellationToken cancellationToken) => ValueTask.FromResult<string?>(null);

        public ValueTask<string?> PickLuaExpressionScriptAsync(CancellationToken cancellationToken) => ValueTask.FromResult<string?>(luaScriptPath);

        public ValueTask<string?> PickSaveDirectoryAsync(CancellationToken cancellationToken) => ValueTask.FromResult<string?>(null);
    }

    private sealed class FakeExportPreferencePort : IExportPreferencePort
    {
        public int SaveFormatIndex
        {
            get => ChapterExportFormats.IndexOf(SaveFormat);
            set => SaveFormat = ChapterExportFormats.AtIndex(value);
        }

        public ChapterExportFormat SaveFormat { get; set; } = ChapterExportFormat.Txt;
    }

    private sealed class FakeNamingPreferencePort : INamingPreferencePort
    {
        public bool AutoGenerateNames { get; set; }

        public bool UseTemplateNames { get; set; }
    }

    private sealed class FakeChapterEditPort : IChapterEditPort
    {
        public void ShiftFramesForward(int frames)
        {
        }
    }

    private sealed class FakePreferenceSink(IAppLocalizer localizer) : IPreferenceSink
    {
        public IAppLocalizer Localizer { get; } = localizer;

        public string UiLanguage { get; private set; } = localizer.CurrentCultureName;

        public int SaveFormatIndex { get; private set; }

        public string XmlLanguage { get; private set; } = "und";

        public OutputTextEncoding OutputTextEncoding { get; private set; } = OutputTextEncoding.Utf8;

        public decimal FrameAccuracyTolerance { get; private set; } = 0.15m;

        public void ApplyLivePreferences(AppSettings settings)
        {
            UiLanguage = AppLanguage.Normalize(settings.Language);
        }

        public ValueTask SaveUiLanguageAsync(string language, CancellationToken cancellationToken)
        {
            UiLanguage = AppLanguage.Normalize(language);
            Localizer.SetCulture(UiLanguage);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeWindowService : IWindowService
    {
        public ValueTask ShowAsync(string windowId, object? parameter, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask HideAsync(string windowId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
