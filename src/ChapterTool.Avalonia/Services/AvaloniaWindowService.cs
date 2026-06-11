using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views.Tools;
using ChapterTool.Core.Services;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaWindowService : IWindowService
{
    private readonly ISettingsStore<ThemeColorSettings>? themeSettingsStore;
    private readonly Dictionary<string, Window> windows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> parameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAppLocalizer localizer;

    public AvaloniaWindowService(
        ISettingsStore<ThemeColorSettings>? themeSettingsStore = null,
        IAppLocalizer? localizer = null)
    {
        this.themeSettingsStore = themeSettingsStore;
        this.localizer = localizer ?? new AppLocalizationManager();
        this.localizer.CultureChanged += (_, _) =>
        {
            foreach (var (id, window) in windows)
            {
                Refresh(window, id, parameters.GetValueOrDefault(id));
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
            Width = windowId is "preview" or "log" ? 760 : 620,
            Height = 460,
            MinWidth = 420,
            MinHeight = 280,
            MaxWidth = 1100,
            MaxHeight = 840
        };
        Refresh(window, windowId, parameter);
        parameters[windowId] = parameter;
        window.Closed += (_, _) => windows.Remove(windowId);
        windows[windowId] = window;
        window.Show();
        return ValueTask.CompletedTask;
    }

    public ValueTask HideAsync(string windowId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (windows.Remove(windowId, out var window))
        {
            parameters.Remove(windowId);
            window.Close();
        }

        return ValueTask.CompletedTask;
    }

    private void Refresh(Window window, string id, object? parameter)
    {
        window.Title = Title(id);
        parameters[id] = parameter;
        window.Content = parameter is MainWindowViewModel viewModel
            ? CreateContent(window, id, viewModel)
            : Placeholder(Title(id));
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
                    new TextToolOptions { ClearAction = viewModel.ClearLog })
            },
            "color-settings" => new ColorSettingsView { DataContext = new ColorSettingsViewModel(themeSettingsStore) },
            "language" => new LanguageToolView { DataContext = new LanguageToolViewModel(viewModel) },
            "expression" => new ExpressionToolView { DataContext = new ExpressionToolViewModel(viewModel) },
            "template-names" => new TemplateNamesToolView { DataContext = new TemplateNamesToolViewModel(viewModel) },
            "file-association" => Placeholder(localizer.GetString("Prompt.FileAssociationUnsupported")),
            "zones" => new TextToolView { DataContext = new TextToolViewModel(viewModel.CreateZonesText) },
            "forward-shift" => new ForwardShiftToolView { DataContext = new ForwardShiftToolViewModel(viewModel) },
            _ => Placeholder(Title(id))
        };

    private static Control Placeholder(string text) =>
        new TextBlock
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
        "color-settings" => localizer.GetString("Tool.ColorSettings.Title"),
        "language" => localizer.GetString("Tool.Language.Title"),
        "expression" => localizer.GetString("Tool.Expression.Title"),
        "template-names" => localizer.GetString("Tool.TemplateNames.Title"),
        "file-association" => localizer.GetString("Tool.FileAssociation.Title"),
        "zones" => localizer.GetString("Tool.Zones.Title"),
        "forward-shift" => localizer.GetString("Tool.ForwardShift.Title"),
        _ => id
    };

}
