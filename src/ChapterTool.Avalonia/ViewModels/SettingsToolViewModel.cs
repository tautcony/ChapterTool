using System.Collections.ObjectModel;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Services;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Tools;

namespace ChapterTool.Avalonia.ViewModels;

public sealed class SettingsToolViewModel : ObservableViewModel
{
    private static readonly ChapterExportFormat[] SaveFormats =
    [
        ChapterExportFormat.Txt,
        ChapterExportFormat.Xml,
        ChapterExportFormat.Qpf,
        ChapterExportFormat.TimeCodes,
        ChapterExportFormat.TsMuxerMeta,
        ChapterExportFormat.Cue,
        ChapterExportFormat.Json,
        ChapterExportFormat.WebVtt,
        ChapterExportFormat.Celltimes,
        ChapterExportFormat.Chapter2Qpfile
    ];

    private readonly MainWindowViewModel owner;
    private readonly ISettingsStore<AppSettings>? appSettingsStore;
    private readonly ISettingsStore<ThemeColorSettings>? themeSettingsStore;
    private readonly IAppLocalizer localizer;
    private readonly ISettingsPickerService? picker;
    private string selectedLanguage;
    private string? saveDirectory;
    private string? mkvToolnixPath;
    private string? eac3toPath;
    private string? ffprobePath;
    private string? ffmpegPath;
    private int defaultSaveFormatIndex;
    private int defaultXmlLanguageIndex;
    private string statusText = string.Empty;
    private string mkvToolnixStatus = string.Empty;
    private string eac3toStatus = string.Empty;
    private string ffprobeStatus = string.Empty;
    private string ffmpegStatus = string.Empty;

    public SettingsToolViewModel(
        MainWindowViewModel owner,
        ISettingsStore<AppSettings>? appSettingsStore,
        ISettingsStore<ThemeColorSettings>? themeSettingsStore,
        IAppLocalizer? localizer = null,
        ISettingsPickerService? picker = null)
    {
        this.owner = owner;
        this.appSettingsStore = appSettingsStore;
        this.themeSettingsStore = themeSettingsStore;
        this.localizer = localizer ?? owner.Localizer;
        this.picker = picker;
        selectedLanguage = AppLanguage.Normalize(owner.UiLanguage);
        defaultSaveFormatIndex = Math.Clamp(owner.SaveFormatIndex, 0, SaveFormats.Length - 1);
        defaultXmlLanguageIndex = XmlLanguageIndex(owner.XmlLanguage);
        Languages = BuildLanguages();
        ColorSlots = new ObservableCollection<ColorSlotViewModel>(
            ThemeColorSettings.Default.OrderedSlots.Select(static slot => new ColorSlotViewModel(slot.Name, slot.Value)));
        SaveCommand = new UiCommand(async (_, token) => await SaveAsync(token), _ => appSettingsStore is not null || themeSettingsStore is not null);
        ResetCommand = new UiCommand((_, _) =>
        {
            ApplyDefaults();
            return ValueTask.CompletedTask;
        });
        ValidateToolsCommand = new UiCommand((_, _) =>
        {
            RefreshToolStatuses();
            return ValueTask.CompletedTask;
        });
        BrowseSaveDirectoryCommand = new UiCommand(async (_, token) => await PickDirectoryAsync(value => SaveDirectory = value, token));
        BrowseMkvToolnixCommand = new UiCommand(async (_, token) => await PickExecutableAsync(value => MkvToolnixPath = value, token));
        BrowseEac3toCommand = new UiCommand(async (_, token) => await PickExecutableAsync(value => Eac3toPath = value, token));
        BrowseFfprobeCommand = new UiCommand(async (_, token) => await PickExecutableAsync(value => FfprobePath = value, token));
        BrowseFfmpegCommand = new UiCommand(async (_, token) => await PickDirectoryAsync(value => FfmpegPath = value, token));
        ClearSaveDirectoryCommand = ClearCommand(() => SaveDirectory = null);
        ClearMkvToolnixCommand = ClearCommand(() => MkvToolnixPath = null);
        ClearEac3toCommand = ClearCommand(() => Eac3toPath = null);
        ClearFfprobeCommand = ClearCommand(() => FfprobePath = null);
        ClearFfmpegCommand = ClearCommand(() => FfmpegPath = null);
        this.localizer.CultureChanged += (_, _) =>
        {
            Languages = BuildLanguages();
            RefreshToolStatuses();
            if (!string.IsNullOrWhiteSpace(StatusText))
            {
                StatusText = this.localizer.GetString("Settings.Status.Ready");
            }
        };
        _ = LoadAsync(CancellationToken.None);
    }

