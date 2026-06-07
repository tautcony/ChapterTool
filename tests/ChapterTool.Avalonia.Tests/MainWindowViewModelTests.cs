using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Platform;

namespace ChapterTool.Avalonia.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void StartsWithoutSource()
    {
        var vm = CreateViewModel();

        Assert.Equal(string.Empty, vm.CurrentPath);
        Assert.Empty(vm.Rows);
        Assert.False(vm.IsClipSelectionVisible);
        Assert.False(vm.IsAdvancedPanelExpanded);
        Assert.Equal(ChapterExportFormat.Txt, vm.SaveFormat);
        Assert.False(vm.SaveCommand.CanExecute());
    }

    [Fact]
    public void ConstructsDocumentedCommands()
    {
        var vm = CreateViewModel();

        Assert.NotNull(vm.LoadCommand);
        Assert.NotNull(vm.ReloadCommand);
        Assert.NotNull(vm.AppendMplsCommand);
        Assert.NotNull(vm.DropPathLoadCommand);
        Assert.NotNull(vm.SaveCommand);
        Assert.NotNull(vm.SaveDirectoryCommand);
        Assert.NotNull(vm.RefreshCommand);
        Assert.NotNull(vm.SelectClipCommand);
        Assert.NotNull(vm.CombineCommand);
        Assert.NotNull(vm.EditTimeCommand);
        Assert.NotNull(vm.EditNameCommand);
        Assert.NotNull(vm.EditFrameCommand);
        Assert.NotNull(vm.DeleteCommand);
        Assert.NotNull(vm.InsertCommand);
        Assert.NotNull(vm.PreviewCommand);
        Assert.NotNull(vm.LogCommand);
        Assert.NotNull(vm.ColorSettingsCommand);
        Assert.NotNull(vm.LanguageCommand);
        Assert.NotNull(vm.ExpressionCommand);
        Assert.NotNull(vm.TemplateNamesCommand);
        Assert.NotNull(vm.FileAssociationCommand);
        Assert.NotNull(vm.ZonesCommand);
        Assert.NotNull(vm.ForwardShiftCommand);
        Assert.NotNull(vm.OpenRelatedMediaCommand);
    }

    [Fact]
    public async Task LoadUpdatesStateAndClipSelection()
    {
        var load = new FakeLoadService(ImportResult("movie.mpls", Info("MPLS", "00001", new Chapter(1, TimeSpan.Zero, "A")), Info("MPLS", "00002", new Chapter(1, TimeSpan.FromSeconds(1), "B"))));
        var vm = CreateViewModel(load);

        await vm.LoadCommand.ExecuteAsync("movie.mpls");

        Assert.Equal("movie.mpls", vm.CurrentPath);
        Assert.Equal("movie.mpls", vm.DisplayPath);
        Assert.True(vm.IsClipSelectionVisible);
        Assert.Single(vm.Rows);
        Assert.Equal("0 K", vm.Rows[0].FramesInfo);
        Assert.Equal(1, vm.SelectedFrameRateIndex);
        Assert.Equal("Loaded 1 chapters", vm.StatusText);
        Assert.Equal(1, vm.Progress);
    }

    [Fact]
    public async Task RefreshCommandRecalculatesFramesFromSelectedFrameOptions()
    {
        var info = Info("OGM", "movie.txt", new Chapter(1, TimeSpan.FromSeconds(0.5), "Intro"));
        var load = new FakeLoadService(ImportResult("movie.txt", info));
        var save = new FakeSaveService();
        var vm = CreateViewModel(load, save);

        await vm.LoadCommand.ExecuteAsync("movie.txt");
        vm.SetFrameOptions(frameRateIndex: 2, roundFrames: false);
        await vm.RefreshCommand.ExecuteAsync();
        await vm.SaveDirectoryCommand.ExecuteAsync("out");

        Assert.Equal("12.5", vm.Rows[0].FramesInfo);
        Assert.Equal(2, vm.SelectedFrameRateIndex);
        Assert.NotNull(save.LastInfo);
        Assert.Equal("12.5", save.LastInfo.Chapters[0].FramesInfo);
    }

    [Fact]
    public async Task SaveDelegatesOptions()
    {
        var save = new FakeSaveService();
        var vm = CreateViewModel(saveService: save);
        await vm.LoadCommand.ExecuteAsync("movie.txt");
        vm.SaveFormat = ChapterExportFormat.Cue;
        vm.XmlLanguage = "jpn";
        vm.AutoGenerateNames = true;
        vm.OrderShift = 2;
        vm.ApplyExpression = true;
        vm.Expression = "t + 1";

        await vm.SaveDirectoryCommand.ExecuteAsync("out");

        Assert.NotNull(save.LastOptions);
        Assert.Equal(ChapterExportFormat.Cue, save.LastOptions.Format);
        Assert.Equal("jpn", save.LastOptions.XmlLanguage);
        Assert.True(save.LastOptions.AutoGenerateNames);
        Assert.Equal(2, save.LastOptions.OrderShift);
        Assert.True(save.LastOptions.ApplyExpression);
        Assert.Equal("t + 1", save.LastOptions.Expression);
        Assert.Equal("out", save.LastDirectory);
    }

    [Fact]
    public async Task ShortcutsRouteToCommandsAndClipSelection()
    {
        var load = new FakeLoadService(ImportResult("movie.mpls", Info("MPLS", "00001", new Chapter(1, TimeSpan.Zero, "A")), Info("MPLS", "00002", new Chapter(1, TimeSpan.FromSeconds(1), "B"))));
        var save = new FakeSaveService();
        var vm = CreateViewModel(load, save);
        await vm.LoadCommand.ExecuteAsync("movie.mpls");
        var router = new ShortcutRouter(vm);

        await router.RouteAsync("Ctrl+2");
        await router.RouteAsync("Ctrl+S");

        Assert.Equal(1, vm.SelectedClipIndex);
        Assert.NotNull(save.LastOptions);
    }

    [Fact]
    public async Task GridCommandsEditDeleteInsertAndRefreshRows()
    {
        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync("movie.txt");

        await vm.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "Renamed"));
        await vm.InsertCommand.ExecuteAsync(1);
        await vm.DeleteCommand.ExecuteAsync(new HashSet<int> { 1 });

        Assert.Equal("Renamed", vm.Rows[0].Name);
        Assert.Single(vm.Rows);
    }

    [Fact]
    public async Task AuxiliaryWindowCommandsUseWindowService()
    {
        var windows = new FakeWindowService();
        var vm = CreateViewModel(windowService: windows);

        await vm.PreviewCommand.ExecuteAsync();
        await vm.LogCommand.ExecuteAsync();

        Assert.Equal(["preview", "log"], windows.Opened);
    }

    [Fact]
    public async Task PreviewAndLogUseCurrentChapterState()
    {
        var log = new InMemoryApplicationLogService();
        var vm = CreateViewModel(logService: log);

        await vm.LoadCommand.ExecuteAsync("movie.txt");
        vm.SaveFormat = ChapterExportFormat.Txt;

        Assert.Contains("CHAPTER01=", vm.BuildPreview(), StringComparison.Ordinal);
        Assert.Contains("Loaded 1 chapters", vm.LogText(), StringComparison.Ordinal);

        vm.ClearLog();
        Assert.Equal(string.Empty, vm.LogText());
    }

    [Fact]
    public async Task OpenRelatedMediaUsesShellServiceWhenReferenceExists()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var media = Path.Combine(root, "movie.m2ts");
        await File.WriteAllBytesAsync(media, [0]);
        var shell = new FakeShellService();
        var info = Info("MPLS", "movie", new Chapter(1, TimeSpan.Zero, "A"));
        var option = new ChapterSourceOption("clip-0", "movie__1", info, MediaReferences: [new SourceMediaReference("movie.m2ts", "movie.m2ts")]);
        var load = new FakeLoadService(new ChapterImportResult(true, [new ChapterInfoGroup(Path.Combine(root, "movie.mpls"), [option], 0)], []));
        var vm = CreateViewModel(load, shellService: shell);

        try
        {
            await vm.LoadCommand.ExecuteAsync(Path.Combine(root, "movie.mpls"));
            await vm.OpenRelatedMediaCommand.ExecuteAsync();

            Assert.Equal(media, shell.Opened.Single());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ZonesAndForwardShiftUseEditingServices()
    {
        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync("movie.txt");
        await vm.InsertCommand.ExecuteAsync(1);
        await vm.EditTimeCommand.ExecuteAsync(new ChapterCellEdit(1, "00:00:10.000"));
        vm.UpdateSelectedRows(new HashSet<int> { 0 });

        var zones = vm.CreateZonesText();
        vm.ShiftFramesForward(24);

        Assert.StartsWith("--zones ", zones, StringComparison.Ordinal);
        Assert.Single(vm.Rows);
        Assert.Equal("00:00:09.000", vm.Rows[0].TimeText);
    }

    [Fact]
    public async Task SettingsLoadAndSaveDirectoryPersistThroughStore()
    {
        var store = new FakeSettingsStore(new AppSettings(SavingPath: "out", Language: "en-US"));
        var save = new FakeSaveService();
        var vm = CreateViewModel(saveService: save, appSettingsStore: store);

        await vm.LoadSettingsAsync(CancellationToken.None);
        await vm.LoadCommand.ExecuteAsync("movie.txt");
        await vm.SaveDirectoryCommand.ExecuteAsync("new-out");

        Assert.Equal("new-out", save.LastDirectory);
        Assert.Equal("new-out", store.Current.SavingPath);
    }

    [Fact]
    public async Task UiLanguagePersistsThroughSettingsStore()
    {
        var store = new FakeSettingsStore(new AppSettings(Language: ""));
        var vm = CreateViewModel(appSettingsStore: store);

        await vm.SaveUiLanguageAsync("en-US", CancellationToken.None);

        Assert.Equal("en-US", vm.UiLanguage);
        Assert.Equal("en-US", store.Current.Language);
    }

    [Fact]
    public void AvaloniaViewModelsDoNotReferenceWinForms()
    {
        var root = RepositoryRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "ChapterTool.Avalonia"), "*.cs", SearchOption.AllDirectories);
        var banned = new[] { "DataGridView", "ToolStrip", "MessageBox", "Application.DoEvents", "System.Windows.Forms" };

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain(banned, token => text.Contains(token, StringComparison.Ordinal));
        }
    }

    private static MainWindowViewModel CreateViewModel(
        FakeLoadService? loadService = null,
        FakeSaveService? saveService = null,
        FakeWindowService? windowService = null,
        IApplicationLogService? logService = null,
        IShellService? shellService = null,
        ISettingsStore<AppSettings>? appSettingsStore = null) =>
        new(
            loadService ?? new FakeLoadService(ImportResult("movie.txt", Info("OGM", "movie.txt", new Chapter(1, TimeSpan.Zero, "Intro")))),
            saveService ?? new FakeSaveService(),
            new ChapterEditingService(new ChapterTimeFormatter()),
            new ChapterSegmentService(),
            windowService ?? new FakeWindowService(),
            new ChapterTimeFormatter(),
            logService,
            shellService,
            appSettingsStore);

    private static ChapterInfo Info(string sourceType, string sourceName, params Chapter[] chapters) =>
        new(sourceName, sourceName, 0, sourceType, 24, chapters.Last().Time, chapters);

    private static ChapterImportResult ImportResult(string path, params ChapterInfo[] infos)
    {
        var options = infos.Select((info, index) => new ChapterSourceOption($"option-{index}", info.SourceName ?? info.Title, info)).ToArray();
        return new ChapterImportResult(true, [new ChapterInfoGroup(path, options, 0)], Array.Empty<ChapterDiagnostic>());
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Time_Shift.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }

    private sealed class FakeLoadService(ChapterImportResult result) : IChapterLoadService
    {
        public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(result);
    }

    private sealed class FakeSaveService : IChapterSaveService
    {
        public ChapterInfo? LastInfo { get; private set; }
        public ChapterExportOptions? LastOptions { get; private set; }
        public string? LastDirectory { get; private set; }

        public ValueTask<ChapterExportResult> SaveAsync(ChapterInfo info, ChapterExportOptions options, string? directory, CancellationToken cancellationToken)
        {
            LastInfo = info;
            LastOptions = options;
            LastDirectory = directory;
            return ValueTask.FromResult(new ChapterExportResult(true, "ok", ".txt", Array.Empty<ChapterDiagnostic>()));
        }
    }

    private sealed class FakeWindowService : IWindowService
    {
        public List<string> Opened { get; } = [];

        public ValueTask ShowAsync(string windowId, object? parameter, CancellationToken cancellationToken)
        {
            Opened.Add(windowId);
            return ValueTask.CompletedTask;
        }

        public ValueTask HideAsync(string windowId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class FakeShellService : IShellService
    {
        public List<string> Opened { get; } = [];

        public ValueTask OpenAsync(string target, CancellationToken cancellationToken)
        {
            Opened.Add(target);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeSettingsStore(AppSettings initial) : ISettingsStore<AppSettings>
    {
        public AppSettings Current { get; private set; } = initial;

        public ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Current);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            Current = settings;
            return ValueTask.CompletedTask;
        }
    }
}
