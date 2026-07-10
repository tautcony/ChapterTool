using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views.Tools;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaWindowService : IWindowService
{
    private readonly ISettingsStore<AppSettings>? appSettingsStore;
    private readonly ISettingsStore<ThemeSettings>? themeSettingsStore;
    private readonly ISettingsStore<FontSettings>? fontSettingsStore;
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

    public AvaloniaWindowService(
        ISettingsStore<AppSettings>? appSettingsStore = null,
        ISettingsStore<ThemeSettings>? themeSettingsStore = null,
        IThemeApplicationService? themeApplicationService = null,
        IAppLocalizer? localizer = null,
        Func<Window, ISettingsPickerService>? settingsPickerFactory = null,
        IExternalToolLocator? externalToolLocator = null,
        ISettingsCloseConfirmationService? settingsCloseConfirmationService = null,
        IShellService? shellService = null,
        ISettingsStore<FontSettings>? fontSettingsStore = null,
        IFontFamilyCatalog? fontFamilyCatalog = null,
        IFontApplicationService? fontApplicationService = null,
        string? settingsDirectory = null)
    {
        this.appSettingsStore = appSettingsStore;
        this.themeSettingsStore = themeSettingsStore;
        this.themeApplicationService = themeApplicationService;
        this.fontSettingsStore = fontSettingsStore;
        this.fontFamilyCatalog = fontFamilyCatalog;
        this.fontApplicationService = fontApplicationService;
        this.localizer = localizer ?? new AppLocalizationManager();
        this.settingsCloseConfirmationService = settingsCloseConfirmationService
            ?? new AvaloniaSettingsCloseConfirmationService(this.localizer);
        this.settingsPickerFactory = settingsPickerFactory;
        this.externalToolLocator = externalToolLocator;
        this.shellService = shellService;
        this.settingsDirectory = settingsDirectory;
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

        var window = new Window
        {
            Title = Title(windowId),
            Width = windowId is "preview" or "log" or "settings" ? 760 : 620,
            Height = 460,
            MinWidth = 420,
            MinHeight = 280,
        };
        var closeAccepted = false;
        Refresh(window, windowId, parameter);
        parameters[windowId] = parameter;
        window.Closing += async (sender, args) =>
        {
            if (closeAccepted || window.Content is not SettingsToolView { DataContext: SettingsToolViewModel settings } || !settings.HasUnsavedChanges)
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

    private Control CreateContent(Window window, string id, MainWindowViewModel viewModel) =>
        id switch
        {
            "preview" => new TextToolView
            {
                DataContext = new TextToolViewModel(
                    viewModel.BuildPreview,
                    new TextToolOptions { FormatSelector = new TextToolFormatSelector(viewModel) })
            },
            "log" => new TextToolView
            {
                DataContext = new TextToolViewModel(
                    viewModel.LogText,
                    new TextToolOptions
                    {
                        ClearAction = viewModel.ClearLog,
                        LiveRefreshService = viewModel.LogService
                    })
            },
            "settings" => new SettingsToolView
            {
                DataContext = new SettingsToolViewModel(
                    viewModel,
                    appSettingsStore,
                    themeSettingsStore,
                    localizer,
                    settingsPickerFactory?.Invoke(window),
                    externalToolLocator,
                    themeApplicationService,
                    shellService,
                    fontSettingsStore,
                    fontFamilyCatalog,
                    fontApplicationService,
                    settingsDirectory)
            },
            "language" => new LanguageToolView { DataContext = new LanguageToolViewModel(viewModel) },
            "expression" => new ExpressionToolView { DataContext = new ExpressionToolViewModel(viewModel, new AvaloniaFilePickerService(window, localizer)) },
            "template-names" => new TemplateNamesToolView { DataContext = new TemplateNamesToolViewModel(viewModel) },
            "zones" => new TextToolView { DataContext = new TextToolViewModel(viewModel.CreateZonesText) },
            "forward-shift" => new ForwardShiftToolView { DataContext = new ForwardShiftToolViewModel(viewModel) },
            _ => Placeholder(PlaceholderText(id))
        };

    private static TextBlock Placeholder(string text) =>
        new()
        {
            Margin = new Thickness(20),
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 16
        };

    private string Title(string id) => id switch
    {
        "preview" => localizer.GetString("Tool.Preview.Title"),
        "log" => localizer.GetString("Tool.Log.Title"),
        "settings" => localizer.GetString("Tool.Settings.Title"),
        "language" => localizer.GetString("Tool.Language.Title"),
        "expression" => localizer.GetString("Tool.Expression.Title"),
        "template-names" => localizer.GetString("Tool.TemplateNames.Title"),
        "zones" => localizer.GetString("Tool.Zones.Title"),
        "forward-shift" => localizer.GetString("Tool.ForwardShift.Title"),
        _ => id
    };

    private string PlaceholderText(string id) => Title(id);

    private static void DisposeContentDataContext(Window window)
    {
        if (window.Content is Control { DataContext: IDisposable disposable })
        {
            disposable.Dispose();
        }
    }
}