    public IReadOnlyList<LanguageOptionViewModel> Languages { get; private set; }

    public IReadOnlyList<string> SaveFormatOptions { get; } = SaveFormats.Select(static format => format.ToString()).ToArray();

    public IReadOnlyList<string> XmlLanguageOptions { get; } =
        XmlChapterLanguageCatalog.Languages.Select(static language => language.Code).ToArray();

    public ObservableCollection<ColorSlotViewModel> ColorSlots { get; }

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
            return Math.Max(0, index);
        }
        set
        {
            if (value >= 0 && value < Languages.Count)
            {
                SelectedLanguage = Languages[value].CultureName;
            }
        }
    }

    public string? SaveDirectory
    {
        get => saveDirectory;
        set => SetProperty(ref saveDirectory, CleanPath(value));
    }

    public string? MkvToolnixPath
    {
        get => mkvToolnixPath;
        set
        {
            if (SetProperty(ref mkvToolnixPath, CleanPath(value)))
            {
                MkvToolnixStatus = FormatToolStatus(ValidateTool(value, "mkvextract"));
            }
        }
    }

    public string? Eac3toPath
    {
        get => eac3toPath;
        set
        {
            if (SetProperty(ref eac3toPath, CleanPath(value)))
            {
                Eac3toStatus = FormatToolStatus(ValidateTool(value, "eac3to"));
            }
        }
    }

    public string? FfprobePath
    {
        get => ffprobePath;
        set
        {
            if (SetProperty(ref ffprobePath, CleanPath(value)))
            {
                FfprobeStatus = FormatToolStatus(ValidateTool(value, "ffprobe"));
            }
        }
    }

    public string? FfmpegPath
    {
        get => ffmpegPath;
        set
        {
            if (SetProperty(ref ffmpegPath, CleanPath(value)))
            {
                FfmpegStatus = FormatToolStatus(ValidateTool(value, "ffprobe"));
            }
        }
    }

    public int DefaultSaveFormatIndex
    {
        get => defaultSaveFormatIndex;
        set => SetProperty(ref defaultSaveFormatIndex, Math.Clamp(value, 0, SaveFormats.Length - 1));
    }

    public int DefaultXmlLanguageIndex
    {
        get => defaultXmlLanguageIndex;
        set => SetProperty(ref defaultXmlLanguageIndex, Math.Clamp(value, 0, XmlLanguageOptions.Count - 1));
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public string MkvToolnixStatus
    {
        get => mkvToolnixStatus;
        private set => SetProperty(ref mkvToolnixStatus, value);
    }

    public string Eac3toStatus
    {
        get => eac3toStatus;
        private set => SetProperty(ref eac3toStatus, value);
    }

    public string FfprobeStatus
    {
        get => ffprobeStatus;
        private set => SetProperty(ref ffprobeStatus, value);
    }

    public string FfmpegStatus
    {
        get => ffmpegStatus;
        private set => SetProperty(ref ffmpegStatus, value);
    }

    public UiCommand SaveCommand { get; }

    public UiCommand ResetCommand { get; }

    public UiCommand ValidateToolsCommand { get; }

    public UiCommand BrowseSaveDirectoryCommand { get; }

    public UiCommand BrowseMkvToolnixCommand { get; }

    public UiCommand BrowseEac3toCommand { get; }

    public UiCommand BrowseFfprobeCommand { get; }

    public UiCommand BrowseFfmpegCommand { get; }

    public UiCommand ClearSaveDirectoryCommand { get; }

    public UiCommand ClearMkvToolnixCommand { get; }

    public UiCommand ClearEac3toCommand { get; }

    public UiCommand ClearFfprobeCommand { get; }

    public UiCommand ClearFfmpegCommand { get; }

    public async ValueTask LoadAsync(CancellationToken cancellationToken)
    {
        if (appSettingsStore is not null)
        {
            var settings = await appSettingsStore.LoadAsync(cancellationToken);
            SelectedLanguage = settings.Language;
            SaveDirectory = settings.SavingPath;
            MkvToolnixPath = settings.MkvToolnixPath;
            Eac3toPath = settings.Eac3toPath;
            FfprobePath = settings.FfprobePath;
            FfmpegPath = settings.FfmpegPath;
            DefaultSaveFormatIndex = SaveFormatIndex(settings.DefaultSaveFormat);
            DefaultXmlLanguageIndex = XmlLanguageIndex(settings.DefaultXmlLanguage);
        }

        if (themeSettingsStore is not null)
        {
            var theme = await themeSettingsStore.LoadAsync(cancellationToken);
            ApplyColors(theme);
        }

        RefreshToolStatuses();
        StatusText = localizer.GetString("Settings.Status.Ready");
    }

    private async ValueTask SaveAsync(CancellationToken cancellationToken)
    {
        if (appSettingsStore is not null)
        {
            var current = await appSettingsStore.LoadAsync(cancellationToken);
            var settings = current with
            {
                Language = SelectedLanguage,
                SavingPath = SaveDirectory,
                MkvToolnixPath = MkvToolnixPath,
                Eac3toPath = Eac3toPath,
                FfprobePath = FfprobePath,
                FfmpegPath = FfmpegPath,
                DefaultSaveFormat = SaveFormats[DefaultSaveFormatIndex].ToString(),
                DefaultXmlLanguage = XmlLanguageOptions[DefaultXmlLanguageIndex]
            };
            await appSettingsStore.SaveAsync(settings, cancellationToken);
            owner.ApplySettings(settings);
        }

        if (themeSettingsStore is not null)
        {
            await themeSettingsStore.SaveAsync(CurrentThemeSettings(), cancellationToken);
        }

        RefreshToolStatuses();
        StatusText = localizer.GetString("Settings.Status.Saved");
    }

    private void ApplyDefaults()
    {
        var defaults = new AppSettings();
        SelectedLanguage = defaults.Language;
        SaveDirectory = defaults.SavingPath;
        MkvToolnixPath = defaults.MkvToolnixPath;
        Eac3toPath = defaults.Eac3toPath;
        FfprobePath = defaults.FfprobePath;
        FfmpegPath = defaults.FfmpegPath;
        DefaultSaveFormatIndex = SaveFormatIndex(defaults.DefaultSaveFormat);
        DefaultXmlLanguageIndex = XmlLanguageIndex(defaults.DefaultXmlLanguage);
        ApplyColors(ThemeColorSettings.Default);
        RefreshToolStatuses();
        StatusText = localizer.GetString("Settings.Status.Reset");
    }

    private async ValueTask PickDirectoryAsync(Action<string> apply, CancellationToken cancellationToken)
    {
        if (picker is null)
        {
            return;
        }

        var path = await picker.PickDirectoryAsync(localizer.GetString("Settings.BrowseDirectory"), cancellationToken);
        if (!string.IsNullOrWhiteSpace(path))
        {
            apply(path);
        }
    }

    private async ValueTask PickExecutableAsync(Action<string> apply, CancellationToken cancellationToken)
    {
        if (picker is null)
        {
            return;
        }

        var path = await picker.PickExecutableAsync(localizer.GetString("Settings.BrowseExecutable"), cancellationToken);
        if (!string.IsNullOrWhiteSpace(path))
        {
            apply(path);
        }
    }

    private static UiCommand ClearCommand(Action clear) =>
        new((_, _) =>
        {
            clear();
            return ValueTask.CompletedTask;
        });

    private IReadOnlyList<LanguageOptionViewModel> BuildLanguages()
    {
        var languages = localizer.SupportedLanguages
            .Select(language => new LanguageOptionViewModel(
                language.CultureName,
                localizer.GetString(language.DisplayNameKey)))
            .ToArray();
        OnPropertyChanged(nameof(Languages));
        OnPropertyChanged(nameof(SelectedLanguageIndex));
        return languages;
    }

    private void RefreshToolStatuses()
    {
        MkvToolnixStatus = FormatToolStatus(ValidateTool(MkvToolnixPath, "mkvextract"));
        Eac3toStatus = FormatToolStatus(ValidateTool(Eac3toPath, "eac3to"));
        FfprobeStatus = FormatToolStatus(ValidateTool(FfprobePath, "ffprobe"));
        FfmpegStatus = FormatToolStatus(ValidateTool(FfmpegPath, "ffprobe"));
    }

    private string FormatToolStatus(SettingsToolStatus status) =>
        status.Kind switch
        {
            SettingsToolStatusKind.Discovery => localizer.GetString("Settings.ToolStatus.Discovery"),
            SettingsToolStatusKind.Found => localizer.Format("Settings.ToolStatus.Found", new Dictionary<string, object?> { ["path"] = status.ResolvedPath }),
            SettingsToolStatusKind.Missing => localizer.Format("Settings.ToolStatus.Missing", new Dictionary<string, object?> { ["name"] = status.ExpectedExecutable }),
            SettingsToolStatusKind.InvalidPath => localizer.GetString("Settings.ToolStatus.InvalidPath"),
            _ => localizer.GetString("Settings.ToolStatus.Unsupported")
        };

    private static SettingsToolStatus ValidateTool(string? configuredPath, string toolId)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return new SettingsToolStatus(
                SettingsToolStatusKind.Discovery,
                null,
                ExternalToolPathResolver.ExecutableName(toolId));
        }

        var text = configuredPath.Trim();
        var executableName = ExternalToolPathResolver.ExecutableName(toolId);
        if (Directory.Exists(text))
        {
            var candidate = Path.Combine(text, executableName);
            return File.Exists(candidate)
                ? new SettingsToolStatus(SettingsToolStatusKind.Found, candidate, executableName)
                : new SettingsToolStatus(SettingsToolStatusKind.Missing, candidate, executableName);
        }

        return File.Exists(text)
            ? new SettingsToolStatus(SettingsToolStatusKind.Found, text, executableName)
            : new SettingsToolStatus(SettingsToolStatusKind.InvalidPath, text, executableName);
    }

    private ThemeColorSettings CurrentThemeSettings()
    {
        var defaults = ThemeColorSettings.Default.OrderedSlots.ToArray();
        var values = ColorSlots.Select((slot, index) => NormalizeColor(slot.Value, defaults[index].Value)).ToArray();
        return new ThemeColorSettings(values[0], values[1], values[2], values[3], values[4], values[5]);
    }

    private void ApplyColors(ThemeColorSettings settings)
    {
        var values = settings.OrderedSlots.ToArray();
        for (var index = 0; index < ColorSlots.Count && index < values.Length; index++)
        {
            ColorSlots[index].Value = values[index].Value;
        }
    }

    private static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var text = value.Trim();
        return text.Length == 7 && text[0] == '#' && text.Skip(1).All(Uri.IsHexDigit)
            ? text.ToUpperInvariant()
            : fallback;
    }

    private static string? CleanPath(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int SaveFormatIndex(string? value)
    {
        if (Enum.TryParse<ChapterExportFormat>(value, ignoreCase: true, out var format))
        {
            var index = Array.IndexOf(SaveFormats, format);
            return Math.Max(0, index);
        }

        return 0;
    }

    private int XmlLanguageIndex(string? value)
    {
        var index = XmlLanguageOptions.ToList().FindIndex(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase));
        return Math.Max(0, index);
    }

}

public sealed record SettingsToolStatus(
    SettingsToolStatusKind Kind,
    string? ResolvedPath,
    string ExpectedExecutable);

public enum SettingsToolStatusKind
{
    Discovery,
    Found,
    Missing,
    InvalidPath,
    Unsupported
}
