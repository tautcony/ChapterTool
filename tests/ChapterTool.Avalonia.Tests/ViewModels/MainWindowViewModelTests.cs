using System.ComponentModel;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Platform;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.Tests.ViewModels;

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
        Assert.NotNull(vm.ChangeFpsCommand);
        Assert.NotNull(vm.SelectClipCommand);
        Assert.NotNull(vm.CombineCommand);
        Assert.NotNull(vm.EditTimeCommand);
        Assert.NotNull(vm.EditNameCommand);
        Assert.NotNull(vm.EditFrameCommand);
        Assert.NotNull(vm.DeleteCommand);
        Assert.NotNull(vm.InsertCommand);
        Assert.NotNull(vm.PreviewCommand);
        Assert.NotNull(vm.LogCommand);
        Assert.NotNull(vm.SettingsCommand);
        Assert.NotNull(vm.ColorSettingsCommand);
        Assert.NotNull(vm.LanguageCommand);
        Assert.NotNull(vm.ExpressionCommand);
        Assert.NotNull(vm.TemplateNamesCommand);
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
        Assert.Equal("0", vm.Rows[0].FramesInfo);
        Assert.True(vm.Rows[0].IsFrameAccurate);
        Assert.Equal(2, vm.SelectedFrameRateIndex);
        Assert.Equal("Loaded 1 chapters", vm.StatusText);
        Assert.Equal(1, vm.Progress);
    }

    [Fact]
    public async Task LoadAppliesProgressUpdatesBeforeCompletion()
    {
        var load = new FakeLoadService(ImportResult("movie.txt", Info("OGM", "movie.txt", new Chapter(1, TimeSpan.Zero, "Intro"))))
        {
            OnLoad = progress => progress?.Report(new ChapterLoadProgress(0.42, "Status.LoadingSource.Export"))
        };
        var vm = CreateViewModel(load);
        var progressValues = new List<double>();
        var progressStatuses = new List<string>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.Progress))
            {
                progressValues.Add(vm.Progress);
            }
            else if (args.PropertyName == nameof(MainWindowViewModel.StatusText))
            {
                progressStatuses.Add(vm.StatusText);
            }
        };

        await vm.LoadCommand.ExecuteAsync("movie.txt");

        Assert.Contains(progressValues, value => value is > 0 and < 1);
        Assert.Contains("Loading source...", progressStatuses);
        Assert.Contains("Exporting chapter text...", progressStatuses);
        Assert.Equal(1, vm.Progress);
    }

    [Fact]
    public async Task AsyncLoadUpdatesObservableStateThroughViewModel()
    {
        var load = new AsyncLoadService(ImportResult(
            "movie.mpls",
            Info("MPLS", "00001", new Chapter(1, TimeSpan.Zero, "A")),
            Info("MPLS", "00002", new Chapter(1, TimeSpan.FromSeconds(1), "B"))));
        var vm = CreateViewModel(load);
        var rowNotifications = 0;
        var clipNotifications = 0;
        var statusNotifications = new List<string>();
        var progressNotifications = new List<double>();
        vm.Rows.CollectionChanged += (_, _) => rowNotifications++;
        vm.ClipOptions.CollectionChanged += (_, _) => clipNotifications++;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.StatusText))
            {
                statusNotifications.Add(vm.StatusText);
            }
            else if (args.PropertyName == nameof(MainWindowViewModel.Progress))
            {
                progressNotifications.Add(vm.Progress);
            }
        };

        await vm.LoadCommand.ExecuteAsync("movie.mpls");

        Assert.True(load.CompletedAfterAwait);
        Assert.True(rowNotifications > 0);
        Assert.True(clipNotifications > 0);
        Assert.Equal(2, vm.ClipOptions.Count);
        Assert.Equal(0, vm.SelectedClipIndex);
        Assert.Single(vm.Rows);
        Assert.Equal("A", vm.Rows[0].Name);
        Assert.Equal("Loaded 1 chapters", vm.StatusText);
        Assert.Equal(1, vm.Progress);
        Assert.Contains("Parsing chapter text...", statusNotifications);
        Assert.Contains(progressNotifications, value => value is > 0 and < 1);
    }

    [Fact]
    public async Task OlderLoadResultDoesNotOverwriteNewerLoad()
    {
        var load = new ControlledLoadService(new Dictionary<string, ChapterImportResult>(StringComparer.Ordinal)
        {
            ["slow.txt"] = ImportResult("slow.txt", Info("OGM", "slow.txt", new Chapter(1, TimeSpan.Zero, "Slow"))),
            ["fast.txt"] = ImportResult("fast.txt", Info("OGM", "fast.txt", new Chapter(1, TimeSpan.Zero, "Fast")))
        });
        var vm = CreateViewModel(load);

        var slow = vm.LoadCommand.ExecuteAsync("slow.txt").AsTask();
        await load.WaitForRequestAsync("slow.txt");
        var fast = vm.DropPathLoadCommand.ExecuteAsync("fast.txt").AsTask();
        await load.WaitForRequestAsync("fast.txt");

        load.Complete("fast.txt");
        await fast;
        Assert.Equal("fast.txt", vm.CurrentPath);
        Assert.Equal("Fast", vm.Rows.Single().Name);

        load.Complete("slow.txt");
        await slow;

        Assert.Equal("fast.txt", vm.CurrentPath);
        Assert.Equal("Fast", vm.Rows.Single().Name);
    }

    [Fact]
    public async Task OlderAppendResultDoesNotOverwriteNewerLoad()
    {
        var load = new ControlledLoadService(new Dictionary<string, ChapterImportResult>(StringComparer.Ordinal)
        {
            ["base.mpls"] = ImportResult("base.mpls", Info("MPLS", "base", new Chapter(1, TimeSpan.Zero, "Base"))),
            ["append.mpls"] = ImportResult("append.mpls", Info("MPLS", "append", new Chapter(1, TimeSpan.Zero, "Append"))),
            ["new.txt"] = ImportResult("new.txt", Info("OGM", "new.txt", new Chapter(1, TimeSpan.Zero, "New")))
        });
        var vm = CreateViewModel(load);

        var baseLoad = vm.LoadCommand.ExecuteAsync("base.mpls").AsTask();
        await load.WaitForRequestAsync("base.mpls");
        load.Complete("base.mpls");
        await baseLoad;

        var append = vm.AppendMplsCommand.ExecuteAsync("append.mpls").AsTask();
        await load.WaitForRequestAsync("append.mpls");
        var newerLoad = vm.LoadCommand.ExecuteAsync("new.txt").AsTask();
        await load.WaitForRequestAsync("new.txt");

        load.Complete("new.txt");
        await newerLoad;
        Assert.Equal("new.txt", vm.CurrentPath);
        Assert.Equal("New", vm.Rows.Single().Name);

        load.Complete("append.mpls");
        await append;

        Assert.Equal("new.txt", vm.CurrentPath);
        Assert.Equal("New", vm.Rows.Single().Name);
        Assert.False(vm.IsClipCombineChecked);
    }

    [Fact]
    public async Task ClipDisplayOptionsExposeMainContentWithRemarksWithoutChangingSourceOptions()
    {
        var firstInfo = Info("MPLS", "00002", new Chapter(1, TimeSpan.Zero, "A"));
        var secondInfo = Info("MPLS", "00003", new Chapter(1, TimeSpan.Zero, "B"));
        var load = new FakeLoadService(new ChapterImportResult(true, [
            new ChapterInfoGroup("movie.mpls", [
                new ChapterSourceOption("clip-0", "00002__6", firstInfo),
                new ChapterSourceOption("clip-1", "00003__8", secondInfo)
            ])
        ], []));
        var vm = CreateViewModel(load);

        await vm.LoadCommand.ExecuteAsync("movie.mpls");

        Assert.Equal("00002__6", vm.ClipOptions[0].DisplayName);
        Assert.Equal("00002", vm.ClipDisplayOptions[0].MainText);
        Assert.Equal("6 chapters", vm.ClipDisplayOptions[0].RemarkText);
        Assert.Equal("00002（6 chapters）", vm.ClipDisplayOptions[0].DisplayText);

        await vm.SelectClipCommand.ExecuteAsync(1);

        Assert.Equal(1, vm.SelectedClipIndex);
        Assert.Equal("B", vm.Rows[0].Name);
    }

    [Fact]
    public async Task CombineCommandTogglesMplsClipsBetweenCombinedAndSplitOptions()
    {
        var load = new FakeLoadService(ImportResult(
            "movie.mpls",
            Info("MPLS", "00001", new Chapter(1, TimeSpan.Zero, "A"), new Chapter(2, TimeSpan.FromSeconds(10), "B")),
            Info("MPLS", "00002", new Chapter(1, TimeSpan.Zero, "C"), new Chapter(2, TimeSpan.FromSeconds(5), "D"))));
        var vm = CreateViewModel(load);

        await vm.LoadCommand.ExecuteAsync("movie.mpls");
        await vm.CombineCommand.ExecuteAsync();

        Assert.True(vm.IsClipCombineChecked);
        Assert.True(vm.IsClipSelectionVisible);
        Assert.True(vm.CanCombine);
        Assert.Single(vm.ClipOptions);
        Assert.Equal(4, vm.Rows.Count);
        Assert.Equal(["Chapter 01", "Chapter 02", "Chapter 03", "Chapter 04"], vm.Rows.Select(static row => row.Name).ToArray());

        await vm.CombineCommand.ExecuteAsync();

        Assert.False(vm.IsClipCombineChecked);
        Assert.True(vm.IsClipSelectionVisible);
        Assert.Equal(2, vm.ClipOptions.Count);
        Assert.Equal(0, vm.SelectedClipIndex);
        Assert.Equal(["A", "B"], vm.Rows.Select(static row => row.Name).ToArray());
    }

    [Fact]
    public async Task LoadMplsRaisesSelectedClipIndexChangeAfterClipOptionsPopulate()
    {
        var load = new FakeLoadService(ImportResult(
            "movie.mpls",
            Info("MPLS", "00001", new Chapter(1, TimeSpan.Zero, "A")),
            Info("MPLS", "00002", new Chapter(1, TimeSpan.FromSeconds(1), "B"))));
        var vm = CreateViewModel(load);
        var indexNotifications = new List<int>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.SelectedClipIndex))
            {
                indexNotifications.Add(vm.SelectedClipIndex);
            }
        };

        await vm.LoadCommand.ExecuteAsync("movie.mpls");

        Assert.Equal(2, vm.ClipOptions.Count);
        Assert.Equal(0, vm.SelectedClipIndex);
        Assert.NotEmpty(indexNotifications);
        Assert.Equal(0, indexNotifications[^1]);
    }

    [Fact]
    public async Task LoadAfterPreviousLoadRaisesSelectedClipIndexNotification()
    {
        var firstLoad = ImportResult(
            "first.mpls",
            Info("MPLS", "00001", new Chapter(1, TimeSpan.Zero, "A")),
            Info("MPLS", "00002", new Chapter(1, TimeSpan.FromSeconds(1), "B")));
        var secondLoad = ImportResult(
            "second.mpls",
            Info("MPLS", "00010", new Chapter(1, TimeSpan.Zero, "X")),
            Info("MPLS", "00020", new Chapter(1, TimeSpan.FromSeconds(2), "Y")),
            Info("MPLS", "00030", new Chapter(1, TimeSpan.FromSeconds(4), "Z")));
        var load = new FakeLoadService(firstLoad, secondLoad);
        var vm = CreateViewModel(load);

        await vm.LoadCommand.ExecuteAsync("first.mpls");
        await vm.SelectClipCommand.ExecuteAsync(1);
        Assert.Equal(1, vm.SelectedClipIndex);

        var notifications = new List<int>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.SelectedClipIndex))
            {
                notifications.Add(vm.SelectedClipIndex);
            }
        };

        await vm.LoadCommand.ExecuteAsync("second.mpls");

        Assert.Equal(3, vm.ClipOptions.Count);
        Assert.Equal(0, vm.SelectedClipIndex);
        Assert.Contains(0, notifications);
        Assert.Equal(0, notifications[^1]);
    }

    [Fact]
    public async Task ScalarStateChangesRaisePropertyNotifications()
    {
        var vm = CreateViewModel();
        var changed = new List<string?>();
        Assert.IsAssignableFrom<INotifyPropertyChanged>(vm);
        vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        await vm.LoadCommand.ExecuteAsync("movie.txt");
        vm.SaveFormat = ChapterExportFormat.Json;
        vm.XmlLanguage = "jpn";
        vm.AutoGenerateNames = true;
        vm.SetFrameOptions(frameRateIndex: 2, roundFrames: false);

        Assert.Contains(nameof(MainWindowViewModel.CurrentPath), changed);
        Assert.Contains(nameof(MainWindowViewModel.DisplayPath), changed);
        Assert.Contains(nameof(MainWindowViewModel.StatusText), changed);
        Assert.Contains(nameof(MainWindowViewModel.Progress), changed);
        Assert.Contains(nameof(MainWindowViewModel.SaveFormat), changed);
        Assert.Contains(nameof(MainWindowViewModel.XmlLanguage), changed);
        Assert.Contains(nameof(MainWindowViewModel.AutoGenerateNames), changed);
        Assert.Contains(nameof(MainWindowViewModel.RoundFrames), changed);
        Assert.Contains(nameof(MainWindowViewModel.SelectedFrameRateIndex), changed);
        Assert.Contains(nameof(MainWindowViewModel.IsXmlLanguageEnabled), changed);
    }

    [Fact]
    public void XmlLanguageIsEnabledOnlyForXmlExport()
    {
        var vm = CreateViewModel();

        Assert.False(vm.IsXmlLanguageEnabled);

        vm.SaveFormat = ChapterExportFormat.Xml;
        Assert.True(vm.IsXmlLanguageEnabled);

        vm.SaveFormat = ChapterExportFormat.Txt;
        Assert.False(vm.IsXmlLanguageEnabled);
    }

    [Fact]
    public void XmlLanguageOptionsIncludeLegacyAndIsoCodes()
    {
        var vm = CreateViewModel();

        Assert.Equal(["und", "zh", "ja", "en", "jpn"], vm.XmlLanguageOptions.Take(5));
        Assert.Contains("fr", vm.XmlLanguageOptions);
        Assert.Equal(
            vm.XmlLanguageOptions.Count,
            vm.XmlLanguageOptions.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void XmlLanguageDisplayOptionsExposeReadableNamesWithoutChangingCodes()
    {
        var vm = CreateViewModel();

        var index = vm.XmlLanguageOptions.ToList().IndexOf("jpn");
        var option = vm.XmlLanguageDisplayOptions[index];

        Assert.Equal("jpn", option.MainText);
        Assert.Equal("Japanese", option.RemarkText);
        Assert.Equal("jpn（Japanese）", option.DisplayText);

        vm.XmlLanguageIndex = index;

        Assert.Equal("jpn", vm.XmlLanguage);
    }

    [Fact]
    public void XmlLanguageIndexUpdatesSelectedLanguage()
    {
        var vm = CreateViewModel();
        var index = vm.XmlLanguageOptions.ToList().IndexOf("jpn");

        vm.XmlLanguageIndex = index;

        Assert.Equal("jpn", vm.XmlLanguage);
        Assert.Equal(index, vm.XmlLanguageIndex);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void XmlLanguageIndexOutOfRangeDoesNotChangeSelectedLanguage(int index)
    {
        var vm = CreateViewModel();
        vm.XmlLanguage = "und";

        vm.XmlLanguageIndex = index;

        Assert.Equal("und", vm.XmlLanguage);
    }

    [Fact]
    public async Task ChapterNameTemplateReaderDetectsUtf8Bom()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"), "names.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, [0xEF, 0xBB, 0xBF, .. "Opening\nMiddle"u8.ToArray()]);

        try
        {
            var text = await ChapterNameTemplateReader.ReadAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal("Opening\nMiddle", text);
            Assert.DoesNotContain("\uFEFF", text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task LoadRaisesCommandAvailabilityChanges()
    {
        var vm = CreateViewModel();
        var saveChanges = 0;
        var reloadChanges = 0;
        vm.SaveCommand.CanExecuteChanged += (_, _) => saveChanges++;
        vm.ReloadCommand.CanExecuteChanged += (_, _) => reloadChanges++;

        await vm.LoadCommand.ExecuteAsync("movie.txt");

        Assert.True(vm.SaveCommand.CanExecute());
        Assert.True(vm.ReloadCommand.CanExecute());
        Assert.True(saveChanges > 0);
        Assert.True(reloadChanges > 0);
    }

    [Fact]
    public async Task RefreshCommandRecalculatesFramesFromSelectedFrameOptions()
    {
        var info = Info("OGM", "movie.txt", new Chapter(1, TimeSpan.FromSeconds(0.5), "Intro"));
        var load = new FakeLoadService(ImportResult("movie.txt", info));
        var save = new FakeSaveService();
        var vm = CreateViewModel(load, save);

        await vm.LoadCommand.ExecuteAsync("movie.txt");
        vm.SetFrameOptions(frameRateIndex: 3, roundFrames: false);
        await vm.RefreshCommand.ExecuteAsync();
        await vm.SaveDirectoryCommand.ExecuteAsync("out");

        Assert.Equal("12.5", vm.Rows[0].FramesInfo);
        Assert.True(vm.Rows[0].IsFrameNeutral);
        Assert.Equal(3, vm.SelectedFrameRateIndex);
        Assert.NotNull(save.LastInfo);
        Assert.Equal("12.5", save.LastInfo.Chapters[0].FramesInfo);
        Assert.Equal(FrameAccuracy.Neutral, save.LastInfo.Chapters[0].FrameAccuracy);
    }

    [Fact]
    public async Task ConfiguredFrameAccuracyToleranceControlsFrameStylingState()
    {
        var store = new FakeSettingsStore(new AppSettings(FrameAccuracyTolerance: 0.001m));
        var load = new FakeLoadService(ImportResult("movie.txt", Info("OGM", "movie.txt", new Chapter(1, TimeSpan.FromSeconds(1.004), "Intro"))));
        var vm = CreateViewModel(load, appSettingsStore: store);

        await vm.LoadSettingsAsync(TestContext.Current.CancellationToken);
        await vm.LoadCommand.ExecuteAsync("movie.txt");
        vm.SetFrameOptions(frameRateIndex: 3, roundFrames: true);
        await vm.RefreshCommand.ExecuteAsync();

        Assert.Equal("25", vm.Rows[0].FramesInfo);
        Assert.True(vm.Rows[0].IsFrameInexact);

        store.Current = store.Current with { FrameAccuracyTolerance = 0.2m };
        await vm.LoadSettingsAsync(TestContext.Current.CancellationToken);
        await vm.RefreshCommand.ExecuteAsync();

        Assert.True(vm.Rows[0].IsFrameAccurate);
    }

    [Fact]
    public async Task AutoFrameRateRunsDetectionAndUpdatesStatusText()
    {
        var info = Info(
            "OGM",
            "movie.txt",
            new Chapter(1, TimeSpan.Zero, "A"),
            new Chapter(2, TimeSpan.FromMilliseconds(40), "B"),
            new Chapter(3, TimeSpan.FromMilliseconds(80), "C"));
        var load = new FakeLoadService(ImportResult("movie.txt", info));
        var vm = CreateViewModel(load);

        await vm.LoadCommand.ExecuteAsync("movie.txt");
        vm.SetFrameOptions(frameRateIndex: 0, roundFrames: false);
        await vm.RefreshCommand.ExecuteAsync();

        Assert.Equal(0, vm.SelectedFrameRateIndex);
        Assert.Contains("Detected 25000 / 1000", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("High", vm.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManualFrameRateChoiceDoesNotEmitDetectedStatusText()
    {
        var info = Info(
            "OGM",
            "movie.txt",
            new Chapter(1, TimeSpan.Zero, "A"),
            new Chapter(2, TimeSpan.FromMilliseconds(40), "B"));
        var load = new FakeLoadService(ImportResult("movie.txt", info));
        var vm = CreateViewModel(load);

        await vm.LoadCommand.ExecuteAsync("movie.txt");
        vm.SetFrameOptions(frameRateIndex: 3, roundFrames: false);
        await vm.RefreshCommand.ExecuteAsync();

        Assert.Equal(3, vm.SelectedFrameRateIndex);
        Assert.DoesNotContain("Detected", vm.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangeFpsCommandPreservesFramesWhenApplyingSelectedFrameRate()
    {
        var load = new FakeLoadService(ImportResult("movie.txt", Info("OGM", "movie.txt", new Chapter(1, TimeSpan.FromSeconds(10), "A"))));
        var vm = CreateViewModel(load);

        await vm.LoadCommand.ExecuteAsync("movie.txt");
        var targetIndex = new FrameRateService().Options.Single(option => option.Code == "Fps5994").LegacyMplsCode;
        vm.SetFrameOptions(frameRateIndex: targetIndex, roundFrames: true);
        await vm.ChangeFpsCommand.ExecuteAsync();

        Assert.Equal("00:00:04.004", vm.Rows[0].TimeText);
        Assert.Equal("240", vm.Rows[0].FramesInfo);
        Assert.True(vm.Rows[0].IsFrameAccurate);
    }

    [Fact]
    public async Task ChangeFpsCommandLogsSourceAndSelectedTargetFrameRates()
    {
        var log = new ApplicationLogPanelProvider();
        var load = new FakeLoadService(ImportResult("movie.txt", Info("OGM", "movie.txt", new Chapter(1, TimeSpan.FromSeconds(10), "A"))));
        var vm = CreateViewModel(load, logService: log);

        await vm.LoadCommand.ExecuteAsync("movie.txt");
        var targetIndex = new FrameRateService().Options.Single(option => option.Code == "Fps50").LegacyMplsCode;
        vm.SetFrameOptions(frameRateIndex: targetIndex, roundFrames: true);
        await vm.RefreshCommand.ExecuteAsync();
        await vm.ChangeFpsCommand.ExecuteAsync();

        Assert.Equal("00:00:04.800", vm.Rows[0].TimeText);
        Assert.Contains(log.Entries, entry =>
            entry is { MessageKey: "Log.ChangeFps", Arguments: not null }
            && entry.Arguments.TryGetValue("sourceFps", out var sourceFps)
            && entry.Arguments.TryGetValue("targetFps", out var targetFps)
            && string.Equals(sourceFps?.ToString(), "24", StringComparison.Ordinal)
            && string.Equals(targetFps?.ToString(), "50", StringComparison.Ordinal));
        Assert.Contains("Convert to current FPS: source=24, target=50", vm.LogText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveProjectsChaptersAndDelegatesNeutralOptions()
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
        Assert.False(save.LastOptions.AutoGenerateNames);
        Assert.False(save.LastOptions.UseTemplateNames);
        Assert.Equal(0, save.LastOptions.OrderShift);
        Assert.False(save.LastOptions.ApplyExpression);
        Assert.Equal("t + 1", save.LastOptions.Expression);
        Assert.Equal(string.Empty, save.LastOptions.LuaExpressionPresetId);
        Assert.Equal(string.Empty, save.LastOptions.LuaExpressionSourceName);
        Assert.NotNull(save.LastInfo);
        Assert.Equal(3, save.LastInfo.Chapters[0].Number);
        Assert.Equal("Chapter 01", save.LastInfo.Chapters[0].Name);
        Assert.Equal(TimeSpan.FromSeconds(1), save.LastInfo.Chapters[0].Time);
        Assert.Equal("out", save.LastDirectory);
    }

    [Fact]
    public async Task ExpressionAppliesToRowsPreviewAndSavedInfo()
    {
        var save = new FakeSaveService();
        var vm = CreateViewModel(saveService: save);
        await vm.LoadCommand.ExecuteAsync("movie.txt");
        vm.SaveFormat = ChapterExportFormat.Txt;

        vm.ApplyExpression = true;
        vm.Expression = "t + 1";
        vm.LuaExpressionSourceName = "inline";

        Assert.Equal("00:00:01.000", vm.Rows[0].TimeText);
        Assert.Equal("24", vm.Rows[0].FramesInfo);
        Assert.True(vm.Rows[0].IsFrameAccurate);
        Assert.Contains("CHAPTER01=00:00:01.000", vm.BuildPreview(), StringComparison.Ordinal);

        await vm.SaveDirectoryCommand.ExecuteAsync("out");

        Assert.NotNull(save.LastInfo);
        Assert.Equal(TimeSpan.FromSeconds(1), save.LastInfo.Chapters[0].Time);
        Assert.Equal("24", save.LastInfo.Chapters[0].FramesInfo);
        Assert.Equal(FrameAccuracy.Accurate, save.LastInfo.Chapters[0].FrameAccuracy);
        Assert.NotNull(save.LastOptions);
        Assert.False(save.LastOptions.ApplyExpression);
        Assert.Equal("t + 1", save.LastOptions.Expression);
        Assert.Equal("inline", save.LastOptions.LuaExpressionSourceName);
    }


    [Fact]
    public async Task PreviewAndSaveUseSameLuaProjectionForTimesNamesAndNumbers()
    {
        var save = new FakeSaveService();
        var vm = CreateViewModel(saveService: save);
        await vm.LoadCommand.ExecuteAsync("movie.txt");
        vm.SaveFormat = ChapterExportFormat.Txt;
        vm.ApplyExpression = true;
        vm.Expression = "t + 2";
        vm.AutoGenerateNames = true;
        vm.OrderShift = 3;

        var preview = vm.BuildPreview();
        await vm.SaveDirectoryCommand.ExecuteAsync("out");

        Assert.Contains("CHAPTER04=00:00:02.000", preview, StringComparison.Ordinal);
        Assert.Contains("CHAPTER04NAME=Chapter 01", preview, StringComparison.Ordinal);
        Assert.NotNull(save.LastInfo);
        Assert.Equal(4, save.LastInfo.Chapters[0].Number);
        Assert.Equal("Chapter 01", save.LastInfo.Chapters[0].Name);
        Assert.Equal(TimeSpan.FromSeconds(2), save.LastInfo.Chapters[0].Time);
    }

    [Fact]
    public async Task NamingAndOrderOptionsApplyToRowsAndPreview()
    {
        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync("movie.txt");
        vm.SaveFormat = ChapterExportFormat.Txt;

        vm.OrderShift = 2;
        vm.AutoGenerateNames = true;

        Assert.Equal(3, vm.Rows[0].Number);
        Assert.Equal("Chapter 01", vm.Rows[0].Name);
        Assert.Contains("CHAPTER03=00:00:00.000", vm.BuildPreview(), StringComparison.Ordinal);
        Assert.Contains("CHAPTER03NAME=Chapter 01", vm.BuildPreview(), StringComparison.Ordinal);

        vm.AutoGenerateNames = false;
        vm.UseTemplateNames = true;

        Assert.Equal("Chapter 01", vm.Rows[0].Name);

        vm.ChapterNameTemplateText = "Opening";

        Assert.Equal("Opening", vm.Rows[0].Name);
        Assert.Contains("CHAPTER03NAME=Opening", vm.BuildPreview(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NegativeOrderShiftNormalizesToZeroAcrossRowsPreviewAndSave()
    {
        var save = new FakeSaveService();
        var vm = CreateViewModel(saveService: save);
        await vm.LoadCommand.ExecuteAsync("movie.txt");
        vm.SaveFormat = ChapterExportFormat.Txt;

        vm.OrderShift = -2;

        Assert.Equal(1, vm.Rows[0].Number);
        var preview = vm.BuildPreview();
        Assert.Contains("CHAPTER01=00:00:00.000", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("CHAPTER00", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("CHAPTER-01", preview, StringComparison.Ordinal);

        await vm.SaveDirectoryCommand.ExecuteAsync("out");

        Assert.NotNull(save.LastInfo);
        Assert.Equal(1, save.LastInfo.Chapters[0].Number);
        Assert.NotNull(save.LastOptions);
        Assert.Equal(0, save.LastOptions.OrderShift);
    }

    [Fact]
    public async Task OrderShiftUsesOutputChapterOrderAndSkipsSeparators()
    {
        var load = new FakeLoadService(ImportResult(
            "album.cue",
            Info(
                "CUE",
                "album.cue",
                new Chapter(1, TimeSpan.Zero, "A", "0"),
                new Chapter(-1, Chapter.SeparatorTime, ""),
                new Chapter(2, TimeSpan.FromSeconds(7), "B", "168"))));
        var vm = CreateViewModel(load);
        await vm.LoadCommand.ExecuteAsync("album.cue");
        vm.SaveFormat = ChapterExportFormat.Txt;

        vm.OrderShift = 2;

        Assert.Equal([3, 4], vm.Rows.Select(static row => row.Number).ToArray());
        var preview = vm.BuildPreview();
        Assert.Contains("CHAPTER03=00:00:00.000", preview, StringComparison.Ordinal);
        Assert.Contains("CHAPTER04=00:00:07.000", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("CHAPTER-01", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("CHAPTER05", preview, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ZeroOrderShiftUsesFirstChapterNumber()
    {
        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync("movie.txt");
        vm.SaveFormat = ChapterExportFormat.Txt;

        vm.OrderShift = 0;

        Assert.Equal(1, vm.Rows[0].Number);
        Assert.Contains("CHAPTER01=00:00:00.000", vm.BuildPreview(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NegativeExpressionResultNormalizesRowsAndSavedInfoToZero()
    {
        var save = new FakeSaveService();
        var load = new FakeLoadService(ImportResult("movie.txt", Info("OGM", "movie.txt", new Chapter(1, TimeSpan.FromSeconds(10), "Intro", "240"))));
        var vm = CreateViewModel(load, save);
        await vm.LoadCommand.ExecuteAsync("movie.txt");

        vm.ApplyExpression = true;
        vm.Expression = "t - 10000";

        Assert.Equal("00:00:00.000", vm.Rows[0].TimeText);
        Assert.Equal("0", vm.Rows[0].FramesInfo);
        Assert.True(vm.Rows[0].IsFrameAccurate);

        await vm.SaveDirectoryCommand.ExecuteAsync("out");

        Assert.NotNull(save.LastInfo);
        Assert.Equal(TimeSpan.Zero, save.LastInfo.Chapters[0].Time);
        Assert.Equal("0", save.LastInfo.Chapters[0].FramesInfo);
        Assert.Equal(FrameAccuracy.Accurate, save.LastInfo.Chapters[0].FrameAccuracy);
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

        await vm.LoadCommand.ExecuteAsync("movie.txt");
        await vm.PreviewCommand.ExecuteAsync();
        await vm.LogCommand.ExecuteAsync();

        Assert.Equal(["preview", "log"], windows.Opened);
    }

    [Fact]
    public async Task PreviewAndLogUseCurrentChapterState()
    {
        var log = new ApplicationLogPanelProvider();
        var vm = CreateViewModel(logService: log);

        await vm.LoadCommand.ExecuteAsync("movie.txt");
        vm.SaveFormat = ChapterExportFormat.Txt;

        Assert.Contains("CHAPTER01=", vm.BuildPreview(), StringComparison.Ordinal);
        Assert.Contains("Loaded 1 chapters", vm.LogText(), StringComparison.Ordinal);
        Assert.Contains(log.Entries, entry =>
            entry is { Level: LogLevel.Information, MessageKey: "Log.LoadingSource" } &&
            string.Equals(entry.Category, typeof(MainWindowViewModel).FullName, StringComparison.Ordinal));

        vm.ClearLog();
        Assert.Equal(string.Empty, vm.LogText());
    }

    [Fact]
    public async Task PreviewCommandRequiresLoadedChapterState()
    {
        var windows = new FakeWindowService();
        var vm = CreateViewModel(windowService: windows);

        Assert.False(vm.PreviewCommand.CanExecute());
        await vm.PreviewCommand.ExecuteAsync();
        Assert.Empty(windows.Opened);

        await vm.LoadCommand.ExecuteAsync("movie.txt");

        Assert.True(vm.PreviewCommand.CanExecute());
        await vm.PreviewCommand.ExecuteAsync();
        Assert.Equal(["preview"], windows.Opened);
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
        var load = new FakeLoadService(new ChapterImportResult(true, [new ChapterInfoGroup(Path.Combine(root, "movie.mpls"), [option])], []));
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

        await vm.LoadSettingsAsync(TestContext.Current.CancellationToken);
        await vm.LoadCommand.ExecuteAsync("movie.txt");
        await vm.SaveDirectoryCommand.ExecuteAsync("new-out");

        Assert.Equal("new-out", save.LastDirectory);
        Assert.Equal("new-out", store.Current.SavingPath);
    }

    [Fact]
    public async Task FailedSaveDirectoryDoesNotPersistThroughStore()
    {
        var store = new FakeSettingsStore(new AppSettings(SavingPath: "out", Language: "en-US"));
        var save = new FakeSaveService { Result = new ChapterExportResult(false, "", "", []) };
        var vm = CreateViewModel(saveService: save, appSettingsStore: store);

        await vm.LoadSettingsAsync(TestContext.Current.CancellationToken);
        await vm.LoadCommand.ExecuteAsync("movie.txt");
        await vm.SaveDirectoryCommand.ExecuteAsync("bad-out");

        Assert.Equal("bad-out", save.LastDirectory);
        Assert.Equal("out", store.Current.SavingPath);
        Assert.Equal("out", vm.SaveDirectory);
    }

    [Fact]
    public async Task UiLanguagePersistsThroughSettingsStore()
    {
        var store = new FakeSettingsStore(new AppSettings(Language: ""));
        var vm = CreateViewModel(appSettingsStore: store);

        await vm.SaveUiLanguageAsync("en-US", TestContext.Current.CancellationToken);

        Assert.Equal("en-US", vm.UiLanguage);
        Assert.Equal("en-US", store.Current.Language);
    }

    [Fact]
    public async Task BlankUiLanguageFallsBackToSimplifiedChinese()
    {
        var store = new FakeSettingsStore(new AppSettings(Language: ""));
        var localizer = new AppLocalizationManager("en-US");
        var vm = CreateViewModel(appSettingsStore: store, localizer: localizer);

        await vm.LoadSettingsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("zh-CN", vm.UiLanguage);
        Assert.Equal("zh-CN", localizer.CurrentCultureName);
    }

    [Fact]
    public async Task LocalizedStatusAndLogRefreshAfterLanguageSwitch()
    {
        var localizer = new AppLocalizationManager("en-US");
        var log = new ApplicationLogPanelProvider();
        var vm = CreateViewModel(logService: log, localizer: localizer);

        await vm.LoadCommand.ExecuteAsync("movie.txt");

        Assert.Equal("Loaded 1 chapters", vm.StatusText);
        Assert.Contains("Loading source", vm.LogText(), StringComparison.Ordinal);

        localizer.SetCulture("ja-JP");

        Assert.Equal("1 個のチャプターを読み込みました", vm.StatusText);
        Assert.Contains("ソースを読み込み中", vm.LogText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiagnosticLogsCaptureSeverityAndFormatForLogWindow()
    {
        var diagnostic = new ChapterDiagnostic(DiagnosticSeverity.Warning, "PartialParse", "stopped", "line 5", "tail");
        var log = new ApplicationLogPanelProvider();
        var vm = CreateViewModel(new FakeLoadService(ImportResult("movie.txt", Info("OGM", "movie.txt", new Chapter(1, TimeSpan.Zero, "Intro"))) with
        {
            Diagnostics = [diagnostic]
        }), logService: log);

        await vm.LoadCommand.ExecuteAsync("movie.txt");

        var entry = Assert.Single(log.Entries, static item => item.MessageKey == "Log.Diagnostic");
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("PartialParse", entry.Arguments?["code"]);
        Assert.Equal("tail", entry.TechnicalDetail);
        Assert.Contains("Load diagnostic: severity=Warning, code=PartialParse", vm.LogText(), StringComparison.Ordinal);
    }

    private static MainWindowViewModel CreateViewModel(
        IChapterLoadService? loadService = null,
        FakeSaveService? saveService = null,
        FakeWindowService? windowService = null,
        IApplicationLogService? logService = null,
        IShellService? shellService = null,
        ISettingsStore<AppSettings>? appSettingsStore = null,
        IAppLocalizer? localizer = null)
    {
        logService ??= new ApplicationLogPanelProvider();

        return new MainWindowViewModel(
            loadService ?? new FakeLoadService(ImportResult("movie.txt", Info("OGM", "movie.txt", new Chapter(1, TimeSpan.Zero, "Intro")))),
            saveService ?? new FakeSaveService(),
            new ChapterEditingService(new ChapterTimeFormatter()),
            new ChapterSegmentService(),
            windowService ?? new FakeWindowService(),
            new ChapterTimeFormatter(),
            logService,
            TestApplicationLogger.Create<MainWindowViewModel>(logService),
            shellService,
            appSettingsStore,
            frameRateService: null,
            localizer ?? new AppLocalizationManager("en-US"));
    }

    private static ChapterInfo Info(string sourceType, string sourceName, params Chapter[] chapters) =>
        new(sourceName, sourceName, 0, sourceType, 24, chapters.Last().Time, chapters);

    private static ChapterImportResult ImportResult(string path, params ChapterInfo[] infos)
    {
        var options = infos.Select((info, index) => new ChapterSourceOption($"option-{index}", info.SourceName ?? info.Title, info)).ToArray();
        return new ChapterImportResult(true, [new ChapterInfoGroup(path, options)], []);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ChapterTool.Avalonia.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }

    private sealed class FakeLoadService(params ChapterImportResult[] results) : IChapterLoadService
    {
        private readonly Queue<ChapterImportResult> results = new(results);

        public Action<IProgress<ChapterLoadProgress>?>? OnLoad { get; init; }

        public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken)
        {
            return LoadAsync(path, progress: null, cancellationToken);
        }

        public ValueTask<ChapterImportResult> LoadAsync(string path, IProgress<ChapterLoadProgress>? progress, CancellationToken cancellationToken)
        {
            if (results.Count == 0)
            {
                throw new InvalidOperationException("FakeLoadService has no more results queued.");
            }

            OnLoad?.Invoke(progress);
            var result = results.Count == 1 ? results.Peek() : results.Dequeue();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class AsyncLoadService(ChapterImportResult result) : IChapterLoadService
    {
        public bool CompletedAfterAwait { get; private set; }

        public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken)
        {
            return LoadAsync(path, progress: null, cancellationToken);
        }

        public async ValueTask<ChapterImportResult> LoadAsync(string path, IProgress<ChapterLoadProgress>? progress, CancellationToken cancellationToken)
        {
            await Task.Yield();
            progress?.Report(new ChapterLoadProgress(0.25, "Status.LoadingSource.Parse"));
            CompletedAfterAwait = true;
            return result;
        }
    }

    private sealed class ControlledLoadService(IReadOnlyDictionary<string, ChapterImportResult> results) : IChapterLoadService
    {
        private readonly Dictionary<string, TaskCompletionSource> started = [];
        private readonly Dictionary<string, TaskCompletionSource<ChapterImportResult>> completions = [];

        public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken)
        {
            return LoadAsync(path, progress: null, cancellationToken);
        }

        public async ValueTask<ChapterImportResult> LoadAsync(string path, IProgress<ChapterLoadProgress>? progress, CancellationToken cancellationToken)
        {
            var startedSource = SourceFor(started, path);
            var completion = CompletionFor(path);
            startedSource.TrySetResult();
            progress?.Report(new ChapterLoadProgress(0.25, "Status.LoadingSource.Parse"));
            using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            return await completion.Task;
        }

        public Task WaitForRequestAsync(string path) => SourceFor(started, path).Task;

        public void Complete(string path)
        {
            CompletionFor(path).TrySetResult(results[path]);
        }

        private TaskCompletionSource<ChapterImportResult> CompletionFor(string path)
        {
            if (!completions.TryGetValue(path, out var source))
            {
                source = new TaskCompletionSource<ChapterImportResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                completions[path] = source;
            }

            return source;
        }

        private static TaskCompletionSource SourceFor(Dictionary<string, TaskCompletionSource> sources, string path)
        {
            if (!sources.TryGetValue(path, out var source))
            {
                source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                sources[path] = source;
            }

            return source;
        }
    }

    private sealed class FakeSaveService : IChapterSaveService
    {
        public ChapterInfo? LastInfo { get; private set; }
        public ChapterExportOptions? LastOptions { get; private set; }
        public string? LastDirectory { get; private set; }
        public ChapterExportResult Result { get; init; } = new(true, "ok", ".txt", []);

        public ValueTask<ChapterExportResult> SaveAsync(ChapterInfo info, ChapterExportOptions options, string? directory, CancellationToken cancellationToken)
        {
            LastInfo = info;
            LastOptions = options;
            LastDirectory = directory;
            return ValueTask.FromResult(Result);
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
        public List<string> RevealedInFolder { get; } = [];
        public List<string> TerminalsOpened { get; } = [];

        public ValueTask OpenAsync(string target, CancellationToken cancellationToken)
        {
            Opened.Add(target);
            return ValueTask.CompletedTask;
        }

        public ValueTask RevealInFolderAsync(string filePath, CancellationToken cancellationToken)
        {
            RevealedInFolder.Add(filePath);
            return ValueTask.CompletedTask;
        }

        public ValueTask OpenTerminalAsync(string directoryPath, CancellationToken cancellationToken)
        {
            TerminalsOpened.Add(directoryPath);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeSettingsStore(AppSettings initial) : ISettingsStore<AppSettings>
    {
        public AppSettings Current { get; set; } = initial;

        public ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Current);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            Current = settings;
            return ValueTask.CompletedTask;
        }
    }
}
