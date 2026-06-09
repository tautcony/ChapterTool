using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views.Tools;
using ChapterTool.Core.Services;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaWindowService(ISettingsStore<ThemeColorSettings>? themeSettingsStore = null) : IWindowService
{
    private readonly Dictionary<string, Window> windows = new(StringComparer.OrdinalIgnoreCase);

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
            window.Close();
        }

        return ValueTask.CompletedTask;
    }

    private void Refresh(Window window, string id, object? parameter)
    {
        window.Title = Title(id);
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
            "file-association" => Placeholder("File association registration is platform-gated and not enabled in this build."),
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

    private static string Title(string id) => id switch
    {
        "preview" => "Preview",
        "log" => "Log",
        "color-settings" => "Color Settings",
        "language" => "Language",
        "expression" => "Expression",
        "template-names" => "Template Names",
        "file-association" => "File Association",
        "zones" => "Zones",
        "forward-shift" => "Forward Shift",
        _ => id
    };

}
