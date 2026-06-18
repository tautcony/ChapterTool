using System.Collections.ObjectModel;
using System.Text.Json;
using System.Xml.Linq;
using Avalonia.Media;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Services;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.ViewModels;

public sealed class TextToolViewModel : ObservableViewModel
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    private readonly Func<string> refreshText;
    private readonly TextToolOptions options;
    private string text;
    private TextToolKind kind;
    private IReadOnlyList<TextToolLineViewModel> lines;

    public TextToolViewModel(Func<string> refreshText, TextToolOptions? options = null)
    {
        this.refreshText = refreshText;
        this.options = options ?? TextToolOptions.Default;
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
}

public sealed class TextToolFormatSelector(MainWindowViewModel owner)
{
    private static readonly ChapterExportFormat[] Formats =
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

    private int selectedIndex = Math.Clamp(owner.SaveFormatIndex, 0, Formats.Length - 1);

    private MainWindowViewModel Owner { get; } = owner;

    public IReadOnlyList<string> Labels { get; } = Formats.Select(static format => format.ToString()).ToList();

    public int SelectedIndex
    {
        get => selectedIndex;
        set => selectedIndex = Math.Clamp(value, 0, Formats.Length - 1);
    }

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

public sealed class ColorSettingsViewModel : ObservableViewModel
{
    private readonly ISettingsStore<ThemeColorSettings>? store;
    private readonly IThemeApplicationService? themeApplicationService;

    public ColorSettingsViewModel(
        ISettingsStore<ThemeColorSettings>? store,
        IThemeApplicationService? themeApplicationService = null)
    {
        this.store = store;
        this.themeApplicationService = themeApplicationService;
        Slots = new ObservableCollection<ColorSlotViewModel>(
            ThemeColorSettings.Default.OrderedSlots.Select(static slot => new ColorSlotViewModel(slot.Name, slot.Value)));
        foreach (var slot in Slots)
        {
            slot.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ColorSlotViewModel.Value))
                {
                    ApplyCurrentTheme();
                }
            };
        }

        SaveCommand = new UiCommand(async (_, token) => await SaveAsync(token), _ => this.store is not null);
        _ = LoadAsync();
    }

    public ObservableCollection<ColorSlotViewModel> Slots { get; }

    public UiCommand SaveCommand { get; }

    private async Task LoadAsync()
    {
        if (store is null)
        {
            return;
        }

        var settings = await store.LoadAsync(CancellationToken.None);
        var values = settings.OrderedSlots.ToList();
        for (var index = 0; index < Slots.Count && index < values.Count; index++)
        {
            Slots[index].Value = values[index].Value;
        }

        ApplyCurrentTheme();
    }

    private async ValueTask SaveAsync(CancellationToken cancellationToken)
    {
        if (store is null || Slots.Count < 6)
        {
            return;
        }

        var defaults = ThemeColorSettings.Default.OrderedSlots.ToList();
        var settings = new ThemeColorSettings(
            NormalizeColor(Slots[0].Value, defaults[0].Value),
            NormalizeColor(Slots[1].Value, defaults[1].Value),
            NormalizeColor(Slots[2].Value, defaults[2].Value),
            NormalizeColor(Slots[3].Value, defaults[3].Value),
            NormalizeColor(Slots[4].Value, defaults[4].Value),
            NormalizeColor(Slots[5].Value, defaults[5].Value));
        await store.SaveAsync(settings, cancellationToken);
        themeApplicationService?.Apply(settings);
    }

    private void ApplyCurrentTheme()
    {
        if (Slots.Count < 6)
        {
            return;
        }

        var defaults = ThemeColorSettings.Default.OrderedSlots.ToList();
        themeApplicationService?.Apply(new ThemeColorSettings(
            NormalizeColor(Slots[0].Value, defaults[0].Value),
            NormalizeColor(Slots[1].Value, defaults[1].Value),
            NormalizeColor(Slots[2].Value, defaults[2].Value),
            NormalizeColor(Slots[3].Value, defaults[3].Value),
            NormalizeColor(Slots[4].Value, defaults[4].Value),
            NormalizeColor(Slots[5].Value, defaults[5].Value)));
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
}

