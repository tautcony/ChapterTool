using System.Collections.ObjectModel;
using System.Text.Json;
using System.Xml.Linq;
using Avalonia.Threading;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Session.Ports;
using ChapterTool.Core.Exporting;
using ChapterTool.Infrastructure.Services;

namespace ChapterTool.Avalonia.ViewModels;

public sealed class TemplateNamesToolViewModel(INamingPreferencePort namingPreferences) : ObservableViewModel
{
    public bool UseTemplateNames
    {
        get;
        set => SetProperty(ref field, value);
    } = namingPreferences.UseTemplateNames;

    public UiCommand ApplyCommand { get; } = new((parameter, _) =>
    {
        if (parameter is TemplateNamesToolViewModel viewModel)
        {
            namingPreferences.AutoGenerateNames = false;
            namingPreferences.UseTemplateNames = viewModel.UseTemplateNames;
        }

        return ValueTask.CompletedTask;
    });
}

