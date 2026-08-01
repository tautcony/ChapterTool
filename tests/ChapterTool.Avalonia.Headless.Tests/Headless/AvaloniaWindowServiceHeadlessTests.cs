using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
                language => language is { CultureName: "en-US", DisplayName: "英語" });
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
        var field = typeof(AvaloniaWindowService).GetField("windows", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Window service did not expose its window registry.");
        var windows = (IReadOnlyDictionary<string, Window>)field.GetValue(service)!;
        return windows["settings"];
    }

    private static Window FindWindow(AvaloniaWindowService service, string id)
    {
        var field = typeof(AvaloniaWindowService).GetField("windows", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Window service did not expose its window registry.");
        var windows = (IReadOnlyDictionary<string, Window>)field.GetValue(service)!;
        return windows[id];
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