public sealed class ColorSlotViewModel(string name, string value) : ObservableViewModel
{
    private string value = NormalizeColor(value, ThemeColorSettings.Default.BackChange);
    private Color color = ParseColor(value, ThemeColorSettings.Default.BackChange);

    public string Name { get; } = name;

    public string Value
    {
        get => value;
        set
        {
            if (!SetProperty(ref this.value, value))
            {
                return;
            }

            if (TryParseColor(value, out var parsed) && parsed != color)
            {
                color = parsed;
                OnPropertyChanged(nameof(Color));
            }
        }
    }

    public Color Color
    {
        get => color;
        set
        {
            if (!SetProperty(ref color, value))
            {
                return;
            }

            var text = ToHex(value);
            if (!string.Equals(this.value, text, StringComparison.Ordinal))
            {
                this.value = text;
                OnPropertyChanged(nameof(Value));
            }
        }
    }

    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static Color ParseColor(string value, string fallback) =>
        TryParseColor(value, out var color) ? color : Color.Parse(fallback);

    private static bool TryParseColor(string? value, out Color color)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var text = value.Trim();
            if (text.Length == 7 && text[0] == '#' && text.Skip(1).All(Uri.IsHexDigit))
            {
                color = Color.Parse(text);
                return true;
            }
        }

        color = default;
        return false;
    }

    private static string NormalizeColor(string? value, string fallback) =>
        TryParseColor(value, out var color) ? ToHex(color) : fallback;
}

public sealed class LanguageToolViewModel : ObservableViewModel
{
    private readonly MainWindowViewModel owner;
    private string selectedLanguage;

    public LanguageToolViewModel(MainWindowViewModel owner)
    {
        this.owner = owner;
        selectedLanguage = AppLanguage.Normalize(owner.UiLanguage);
        Languages = BuildLanguages();
        owner.Localizer.CultureChanged += (_, _) =>
        {
            Languages = BuildLanguages();
        };
        ApplyCommand = new UiCommand(async (parameter, token) =>
        {
            var language = parameter is LanguageToolViewModel viewModel
                ? viewModel.SelectedLanguage
                : AppLanguage.Normalize(parameter?.ToString());
            await owner.SaveUiLanguageAsync(language, token);
        });
    }

    public IReadOnlyList<LanguageOptionViewModel> Languages { get; private set; }

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

    public UiCommand ApplyCommand { get; }

    private List<LanguageOptionViewModel> BuildLanguages()
    {
        var languages = owner.Localizer.SupportedLanguages
            .Select(language => new LanguageOptionViewModel(
                language.CultureName,
                owner.Localizer.GetString(language.DisplayNameKey)))
            .ToList();
        OnPropertyChanged(nameof(Languages));
        OnPropertyChanged(nameof(SelectedLanguageIndex));
        return languages;
    }
}

public sealed record LanguageOptionViewModel(string CultureName, string DisplayName);

public sealed class ExpressionToolViewModel(MainWindowViewModel owner) : ObservableViewModel
{
    private string expression = owner.Expression;
    private bool applyExpression = owner.ApplyExpression;

    public string Expression
    {
        get => expression;
        set => SetProperty(ref expression, value);
    }

    public bool ApplyExpression
    {
        get => applyExpression;
        set => SetProperty(ref applyExpression, value);
    }

    public UiCommand ApplyCommand { get; } = new((parameter, _) =>
    {
        if (parameter is ExpressionToolViewModel viewModel)
        {
            owner.Expression = string.IsNullOrWhiteSpace(viewModel.Expression) ? "t" : viewModel.Expression;
            owner.ApplyExpression = viewModel.ApplyExpression;
        }

        return ValueTask.CompletedTask;
    });
}

public sealed class TemplateNamesToolViewModel(MainWindowViewModel owner) : ObservableViewModel
{
    private bool useTemplateNames = owner.UseTemplateNames;

    public bool UseTemplateNames
    {
        get => useTemplateNames;
        set => SetProperty(ref useTemplateNames, value);
    }

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
    private decimal frames;

    public decimal Frames
    {
        get => frames;
        set => SetProperty(ref frames, value);
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
