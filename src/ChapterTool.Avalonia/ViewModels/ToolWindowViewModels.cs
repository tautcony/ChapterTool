using System.Collections.ObjectModel;
using System.Text.Json;
using System.Xml.Linq;
using Avalonia.Threading;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Core.Exporting;
using ChapterTool.Infrastructure.Services;

namespace ChapterTool.Avalonia.ViewModels;

public sealed class TextToolViewModel : ObservableViewModel
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    private readonly Func<string> refreshText;
    private readonly TextToolOptions options;
    private readonly IApplicationLogService? liveRefreshService;
    private string text;
    private TextToolKind kind;
    private IReadOnlyList<TextToolLineViewModel> lines;

    public TextToolViewModel(Func<string> refreshText, TextToolOptions? options = null)
    {
        this.refreshText = refreshText;
        this.options = options ?? TextToolOptions.Default;
        liveRefreshService = this.options.LiveRefreshService;
        kind = this.options.FormatSelector?.Kind ?? TextToolKind.Plain;
        text = Format(refreshText(), kind);
        lines = BuildLines(text, kind);
        RefreshCommand = new UiCommand((_, _) =>
        {
            Text = Format(this.refreshText(), Kind);
            return ValueTask.CompletedTask;
        });
        ClearCommand = new UiCommand((_, _) =>
        {
            this.options.ClearAction?.Invoke();
            Text = string.Empty;
            return ValueTask.CompletedTask;
        }, _ => this.options.ClearAction is not null);

        if (liveRefreshService is { } service)
        {
            service.EntryAdded += OnEntryAdded;
        }
    }

    public void DetachLiveRefresh()
    {
        if (liveRefreshService is { } service)
        {
            service.EntryAdded -= OnEntryAdded;
        }
    }

    public string Text
    {
        get => text;
        private set
        {
            if (SetProperty(ref text, value))
            {
                Lines = BuildLines(value, Kind);
            }
        }
    }

    public TextToolKind Kind
    {
        get => kind;
        private set
        {
            if (SetProperty(ref kind, value))
            {
                Lines = BuildLines(Text, value);
            }
        }
    }

    public bool CanClear => options.ClearAction is not null;

    public bool CanSelectFormat => options.FormatSelector is not null;

    public IReadOnlyList<string> FormatOptions => options.FormatSelector?.Labels ?? [];

    public int SelectedFormatIndex
    {
        get => options.FormatSelector?.SelectedIndex ?? -1;
        set
        {
            var selector = options.FormatSelector;
            if (selector is null || value < 0 || value >= selector.Labels.Count || value == selector.SelectedIndex)
            {
                return;
            }

            selector.SelectedIndex = value;
            selector.Apply(value);
            Kind = selector.Kind;
            Text = Format(refreshText(), Kind);
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<TextToolLineViewModel> Lines
    {
        get => lines;
        private set => SetProperty(ref lines, value);
    }

    public UiCommand RefreshCommand { get; }

    public UiCommand ClearCommand { get; }

    private void OnEntryAdded(object? sender, ApplicationLogEntry entry)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            RefreshText();
            return;
        }

        Dispatcher.UIThread.Post(RefreshText);
    }

    private void RefreshText()
    {
        Text = Format(refreshText(), Kind);
    }

    private static string Format(string text, TextToolKind kind)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        try
        {
            return kind switch
            {
                TextToolKind.Json => JsonSerializer.Serialize(
                    JsonSerializer.Deserialize<JsonElement>(text),
                    IndentedJsonOptions),
                TextToolKind.Xml => XDocument.Parse(text).ToString(SaveOptions.None),
                _ => text
            };
        }
        catch (JsonException)
        {
            return text;
        }
        catch (System.Xml.XmlException)
        {
            return text;
        }
    }

    private static IReadOnlyList<TextToolLineViewModel> BuildLines(string text, TextToolKind kind)
    {
        if (text.Length == 0)
        {
            return [];
        }

        return text.ReplaceLineEndings("\n")
            .Split('\n')
            .Select((line, index) => new TextToolLineViewModel(index + 1, Highlight(line, kind)))
            .ToList();
    }

    private static IReadOnlyList<TextToolSpanViewModel> Highlight(string line, TextToolKind kind) =>
        kind switch
        {
            TextToolKind.Json => HighlightJson(line),
            TextToolKind.Xml => HighlightXml(line),
            _ => [new TextToolSpanViewModel(line, TextToolSpanKind.Plain)]
        };

    private static IReadOnlyList<TextToolSpanViewModel> HighlightJson(string line)
    {
        var spans = new List<TextToolSpanViewModel>();
        for (var index = 0; index < line.Length;)
        {
            if (line[index] == '"')
            {
                var end = index + 1;
                while (end < line.Length)
                {
                    if (line[end] == '"' && line[end - 1] != '\\')
                    {
                        end++;
                        break;
                    }

                    end++;
                }

                var token = line[index..Math.Min(end, line.Length)];
                var lookahead = end;
                while (lookahead < line.Length && char.IsWhiteSpace(line[lookahead]))
                {
                    lookahead++;
                }

                spans.Add(new TextToolSpanViewModel(token, lookahead < line.Length && line[lookahead] == ':' ? TextToolSpanKind.Name : TextToolSpanKind.String));
                index = end;
                continue;
            }

            var next = index + 1;
            while (next < line.Length && line[next] != '"')
            {
                next++;
            }

            AddJsonPlainSpan(spans, line[index..next]);
            index = next;
        }

        return spans;
    }

    private static void AddJsonPlainSpan(List<TextToolSpanViewModel> spans, string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            if (char.IsDigit(text[index]) || text[index] == '-')
            {
                var end = index + 1;
                while (end < text.Length && (char.IsDigit(text[end]) || text[end] is '.' or 'e' or 'E' or '+' or '-'))
                {
                    end++;
                }

                spans.Add(new TextToolSpanViewModel(text[index..end], TextToolSpanKind.Number));
                index = end;
                continue;
            }

            var next = index + 1;
            while (next < text.Length && !char.IsDigit(text[next]) && text[next] != '-')
            {
                next++;
            }

            spans.Add(new TextToolSpanViewModel(text[index..next], TextToolSpanKind.Plain));
            index = next;
        }
    }

    private static List<TextToolSpanViewModel> HighlightXml(string line)
    {
        var spans = new List<TextToolSpanViewModel>();
        for (var index = 0; index < line.Length;)
        {
            var open = line.IndexOf('<', index);
            if (open < 0)
            {
                spans.Add(new TextToolSpanViewModel(line[index..], TextToolSpanKind.String));
                break;
            }

            if (open > index)
            {
                spans.Add(new TextToolSpanViewModel(line[index..open], TextToolSpanKind.String));
            }

            var close = line.IndexOf('>', open);
            if (close < 0)
            {
                spans.Add(new TextToolSpanViewModel(line[open..], TextToolSpanKind.Name));
                break;
            }

            spans.Add(new TextToolSpanViewModel(line[open..(close + 1)], TextToolSpanKind.Name));
            index = close + 1;
        }

        return spans;
    }
}

