using ChapterTool.Avalonia.Session.Ports;

namespace ChapterTool.Avalonia.ViewModels.Tools;

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
