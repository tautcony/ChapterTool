using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaWindowService : IWindowService
{
    private readonly ISettingsStore<ChapterToolSettings>? settingsStore;
    private readonly IThemeApplicationService? themeApplicationService;
    private readonly IFontFamilyCatalog? fontFamilyCatalog;
    private readonly IFontApplicationService? fontApplicationService;
    private readonly ISettingsCloseConfirmationService settingsCloseConfirmationService;
    private readonly Func<Window, ISettingsPickerService>? settingsPickerFactory;
    private readonly IExternalToolLocator? externalToolLocator;
    private readonly IShellService? shellService;
    private readonly string? settingsDirectory;
    private readonly Dictionary<string, Window> windows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> parameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAppLocalizer localizer;
    private readonly IReadOnlyList<ToolWindowRegistration> registrations;

    public AvaloniaWindowService(
        IAppLocalizer localizer,
        ISettingsStore<ChapterToolSettings>? settingsStore = null,
        IThemeApplicationService? themeApplicationService = null,
        Func<Window, ISettingsPickerService>? settingsPickerFactory = null,
        IExternalToolLocator? externalToolLocator = null,
        ISettingsCloseConfirmationService? settingsCloseConfirmationService = null,
        IShellService? shellService = null,
        IFontFamilyCatalog? fontFamilyCatalog = null,
        IFontApplicationService? fontApplicationService = null,
        string? settingsDirectory = null,
        IReadOnlyList<ToolWindowRegistration>? registrations = null)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        this.settingsStore = settingsStore;
        this.themeApplicationService = themeApplicationService;
        this.fontFamilyCatalog = fontFamilyCatalog;
        this.fontApplicationService = fontApplicationService;
        this.localizer = localizer;
        this.settingsCloseConfirmationService = settingsCloseConfirmationService
            ?? new AvaloniaSettingsCloseConfirmationService(this.localizer);
        this.settingsPickerFactory = settingsPickerFactory;
        this.externalToolLocator = externalToolLocator;
        this.shellService = shellService;
        this.settingsDirectory = settingsDirectory;
        this.registrations = registrations ?? ToolWindowRegistry.DefaultRegistrations;
        this.localizer.CultureChanged += (_, _) =>
        {
            foreach (var (id, window) in windows)
            {
                window.Title = Title(id);
                if (window.Content is TextBlock placeholder)
                {
                    placeholder.Text = PlaceholderText(id);
                }
                else if (window.Content is null)
                {
                    Refresh(window, id, parameters.GetValueOrDefault(id));
                }
            }
        };
    }

    public ValueTask ShowAsync(string windowId, object? parameter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (windows.TryGetValue(windowId, out var existing))
        {
            Refresh(existing, windowId, parameter);
            existing.Activate();
            return ValueTask.CompletedTask;
        }

        var registration = ToolWindowRegistry.Find(windowId)
            ?? this.registrations.FirstOrDefault(entry =>
                string.Equals(entry.Id, windowId, StringComparison.OrdinalIgnoreCase));
        var window = new Window
        {
            Title = Title(windowId),
            Width = registration?.PreferredWidth ?? 620,
            Height = 460,
            MinWidth = 420,
            MinHeight = 280,
        };
        var closeAccepted = false;
        Refresh(window, windowId, parameter);
        parameters[windowId] = parameter;
        window.Closing += async (sender, args) =>
        {
            if (closeAccepted || window.Content is not Views.Tools.SettingsToolView { DataContext: SettingsToolViewModel settings } || !settings.HasUnsavedChanges)
            {
                return;
            }

            args.Cancel = true;
            var action = await settingsCloseConfirmationService.ConfirmCloseAsync(window, CancellationToken.None);
            switch (action)
            {
                case SettingsCloseAction.Save:
                    await settings.SaveCommand.ExecuteAsync(cancellationToken: CancellationToken.None);
                    closeAccepted = true;
                    ((Window)sender!).Close();
                    break;
                case SettingsCloseAction.Discard:
                    settings.DiscardUnsavedChanges();
                    closeAccepted = true;
                    ((Window)sender!).Close();
                    break;
                case SettingsCloseAction.Cancel:
                    break;
            }
        };
        window.Closed += (_, _) =>
        {
            DisposeContentDataContext(window);
            windows.Remove(windowId);
            parameters.Remove(windowId);
        };
        windows[windowId] = window;
        window.Show();
        return ValueTask.CompletedTask;
    }

    public ValueTask HideAsync(string windowId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (windows.TryGetValue(windowId, out var window))
        {
            window.Close();
        }

        return ValueTask.CompletedTask;
    }

    private void Refresh(Window window, string id, object? parameter)
    {
        DisposeContentDataContext(window);
        window.Title = Title(id);
        parameters[id] = parameter;
        window.Content = parameter is MainWindowViewModel viewModel
            ? CreateContent(window, id, viewModel)
            : Placeholder(PlaceholderText(id));
    }

    private Control CreateContent(Window window, string id, MainWindowViewModel viewModel)
    {
        var registration = ToolWindowRegistry.Find(id)
            ?? registrations.FirstOrDefault(entry =>
                string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase));
        if (registration is null)
        {
            return Placeholder(PlaceholderText(id));
        }

        var context = new ToolWindowCreateContext
        {
            HostWindow = window,
            Owner = viewModel,
            Localizer = localizer,
            SettingsStore = settingsStore,
            ThemeApplicationService = themeApplicationService,
            FontFamilyCatalog = fontFamilyCatalog,
            FontApplicationService = fontApplicationService,
            SettingsPickerFactory = settingsPickerFactory,
            ExternalToolLocator = externalToolLocator,
            ShellService = shellService,
            SettingsDirectory = settingsDirectory,
        };
        return registration.CreateContent(context);
    }

    private static TextBlock Placeholder(string text) =>
        new()
        {
            Margin = new Thickness(20),
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 16
        };

    private string Title(string id)
    {
        var registration = ToolWindowRegistry.Find(id)
            ?? registrations.FirstOrDefault(entry =>
                string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase));
        return registration is null
            ? id
            : localizer.GetString(registration.TitleResourceKey);
    }

    private string PlaceholderText(string id) => Title(id);

    private static void DisposeContentDataContext(Window window)
    {
        if (window.Content is Control { DataContext: IDisposable disposable })
        {
            disposable.Dispose();
        }
    }
}