public enum TextToolKind
{
    Plain,
    Xml,
    Json
}

public enum TextToolSpanKind
{
    Plain,
    Name,
    String,
    Number
}

public sealed record TextToolLineViewModel(
    int Number,
    IReadOnlyList<TextToolSpanViewModel> Spans);

public sealed record TextToolSpanViewModel(
    string Text,
    TextToolSpanKind Kind);

public sealed class TextToolOptions
{
    public static TextToolOptions Default { get; } = new();

    public Action? ClearAction { get; init; }

    public TextToolFormatSelector? FormatSelector { get; init; }

    public IApplicationLogService? LiveRefreshService { get; init; }
}

public sealed class TextToolFormatSelector(MainWindowViewModel owner)
{
    private static IReadOnlyList<ChapterExportFormat> Formats => ChapterExportFormats.All;

    private MainWindowViewModel Owner { get; } = owner;

    public IReadOnlyList<string> Labels { get; } = Formats.Select(ChapterExportFormats.DisplayName).ToArray();

    public int SelectedIndex
    {
        get;
        set => field = Math.Clamp(value, 0, Formats.Count - 1);
    } = Math.Clamp(owner.SaveFormatIndex, 0, Formats.Count - 1);

    public TextToolKind Kind => KindFor(Formats[SelectedIndex]);

    public void Apply(int index)
    {
        SelectedIndex = index;
        Owner.SaveFormatIndex = SelectedIndex;
    }

    private static TextToolKind KindFor(ChapterExportFormat format) =>
        format switch
        {
            ChapterExportFormat.Xml => TextToolKind.Xml,
            ChapterExportFormat.Json => TextToolKind.Json,
            _ => TextToolKind.Plain
        };
}

public sealed class LanguageToolViewModel : ObservableViewModel, IDisposable
{
    private readonly MainWindowViewModel owner;
    private readonly EventHandler cultureChangedHandler;
    private readonly ObservableCollection<LanguageOptionViewModel> languages = [];
    private string selectedLanguage;
    private bool isRefreshingLanguages;

