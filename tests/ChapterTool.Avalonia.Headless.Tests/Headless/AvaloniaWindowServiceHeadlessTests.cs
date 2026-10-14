using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Avalonia.UI.Views.Tools;
using ChapterTool.Contracts.Configuration;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class AvaloniaWindowServiceHeadlessTests
{
    [AvaloniaFact]
    public async Task Typed_host_uses_the_injected_catalog_and_reuses_custom_content()
    {
        using var host = new MainWindowHeadlessTestHost();
        var state = new DisposableState();
        var descriptor = new ToolDescriptor(
            new ToolId("custom-tool"),
            "Tool.Custom.Title",
            new ToolSizeConstraints(),
            ToolRefreshPolicy.Reuse,
            _ => new Border { DataContext = state });
        using var service = new AvaloniaWindowService(
            host.Localizer,
            host.SettingsStore,
            new FakeThemeApplicationService(),
            _ => host.SettingsPickerService,
            externalToolLocator: new UnavailableExternalToolLocator(),
            settingsCloseConfirmationService: new FakeSettingsCloseConfirmationService(SettingsCloseAction.Cancel),
            shellService: host.ShellService,
            fontFamilyCatalog: host.FontFamilyCatalog,
            fontApplicationService: host.FontApplicationService,
            settingsDirectory: Path.GetTempPath(),
            expressionAuthoringService: host.ViewModel.ExpressionAuthoringService,
            clipboardServiceFactory: _ => new UnavailableClipboardService(),
            toolCatalog: new ToolCatalog([descriptor]));
        var request = new AuxiliaryToolRequest(
            host.ViewModel.ToolSession,
            host.Localizer,
            Capabilities: host.ViewModel.Capabilities);

        var first = await service.OpenAsync("CUSTOM-TOOL", request, TestContext.Current.CancellationToken);
        var firstWindow = FindWindow(service, "custom-tool");
        var second = await service.OpenAsync("custom-tool", request, TestContext.Current.CancellationToken);

        Assert.Equal(AuxiliaryToolResultKind.Opened, first.Kind);
        Assert.Equal(AuxiliaryToolResultKind.Activated, second.Kind);
        Assert.Same(firstWindow, FindWindow(service, "custom-tool"));

        await service.CloseAsync("custom-tool", TestContext.Current.CancellationToken);
        await DrainUiAsync();

        Assert.False(firstWindow.IsVisible);
        Assert.True(state.IsDisposed);
    }

    [AvaloniaFact]
    public async Task History_tool_reuses_the_session_dialog_and_tracks_branch_navigation()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "First branch"));
        var firstNode = Assert.Single(host.ViewModel.HistoryEntries, entry => entry.IsCurrent);
        await host.ViewModel.UndoCommand.ExecuteAsync();
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "Alternate branch"));
        for (var index = 0; index < 48; index++)
        {
            await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, $"Long history value {index} {new string('x', 180)}"));
        }
        using var service = CreateService(host, new FakeSettingsCloseConfirmationService(SettingsCloseAction.Cancel));
        var request = new AuxiliaryToolRequest(host.ViewModel.ToolSession, host.Localizer, Capabilities: host.ViewModel.Capabilities, HostWindow: host.Window);
        var returnFocus = Assert.IsType<Button>(host.MainView.FindControl<Button>("HistoryButton"));
        Assert.True(returnFocus.Focus());

        var opened = await service.OpenAsync(ToolIds.History, request, TestContext.Current.CancellationToken);
        var window = FindWindow(service, ToolIds.History.Value);
        var activated = await service.OpenAsync(ToolIds.History, request, TestContext.Current.CancellationToken);
        var viewModel = Assert.IsType<HistoryToolViewModel>(Assert.IsAssignableFrom<Control>(window.Content).DataContext);
        var view = Assert.IsType<HistoryToolView>(window.Content);
        await DrainUiAsync();
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

        Assert.Equal(AuxiliaryToolResultKind.Opened, opened.Kind);
        Assert.Equal(AuxiliaryToolResultKind.Activated, activated.Kind);
        Assert.Same(window, FindWindow(service, ToolIds.History.Value));
        Assert.Equal(520, window.MinWidth);
        Assert.Equal(420, window.MinHeight);
        var currentNode = Assert.Single(viewModel.Entries, entry => entry.IsCurrent);
        Assert.NotEqual(firstNode.Id, currentNode.Id);
        Assert.Contains(viewModel.Entries, entry => entry.ParentId is not null);
        var originalEntry = viewModel.Entries.SingleOrDefault(entry => entry.Id == firstNode.Id)
            ?? throw new InvalidOperationException($"Missing first branch {firstNode.Id}; visible={string.Join(";", viewModel.Entries.Select(entry => $"{entry.Id}:{entry.Description}"))}");
        var rootId = viewModel.Entries.Single(entry => entry.ParentId is null).Id;
        var alternateEntry = viewModel.Entries.Single(entry => entry.ParentId == rootId && entry.Id != firstNode.Id);
        Assert.Contains(viewModel.TreeRoots, root => root.Children.Any(child => child.Id == firstNode.Id)
            && root.Children.Any(child => child.Id == alternateEntry.Id));
        var historyList = view.FindControl<TreeView>("HistoryEntries");
        Assert.NotNull(historyList);
        Assert.True(historyList.IsVisible && historyList.IsEnabled);
        Assert.True(historyList.Bounds.Width > 0 && historyList.Bounds.Height > 0);

        foreach (var entry in viewModel.Entries)
        {
            entry.IsExpanded = true;
        }
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
        var wideScroll = Assert.Single(historyList.GetVisualDescendants().OfType<ScrollViewer>());
        wideScroll.Offset = new Vector(wideScroll.Offset.X, 320);
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
        Assert.True(wideScroll.Offset.Y > 0);
        var wideScrollAnchor = wideScroll.Offset.Y;
        var retainedSelectionId = viewModel.SelectedEntry!.Id;

        window.Width = 520;
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
        Assert.True(viewModel.IsNarrowLayout);
        var narrowHistory = Assert.IsType<TreeView>(view.FindControl<TreeView>("NarrowHistoryEntries"));
        Assert.True(narrowHistory.IsVisible);
        Assert.Equal(retainedSelectionId, Assert.IsType<HistoryEntryViewModel>(narrowHistory.SelectedItem).Id);
        var narrowCurrentRow = Assert.Single(narrowHistory.GetVisualDescendants().OfType<TreeViewItem>(), item =>
            item.DataContext is HistoryEntryViewModel entry && entry.Id == retainedSelectionId);
        var narrowCurrentOrigin = narrowCurrentRow.TranslatePoint(new Point(0, 0), narrowHistory);
        Assert.NotNull(narrowCurrentOrigin);
        Assert.InRange(narrowCurrentOrigin.Value.Y, 0, narrowHistory.Bounds.Height);
        Assert.True(narrowCurrentOrigin.Value.Y + narrowCurrentRow.Bounds.Height <= narrowHistory.Bounds.Height + 2,
            $"Selected row origin={narrowCurrentOrigin.Value}, row={narrowCurrentRow.Bounds}, viewport={narrowHistory.Bounds}, scroll={Assert.Single(narrowHistory.GetVisualDescendants().OfType<ScrollViewer>()).Offset}.");

        window.Width = 1000;
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
        Assert.False(viewModel.IsNarrowLayout);
        Assert.Equal(wideScrollAnchor, wideScroll.Offset.Y);
        Assert.Equal(retainedSelectionId, Assert.IsType<HistoryEntryViewModel>(historyList.SelectedItem).Id);

        var rootContainer = Assert.IsType<TreeViewItem>(historyList.ContainerFromIndex(0));
        var alternateIndex = viewModel.TreeRoots[0].Children.IndexOf(alternateEntry);
        var alternateContainer = Assert.IsType<TreeViewItem>(rootContainer.ContainerFromIndex(alternateIndex));
        var initialSelectedId = viewModel.SelectedEntry!.Id;
        var currentContainer = Assert.Single(historyList.GetVisualDescendants().OfType<TreeViewItem>(), item => item.DataContext is HistoryEntryViewModel entry && entry.Id == currentNode.Id);
        Assert.Contains(currentContainer.GetVisualDescendants().OfType<Control>(), control =>
            AutomationProperties.GetName(control) == viewModel.Entries.Single(entry => entry.Id == currentNode.Id).Title);
        Assert.True(currentContainer.Focus());
        window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, string.Empty);
        await DrainUiAsync();
        Assert.NotEqual(initialSelectedId, viewModel.SelectedEntry!.Id);
        historyList.SelectedItem = alternateEntry;
        await DrainUiAsync();
        await viewModel.RetryDetailsAsync();
        Assert.True(alternateEntry.Id == viewModel.SelectedEntry!.Id, $"selected={viewModel.SelectedEntry.Id} ({viewModel.SelectedEntry.Description}) expected={alternateEntry.Id} ({alternateEntry.Description}) tree={historyList.SelectedItem}");
        Assert.False(viewModel.SelectedEntry.IsCurrent);
        Assert.Equal(alternateEntry.Id, viewModel.Details!.NodeId);
        rootContainer.IsExpanded = false;
        await DrainUiAsync();
        Assert.Equal(alternateEntry.Id, Assert.IsType<HistoryEntryViewModel>(historyList.SelectedItem).Id);
        Assert.Equal(alternateEntry.Id, viewModel.Details!.NodeId);
        rootContainer.IsExpanded = true;
        await DrainUiAsync();
        Assert.Equal(currentNode.Id, viewModel.Entries.Single(entry => entry.IsCurrent).Id);

        host.ViewModel.Expression = "t + 1";
        host.ViewModel.RefreshExpressionPreviewNow();
        Assert.True(host.ViewModel.CanApplyContentPreview);
        Assert.Equal(alternateEntry.Id, Assert.IsType<HistoryEntryViewModel>(historyList.SelectedItem).Id);
        Assert.Equal(alternateEntry.Id, viewModel.Details!.NodeId);
        rootContainer.IsExpanded = false;
        await DrainUiAsync();
        Assert.Equal(alternateEntry.Id, Assert.IsType<HistoryEntryViewModel>(historyList.SelectedItem).Id);
        Assert.Equal(alternateEntry.Id, viewModel.Details!.NodeId);
        rootContainer.IsExpanded = true;
        await DrainUiAsync();
        var beforeInspection = host.ViewModel.Rows[0].Name;
        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == firstNode.Id);
        await viewModel.RetryDetailsAsync();
        await DrainUiAsync();
        Assert.Equal(beforeInspection, host.ViewModel.Rows[0].Name);
        Assert.True(host.ViewModel.CanApplyContentPreview);
        Assert.NotNull(viewModel.Details);
        await viewModel.RestoreCommand.ExecuteAsync(firstNode.Id);
        await DrainUiAsync();
        Assert.Equal("First branch", host.ViewModel.Rows[0].Name);
        Assert.False(host.ViewModel.CanApplyContentPreview);
        Assert.Contains(viewModel.Entries, entry => entry.IsCurrent && entry.Id == firstNode.Id);

        var restoredContainer = Assert.Single(historyList.GetVisualDescendants().OfType<TreeViewItem>(), item => item.DataContext is HistoryEntryViewModel entry && entry.Id == firstNode.Id);
        Assert.True(restoredContainer.Focus());
        Assert.True(historyList.IsKeyboardFocusWithin);
        historyList.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.Enter,
            Source = historyList
        });
        await DrainUiAsync();
        Assert.True(view.FindControl<Control>("HistoryDetails")!.IsKeyboardFocusWithin);

        view.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.Escape,
            Source = view
        });
        await DrainUiAsync();
        Assert.Null(window.Content);
        Assert.Same(returnFocus, TopLevel.GetTopLevel(host.Window)?.FocusManager.GetFocusedElement());
        Assert.Contains(host.ViewModel.HistoryEntries, entry => entry.IsCurrent && entry.Id == firstNode.Id);
        await service.OpenAsync(ToolIds.History, request, TestContext.Current.CancellationToken);
        var reopenedWindow = FindWindow(service, ToolIds.History.Value);
        var reopenedViewModel = Assert.IsType<HistoryToolViewModel>(Assert.IsAssignableFrom<Control>(reopenedWindow.Content).DataContext);
        Assert.Contains(reopenedViewModel.Entries, entry => entry.IsCurrent && entry.Id == firstNode.Id);
        await reopenedViewModel.CloseCommand.ExecuteAsync();
        await DrainUiAsync();
        Assert.Null(reopenedWindow.Content);
    }

    [AvaloniaFact]
    public async Task Settings_close_cancel_keeps_window_open_and_live_changes()
    {
        using var host = new MainWindowHeadlessTestHost(
            appSettings: new AppSettings(Language: "en-US", SavingPath: "saved"),
            themeSettings: new ThemeSettings("solarized-light"));
        var confirmation = new FakeSettingsCloseConfirmationService(SettingsCloseAction.Cancel);
        using var service = CreateService(host, confirmation);
        await OpenSettingsAsync(service, host);
        var window = SettingsWindow(service);
        var settings = SettingsViewModel(window);

        settings.SaveDirectory = "live";
        SelectPreset(settings, "ayu-dark");
        Assert.True(settings.HasUnsavedChanges);
        window.Close();
        await DrainUiAsync();

        try
        {
            Assert.Equal(1, confirmation.Calls);
            Assert.True(window.IsVisible);
            Assert.Equal(Path.GetFullPath("live"), host.ViewModel.SaveDirectory);
            Assert.Equal("saved", host.SettingsStore.Current.Application.SavingPath);
            Assert.Equal("solarized-light", host.SettingsStore.Current.Theme.PresetId);
            Assert.Equal("ayu-dark", settings.Appearance.SelectedThemePreset.Id);
        }
        finally
        {
            settings.DiscardUnsavedChanges();
            window.Close();
            await DrainUiAsync();
        }
    }

    [AvaloniaFact]
    public async Task Settings_close_discard_restores_saved_state_and_closes()
    {
        using var host = new MainWindowHeadlessTestHost(
            appSettings: new AppSettings(Language: "en-US", SavingPath: "saved"),
            themeSettings: new ThemeSettings("solarized-light"));
        var confirmation = new FakeSettingsCloseConfirmationService(SettingsCloseAction.Discard);
        using var service = CreateService(host, confirmation);
        await OpenSettingsAsync(service, host);
        var window = SettingsWindow(service);
        var settings = SettingsViewModel(window);

        settings.SaveDirectory = "live";
        SelectPreset(settings, "ayu-dark");
        window.Close();
        await DrainUiAsync();

        Assert.Equal(1, confirmation.Calls);
        Assert.False(window.IsVisible);
        Assert.Equal(Path.GetFullPath("saved"), host.ViewModel.SaveDirectory);
        Assert.Equal("saved", host.SettingsStore.Current.Application.SavingPath);
        Assert.Equal("solarized-light", host.SettingsStore.Current.Theme.PresetId);
        Assert.Equal("solarized-light", settings.Appearance.SelectedThemePreset.Id);
    }

    [AvaloniaFact]
    public async Task Settings_close_save_persists_state_and_closes()
    {
        using var host = new MainWindowHeadlessTestHost(
            appSettings: new AppSettings(Language: "en-US", SavingPath: "saved"),
            themeSettings: new ThemeSettings("solarized-light"));
        var confirmation = new FakeSettingsCloseConfirmationService(SettingsCloseAction.Save);
        using var service = CreateService(host, confirmation);
        await OpenSettingsAsync(service, host);
        var window = SettingsWindow(service);
        var settings = SettingsViewModel(window);

        settings.SaveDirectory = "live";
        SelectPreset(settings, "ayu-dark");
        window.Close();
        await DrainUiAsync();

        Assert.Equal(1, confirmation.Calls);
        Assert.False(window.IsVisible);
        Assert.Equal(Path.GetFullPath("live"), host.ViewModel.SaveDirectory);
        Assert.Equal(Path.GetFullPath("live"), host.SettingsStore.Current.Application.SavingPath);
        Assert.Equal("ayu-dark", host.SettingsStore.Current.Theme.PresetId);
    }

    [AvaloniaFact]
    public async Task Settings_close_without_changes_does_not_prompt()
    {
        using var host = new MainWindowHeadlessTestHost(appSettings: new AppSettings(Language: "en-US", SavingPath: "saved"));
        var confirmation = new FakeSettingsCloseConfirmationService(SettingsCloseAction.Cancel);
        using var service = CreateService(host, confirmation);
        await OpenSettingsAsync(service, host);
        var window = SettingsWindow(service);

        window.Close();
        await DrainUiAsync();

        Assert.Equal(0, confirmation.Calls);
        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public async Task Settings_close_disposes_localization_subscription()
    {
        using var host = new MainWindowHeadlessTestHost(appSettings: new AppSettings(Language: "en-US", SavingPath: "saved"));
        using var service = CreateService(host, new FakeSettingsCloseConfirmationService(SettingsCloseAction.Cancel));
        await OpenSettingsAsync(service, host);
        var window = SettingsWindow(service);
        var settings = SettingsViewModel(window);
        var notifications = 0;
        settings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsToolViewModel.XmlLanguageDisplayOptions))
            {
                notifications++;
            }
        };

        window.Close();
        await DrainUiAsync();
        host.Localizer.SetCulture("zh-CN");
        await DrainUiAsync();

        Assert.Equal(0, notifications);
    }

    [AvaloniaFact]
    public async Task Settings_language_change_keeps_live_selection_and_refreshes_option_text()
    {
        using var host = new MainWindowHeadlessTestHost(appSettings: new AppSettings(Language: "en-US", SavingPath: "saved"));
        var confirmation = new FakeSettingsCloseConfirmationService(SettingsCloseAction.Cancel);
        using var service = CreateService(host, confirmation);
        await host.LayoutAsync();
        await OpenSettingsAsync(service, host);
        var window = SettingsWindow(service);
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
        var settings = SettingsViewModel(window);
        var japaneseIndex = settings.Languages.ToList().FindIndex(language => language.CultureName == "ja-JP");

        Assert.True(host.ContainsRenderedText("Chapter name"));

        settings.SelectedLanguageIndex = japaneseIndex;
        await DrainUiAsync();
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
        await host.LayoutAsync();

        var languageBox = window.GetVisualDescendants()
            .OfType<ComboBox>()
            .Single(box => ReferenceEquals(box.ItemsSource, settings.Languages));
        var selectedItem = Assert.IsType<LanguageOptionViewModel>(languageBox.SelectedItem);

        try
        {
            Assert.Same(settings, SettingsViewModel(SettingsWindow(service)));
            Assert.Equal("ja-JP", settings.SelectedLanguage);
            Assert.Equal("ja-JP", host.ViewModel.UiLanguage);
            Assert.Equal(japaneseIndex, settings.SelectedLanguageIndex);
            Assert.Equal(japaneseIndex, languageBox.SelectedIndex);
            Assert.Equal("ja-JP", selectedItem.CultureName);
            Assert.Equal("日本語", selectedItem.DisplayName);
            Assert.True(host.ContainsRenderedText("チャプター名"));
            Assert.Contains(
                settings.Languages,
                language => language is { CultureName: "en-US", DisplayName: "English" });
            Assert.Equal("en-US", host.SettingsStore.Current.Application.Language);
        }
        finally
        {
            settings.DiscardUnsavedChanges();
            window.Close();
            await DrainUiAsync();
        }
    }

    [AvaloniaFact]
    public async Task Settings_close_detaches_content_tree()
    {
        using var host = new MainWindowHeadlessTestHost();
        using var service = CreateService(host, new FakeSettingsCloseConfirmationService(SettingsCloseAction.Cancel));
        await OpenSettingsAsync(service, host);
        var window = SettingsWindow(service);

        window.Close();
        await DrainUiAsync();

        Assert.False(window.IsVisible);
        Assert.Null(window.Content);
    }

    [AvaloniaFact]
    public async Task Standard_tool_catalog_constructs_every_tool_window()
    {
        using var host = new MainWindowHeadlessTestHost(appSettings: new AppSettings(Language: "en-US", SavingPath: "saved"));
        using var service = CreateService(host, new FakeSettingsCloseConfirmationService(SettingsCloseAction.Cancel));
        await host.LayoutAsync();

        foreach (var toolId in new[]
                 {
                     ToolIds.Preview,
                     ToolIds.Log,
                     ToolIds.Language,
                     ToolIds.TemplateNames,
                     ToolIds.Zones,
                     ToolIds.ForwardShift,
                     ToolIds.History
                 })
        {
            var request = new AuxiliaryToolRequest(
                host.ViewModel.ToolSession,
                host.Localizer,
                Capabilities: host.ViewModel.Capabilities);
            var result = await service.OpenAsync(toolId, request, TestContext.Current.CancellationToken);
            Assert.Equal(AuxiliaryToolResultKind.Opened, result.Kind);

            await DrainUiAsync();
            await service.CloseAsync(toolId, TestContext.Current.CancellationToken);
            await DrainUiAsync();
        }
    }

    private static AvaloniaWindowService CreateService(
        MainWindowHeadlessTestHost host,
        ISettingsCloseConfirmationService confirmation) =>
        new(
            host.Localizer,
            host.SettingsStore,
            new FakeThemeApplicationService(),
            _ => host.SettingsPickerService,
            externalToolLocator: new UnavailableExternalToolLocator(),
            settingsCloseConfirmationService: confirmation,
            shellService: host.ShellService,
            fontFamilyCatalog: host.FontFamilyCatalog,
            fontApplicationService: host.FontApplicationService,
            settingsDirectory: Path.GetTempPath(),
            expressionAuthoringService: host.ViewModel.ExpressionAuthoringService,
            clipboardServiceFactory: _ => new UnavailableClipboardService(),
            toolCatalog: StandardToolCatalogFactory.Create());

    private static async ValueTask OpenSettingsAsync(AvaloniaWindowService service, MainWindowHeadlessTestHost host)
    {
        await service.OpenAsync(
            ToolIds.Settings,
            new AuxiliaryToolRequest(host.ViewModel.ToolSession, host.Localizer, Capabilities: host.ViewModel.Capabilities),
            TestContext.Current.CancellationToken);

        await SettingsViewModel(SettingsWindow(service)).InitializationTask;
    }

    private static Window SettingsWindow(AvaloniaWindowService service)
    {
        return service.WindowsForTesting["settings"];
    }

    private static Window FindWindow(AvaloniaWindowService service, string id)
    {
        return service.WindowsForTesting[id];
    }

    private static void SelectPreset(SettingsToolViewModel settings, string presetId)
    {
        var index = settings.Appearance.ThemePresets.ToList().FindIndex(option => option.Id == presetId);
        Assert.True(index >= 0, $"Preset not found: {presetId}");
        settings.Appearance.SelectedThemePresetIndex = index;
    }

    private static async ValueTask DrainUiAsync()
    {
        Dispatcher.UIThread.RunJobs();
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();
    }

    private static SettingsToolViewModel SettingsViewModel(Window window) =>
        window.Content is SettingsToolView { DataContext: SettingsToolViewModel viewModel }
            ? viewModel
            : throw new InvalidOperationException("Settings ViewModel was not rendered.");

    private sealed class FakeSettingsCloseConfirmationService(SettingsCloseAction action) : ISettingsCloseConfirmationService
    {
        public int Calls { get; private set; }

        public ValueTask<SettingsCloseAction> ConfirmCloseAsync(Window owner, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(action);
        }
    }

    private sealed class FakeThemeApplicationService : IThemeApplicationService
    {
        public void Apply(ThemeSettings settings)
        {
        }
    }

    private sealed class DisposableState : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
