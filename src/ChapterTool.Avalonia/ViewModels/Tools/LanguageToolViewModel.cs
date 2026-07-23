using System.Collections.ObjectModel;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Session.Ports;

namespace ChapterTool.Avalonia.ViewModels.Tools;

public sealed class LanguageToolViewModel : ObservableViewModel, IDisposable
{
    private readonly IPreferenceSink preferenceSink;
    private readonly EventHandler cultureChangedHandler;
    private readonly ObservableCollection<LanguageOptionViewModel> languages = [];
    private string selectedLanguage;
    private bool isRefreshingLanguages;

    public LanguageToolViewModel(IPreferenceSink preferenceSink)
    {
        this.preferenceSink = preferenceSink;
        selectedLanguage = AppLanguage.Normalize(preferenceSink.UiLanguage);
        ReplaceLanguages(BuildLanguages());
        cultureChangedHandler = (_, _) =>
        {
            RefreshLanguages();
        };
        preferenceSink.Localizer.CultureChanged += cultureChangedHandler;
        ApplyCommand = new UiCommand(async (parameter, token) =>
        {
            var language = parameter is LanguageToolViewModel viewModel
                ? viewModel.SelectedLanguage
                : AppLanguage.Normalize(parameter?.ToString());
            await preferenceSink.SaveUiLanguageAsync(language, token);
        });
    }

    public IReadOnlyList<LanguageOptionViewModel> Languages => languages;

    public string SelectedLanguage
    {
        get => selectedLanguage;
        set
        {
            if (SetProperty(ref selectedLanguage, AppLanguage.Normalize(value)))
            {
                OnPropertyChanged(nameof(SelectedLanguageIndex));
            }
        }
    }

    public int SelectedLanguageIndex
    {
        get
        {
            var index = Languages.ToList().FindIndex(option => string.Equals(option.CultureName, SelectedLanguage, StringComparison.OrdinalIgnoreCase));
            return index;
        }

        set
        {
            if (isRefreshingLanguages)
            {
                return;
            }

            if (value >= 0 && value < Languages.Count)
            {
                SelectedLanguage = Languages[value].CultureName;
            }
        }
    }

    public UiCommand ApplyCommand { get; }

    public void Dispose()
    {
        preferenceSink.Localizer.CultureChanged -= cultureChangedHandler;
    }

    private void RefreshLanguages()
    {
        isRefreshingLanguages = true;
        try
        {
            ReplaceLanguages(BuildLanguages());
            OnPropertyChanged(nameof(Languages));
        }
        finally
        {
            isRefreshingLanguages = false;
        }

        OnPropertyChanged(nameof(SelectedLanguageIndex));
    }

    private List<LanguageOptionViewModel> BuildLanguages() =>
        preferenceSink.Localizer.SupportedLanguages
            .Select(language => new LanguageOptionViewModel(
                language.CultureName,
                preferenceSink.Localizer.GetString(language.DisplayNameKey)))
            .ToList();

    private void ReplaceLanguages(IReadOnlyList<LanguageOptionViewModel> options)
    {
        languages.Clear();
        foreach (var option in options)
        {
            languages.Add(option);
        }
    }
}

public sealed record LanguageOptionViewModel(string CultureName, string DisplayName);