    public LanguageToolViewModel(MainWindowViewModel owner)
    {
        this.owner = owner;
        selectedLanguage = AppLanguage.Normalize(owner.UiLanguage);
        ReplaceLanguages(BuildLanguages());
        cultureChangedHandler = (_, _) =>
        {
            RefreshLanguages();
        };
        owner.Localizer.CultureChanged += cultureChangedHandler;
        ApplyCommand = new UiCommand(async (parameter, token) =>
        {
            var language = parameter is LanguageToolViewModel viewModel
                ? viewModel.SelectedLanguage
                : AppLanguage.Normalize(parameter?.ToString());
            await owner.SaveUiLanguageAsync(language, token);
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
        owner.Localizer.CultureChanged -= cultureChangedHandler;
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
        owner.Localizer.SupportedLanguages
            .Select(language => new LanguageOptionViewModel(
                language.CultureName,
                owner.Localizer.GetString(language.DisplayNameKey)))
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

public sealed class ExpressionToolViewModel : ObservableViewModel
{
    private readonly MainWindowViewModel owner;
    private readonly IFilePickerService? filePicker;

    public ExpressionToolViewModel(MainWindowViewModel owner, IFilePickerService? filePicker = null)
    {
        this.owner = owner;
        this.filePicker = filePicker;
        Expression = owner.Expression;
        ApplyExpression = owner.ApplyExpression;
        ExpressionSourceName = owner.ExpressionSourceName;
        Presets = owner.ExpressionPresets
            .Select(static preset => new ExpressionPresetViewModel(preset.Id, preset.DisplayName, preset.Description, preset.ScriptText))
            .ToList();
        SelectedPresetIndex = Presets.ToList().FindIndex(preset => string.Equals(preset.Id, owner.ExpressionPresetId, StringComparison.Ordinal));
        BrowseScriptCommand = new UiCommand(async (_, token) => await BrowseScriptAsync(token), _ => this.filePicker is not null);
        ApplyCommand = new UiCommand((parameter, _) =>
        {
            if (parameter is ExpressionToolViewModel viewModel)
            {
                owner.Expression = string.IsNullOrWhiteSpace(viewModel.Expression) ? "t" : viewModel.Expression;
                owner.ApplyExpression = viewModel.ApplyExpression;
                owner.ExpressionPresetId = viewModel.SelectedPreset?.Id ?? string.Empty;
                owner.ExpressionSourceName = viewModel.ExpressionSourceName;
            }

            return ValueTask.CompletedTask;
        });
    }

    public IAppLocalizer Localizer => owner.Localizer;

    public IReadOnlyList<ExpressionPresetViewModel> Presets { get; }

    public ExpressionPresetViewModel? SelectedPreset =>
        SelectedPresetIndex >= 0 && SelectedPresetIndex < Presets.Count ? Presets[SelectedPresetIndex] : null;

    public int SelectedPresetIndex
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedPreset));
            if (SelectedPreset is { } preset)
            {
                Expression = preset.ScriptText;
                ExpressionSourceName = preset.DisplayName;
                StatusText = owner.Localizer.Format(LocalizedMessage.Create("Status.LuaExpressionPresetSelected", ("preset", preset.DisplayName)));
            }
        }
    } = -1;

    public string Expression
    {
        get;
        set => SetProperty(ref field, value);
    } = "t";

    public bool ApplyExpression
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string ExpressionSourceName
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public bool CanBrowseScript => filePicker is not null;

    public UiCommand BrowseScriptCommand { get; }

    public UiCommand ApplyCommand { get; }

    private async ValueTask BrowseScriptAsync(CancellationToken cancellationToken)
    {
        if (filePicker is null)
        {
            return;
        }

        var path = await filePicker.PickLuaExpressionScriptAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        Expression = text;
        ExpressionSourceName = Path.GetFileName(path);
        SelectedPresetIndex = -1;
        StatusText = owner.Localizer.Format(LocalizedMessage.Create("Status.LuaExpressionScriptLoaded", ("path", ExpressionSourceName)));
    }
}

public sealed record ExpressionPresetViewModel(string Id, string DisplayName, string Description, string ScriptText);

public sealed class TemplateNamesToolViewModel(MainWindowViewModel owner) : ObservableViewModel
{
    public bool UseTemplateNames
    {
        get;
        set => SetProperty(ref field, value);
    } = owner.UseTemplateNames;

    public UiCommand ApplyCommand { get; } = new((parameter, _) =>
    {
        if (parameter is TemplateNamesToolViewModel viewModel)
        {
            owner.AutoGenerateNames = false;
            owner.UseTemplateNames = viewModel.UseTemplateNames;
        }

        return ValueTask.CompletedTask;
    });
}

public sealed class ForwardShiftToolViewModel(MainWindowViewModel owner) : ObservableViewModel
{
    public decimal Frames
    {
        get;
        set => SetProperty(ref field, value);
    }

    public UiCommand ApplyCommand { get; } = new((parameter, _) =>
    {
        if (parameter is ForwardShiftToolViewModel viewModel)
        {
            owner.ShiftFramesForward((int)viewModel.Frames);
        }

        return ValueTask.CompletedTask;
    });
}
