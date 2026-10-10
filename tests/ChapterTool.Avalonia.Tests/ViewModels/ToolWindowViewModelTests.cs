using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Platform;
using ChapterTool.TestSupport;

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
        var vm = new TextToolViewModel(() => "{\"name\":\"Intro\",\"time\":1}", new TextToolOptions { FormatSelector = new TextToolFormatSelector(owner.ToolSession.ExportPreferences) });

        Assert.Contains(Environment.NewLine, vm.Text, StringComparison.Ordinal);
        Assert.Contains(vm.Lines.SelectMany(line => line.Spans), span => span.Kind == TextToolSpanKind.Name);
        Assert.Contains(vm.Lines.SelectMany(line => line.Spans), span => span.Kind == TextToolSpanKind.Number);
    }

    [Fact]
    public void TextToolFormatsXmlAndBuildsHighlightLines()
    {
        var owner = CreateOwner();
        owner.SaveFormat = ChapterExportFormat.Xml;
        var vm = new TextToolViewModel(() => "<Chapters><ChapterAtom><ChapterUID>1</ChapterUID></ChapterAtom></Chapters>", new TextToolOptions { FormatSelector = new TextToolFormatSelector(owner.ToolSession.ExportPreferences) });

        Assert.Contains(Environment.NewLine, vm.Text, StringComparison.Ordinal);
        Assert.Contains(vm.Lines.SelectMany(line => line.Spans), span => span.Kind == TextToolSpanKind.Name);
        Assert.Contains(vm.Lines.SelectMany(line => line.Spans), span => span.Kind == TextToolSpanKind.String);
    }

    [Fact]
    public void TextToolFormatSelectorUpdatesOwnerAndRefreshesPreviewKind()
    {
        var owner = CreateOwner();
        var vm = new TextToolViewModel(owner.BuildPreview, new TextToolOptions { FormatSelector = new TextToolFormatSelector(owner.ToolSession.ExportPreferences) })
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
        var vm = new LanguageToolViewModel(owner.ToolSession.Preferences);
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

        var expression = new ExpressionToolViewModel(owner.ToolSession.Expression, contentOperations: owner.ToolSession.ContentOperations) { Expression = "t + 1", ApplyExpression = true };
        await expression.ApplyCommand.ExecuteAsync(expression);
        Assert.Equal(ExpressionPreviewReadiness.Ready, expression.PreviewReadiness);
        Assert.True(expression.CanApplyPreview);
        Assert.Equal("00:00:05.000", owner.Rows[0].TimeText);
        await expression.ConfirmApplyCommand.ExecuteAsync();
        var template = new TemplateNamesToolViewModel(owner.ToolSession.NamingPreferences, owner.ToolSession.ContentOperations) { UseTemplateNames = true };
        await template.ApplyCommand.ExecuteAsync(template);
        Assert.True(template.IsPreviewPending);
        await template.ConfirmApplyCommand.ExecuteAsync();
        var forward = new ForwardShiftToolViewModel(owner.ToolSession.ChapterEdit, contentOperations: owner.ToolSession.ContentOperations) { Frames = 24 };
        await forward.ApplyCommand.ExecuteAsync(forward);
        Assert.True(forward.IsPreviewPending);
        var beforeShift = owner.Rows[0].TimeText;
        await forward.CancelPreviewCommand.ExecuteAsync();
        Assert.False(forward.IsPreviewPending);
        Assert.Equal(beforeShift, owner.Rows[0].TimeText);
        await forward.ApplyCommand.ExecuteAsync(forward);
        await forward.ConfirmApplyCommand.ExecuteAsync();

        Assert.Equal("t + 1", owner.Expression);
        Assert.False(owner.ApplyExpression);
        Assert.False(owner.UseTemplateNames);
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
    public void Standard_catalog_registers_known_tool_ids()
    {
        var catalog = StandardToolCatalogFactory.Create();

        Assert.True(catalog.TryGet(ToolIds.Preview, out _));
        Assert.True(catalog.TryGet(ToolIds.Settings, out _));
        Assert.True(catalog.TryGet(ToolIds.Expression, out _));
        Assert.True(catalog.TryGet(ToolIds.History, out var history));
        Assert.True(history.IsModal);
        Assert.True(catalog.TryGet(ToolIds.Language, out _));
        Assert.False(catalog.TryGet("missing-tool", out _));
    }


    [Fact]
    public async Task ExpressionToolAppliesLuaPresetAndExternalScriptToOwner()
    {
        var owner = CreateOwner();
        await owner.LoadCommand.ExecuteAsync("movie.txt");
        var scriptPath = Path.Combine(Path.GetTempPath(), $"chaptertool-{Guid.NewGuid():N}.lua");
        await File.WriteAllTextAsync(scriptPath, "t + 2");
        var closeCount = 0;
        try
        {
            var picker = new FakeFilePicker(scriptPath);
            var expression = new ExpressionToolViewModel(owner.ToolSession.Expression, picker,
                contentOperations: owner.ToolSession.ContentOperations, closeTool: () => closeCount++) { ApplyExpression = true };

            expression.SelectedPresetIndex = expression.Presets.ToList().FindIndex(preset => preset.Id == "round-to-frame");

            Assert.Equal("round-to-frame", expression.SelectedPreset?.Id);
            Assert.Contains("fps", expression.Expression, StringComparison.Ordinal);
            Assert.Equal("Round to nearest frame", expression.ExpressionSourceName);

            await expression.BrowseScriptCommand.ExecuteAsync();
            await expression.ApplyCommand.ExecuteAsync(expression);
            Assert.Equal(ExpressionPreviewReadiness.Ready, expression.PreviewReadiness);
            Assert.Contains("time changes", expression.PreviewSummary, StringComparison.Ordinal);
            Assert.Equal("00:00:05.000", owner.Rows[0].TimeText);
            await expression.ConfirmApplyCommand.ExecuteAsync();

            Assert.Equal("t + 2", owner.Expression);
            Assert.False(owner.ApplyExpression);
            Assert.Equal(string.Empty, owner.ExpressionPresetId);
            Assert.Equal(Path.GetFileName(scriptPath), owner.ExpressionSourceName);
            Assert.Equal("00:00:07.000", owner.Rows[0].TimeText);
            Assert.Contains("Applied expression", owner.StatusText, StringComparison.Ordinal);
            Assert.False(expression.CanApplyPreview);
            Assert.True(owner.CanUndo);
            Assert.Equal(1, closeCount);
            await expression.ConfirmApplyCommand.ExecuteAsync();
            Assert.Equal(1, closeCount);

        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    [Fact]
    public async Task ExpressionToolRejectsEmptyAndInvalidDraftsAndCancelKeepsSavedSettings()
    {
        var owner = CreateOwner();
        await owner.LoadCommand.ExecuteAsync("movie.txt");
        var closeCount = 0;
        var expression = new ExpressionToolViewModel(owner.ToolSession.Expression,
            contentOperations: owner.ToolSession.ContentOperations, closeTool: () => closeCount++);

        expression.Expression = string.Empty;
        Assert.Equal(ExpressionPreviewReadiness.Empty, expression.PreviewReadiness);
        Assert.False(expression.CanApplyPreview);

        expression.Expression = "return (";
        Assert.Equal(ExpressionPreviewReadiness.Waiting, expression.PreviewReadiness);
        expression.RefreshPreviewNow();
        Assert.Equal(ExpressionPreviewReadiness.Invalid, expression.PreviewReadiness);
        Assert.False(expression.CanApplyPreview);

        await expression.CancelPreviewCommand.ExecuteAsync();

        Assert.Equal("t", owner.Expression);
        Assert.Equal(string.Empty, owner.ExpressionPresetId);
        Assert.Equal(1, closeCount);
    }

    [Fact]
    public async Task ExpressionToolMarksConflictedCandidateStaleAndRequiresExplicitRefresh()
    {
        var owner = CreateOwner();
        await owner.LoadCommand.ExecuteAsync("movie.txt");
        var expression = new ExpressionToolViewModel(owner.ToolSession.Expression,
            contentOperations: owner.ToolSession.ContentOperations) { Expression = "t / 2" };
        expression.RefreshPreviewNow();
        var original = expression.Projection;
        var competingChange = owner.ToolSession.ContentOperations.PrepareFrameShift(1);
        var competingOutcome = await owner.ToolSession.ContentOperations.ApplyAsync(competingChange);
        Assert.Equal(TransactionOutcomeKind.Committed, competingOutcome.Kind);

        await expression.ConfirmApplyCommand.ExecuteAsync();

        Assert.Equal(ExpressionPreviewReadiness.Stale, expression.PreviewReadiness);
        Assert.Equal("t / 2", expression.Expression);
        Assert.Same(original, expression.Projection);
        Assert.True(expression.CanRefreshPreview);
        Assert.False(expression.CanApplyPreview);

        await expression.UpdatePreviewCommand.ExecuteAsync();

        Assert.Equal(ExpressionPreviewReadiness.Ready, expression.PreviewReadiness);
        Assert.NotSame(original, expression.Projection);
        Assert.True(expression.CanApplyPreview);
    }

    [Fact]
    public async Task ExpressionToolMarksCompleteNoChangeCandidateUnchanged()
    {
        var owner = CreateOwner();
        await owner.LoadCommand.ExecuteAsync("movie.txt");
        var operations = new DeferredContentOperationPort(owner.ToolSession.ContentOperations, completion: null, forceNoChange: true);
        using var expression = new ExpressionToolViewModel(owner.ToolSession.Expression,
            contentOperations: operations);
        expression.Expression = "t + 2";

        expression.RefreshPreviewNow();

        Assert.Equal(ExpressionPreviewReadiness.Unchanged, expression.PreviewReadiness);
        Assert.False(expression.CanApplyPreview);
    }

    [Fact]
    public async Task ExpressionToolDisablesWhileApplyingAndRetainsFailedCandidate()
    {
        var owner = CreateOwner();
        await owner.LoadCommand.ExecuteAsync("movie.txt");
        var completion = new TaskCompletionSource<TransactionOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operations = new DeferredContentOperationPort(owner.ToolSession.ContentOperations, completion);
        var expression = new ExpressionToolViewModel(owner.ToolSession.Expression,
            contentOperations: operations) { Expression = "t / 2" };
        expression.RefreshPreviewNow();
        var preview = Assert.IsType<ChapterContentPreview>(operations.ExpressionPreview);

        var applying = expression.ConfirmApplyCommand.ExecuteAsync();
        Assert.Equal(ExpressionPreviewReadiness.Applying, expression.PreviewReadiness);
        Assert.False(expression.CanEditExpression);
        Assert.False(expression.CanApplyPreview);

        completion.SetResult(new TransactionOutcome(
            preview.PreviewId,
            TransactionOutcomeKind.Invalid,
            new SessionSnapshot(preview.Before, preview.BaseToken.StateIdentity, preview.BaseToken.MutationRevision),
            System.Collections.Immutable.ImmutableArray.Create("The simulated transaction failed.")));
        await applying;

        Assert.Equal(ExpressionPreviewReadiness.Failed, expression.PreviewReadiness);
        Assert.True(expression.CanEditExpression);
        Assert.False(expression.CanApplyPreview);
        Assert.Same(preview, operations.ExpressionPreview);
        Assert.Equal("The simulated transaction failed.", expression.StatusText);
    }

    [Fact]
    public async Task ExpressionToolBrowseScriptReportsMissingFileInStatus()
    {
        var owner = CreateOwner(new AppLocalizationManager("en-US"));
        var missing = Path.Combine(Path.GetTempPath(), $"chaptertool-missing-{Guid.NewGuid():N}.lua");
        var picker = new FakeFilePicker(missing);
        var expression = new ExpressionToolViewModel(owner.ToolSession.Expression, picker);

        await expression.BrowseScriptCommand.ExecuteAsync();

        Assert.Contains("Failed to load Lua script", expression.StatusText, StringComparison.Ordinal);
        Assert.Null(expression.BrowseScriptCommand.ExecutionError);
    }

    [Fact]
    public void ExpressionLanguageAndForwardShiftCommandsAcceptErrorHandler()
    {
        var owner = CreateOwner();
        var observed = 0;
        Func<Exception, ValueTask> handler = exception =>
        {
            _ = exception;
            observed++;
            return ValueTask.CompletedTask;
        };

        var expression = new ExpressionToolViewModel(owner.ToolSession.Expression, errorHandler: handler);
        using var language = new LanguageToolViewModel(owner.ToolSession.Preferences, handler);
        var forward = new ForwardShiftToolViewModel(owner.ToolSession.ChapterEdit, handler);

        Assert.Same(handler, expression.BrowseScriptCommand.ErrorHandler);
        Assert.Same(handler, expression.ApplyCommand.ErrorHandler);
        Assert.Same(handler, language.ApplyCommand.ErrorHandler);
        Assert.Same(handler, forward.ApplyCommand.ErrorHandler);
        Assert.Equal(0, observed);
    }

    [Fact]
    public async Task ExpressionToolValidatesHandwrittenScriptWhenApplied()
    {
        var owner = CreateOwner(new AppLocalizationManager("en-US"));
        await owner.LoadCommand.ExecuteAsync("movie.txt");
        var expression = new ExpressionToolViewModel(owner.ToolSession.Expression, contentOperations: owner.ToolSession.ContentOperations)
        {
            Expression = "return ("
        };

        expression.RefreshPreviewNow();

        Assert.Equal("t", owner.Expression);
        Assert.Equal("return (", expression.Expression);
        Assert.False(expression.CanApplyPreview);
        Assert.Contains("Lua expression syntax error", expression.StatusText, StringComparison.Ordinal);
        Assert.Contains(owner.LogService.Entries, static entry =>
            entry.Operation == "Lua expression script"
            && entry.Message.StartsWith("Lua expression script diagnostic:", StringComparison.Ordinal)
            && Equals(entry.Arguments?["code"], "LuaExpression.CompileFailed"));

        await expression.CancelPreviewCommand.ExecuteAsync();
        Assert.Equal("00:00:05.000", owner.Rows[0].TimeText);
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

    private sealed class DeferredContentOperationPort(
        IChapterContentOperationPort inner,
        TaskCompletionSource<TransactionOutcome>? completion,
        bool forceNoChange = false) : IChapterContentOperationPort
    {
        public ChapterContentPreview? ExpressionPreview { get; private set; }

        public bool CanPrepareExpression => inner.CanPrepareExpression;

        public ChapterContentPreview PrepareExpression(string expression)
        {
            var preview = inner.PrepareExpression(expression);
            ExpressionPreview = forceNoChange
                ? preview with
                {
                    Candidate = preview.Before,
                    Differences = System.Collections.Immutable.ImmutableArray<ChapterContentDifference>.Empty,
                    Errors = System.Collections.Immutable.ImmutableArray<string>.Empty
                }
                : preview;
            return ExpressionPreview;
        }

        public ChapterContentPreview PrepareTemplateNames(bool autoGenerateNames, bool useTemplateNames) =>
            inner.PrepareTemplateNames(autoGenerateNames, useTemplateNames);

        public ChapterContentPreview PrepareContentOptions() => inner.PrepareContentOptions();

        public ChapterContentPreview PrepareFrameShift(int frames) => inner.PrepareFrameShift(frames);

        public ChapterContentPreview PrepareFrameRateConversion(decimal sourceFps, decimal targetFps) =>
            inner.PrepareFrameRateConversion(sourceFps, targetFps);

        public ValueTask<TransactionOutcome> ApplyAsync(ChapterContentPreview preview, CancellationToken cancellationToken = default) =>
            new(completion?.Task ?? Task.FromException<TransactionOutcome>(new InvalidOperationException("No outcome was configured.")));

        public void Cancel(ChapterContentPreview preview) => inner.Cancel(preview);
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
        public ValueTask ShiftFramesForwardAsync(int frames, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
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

        public void ApplyLoadedSettings(AppSettings settings)
        {
            ApplyLivePreferences(settings);
        }

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

    private sealed class FakeWindowService : IAuxiliaryToolHost
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
