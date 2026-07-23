using System.Text.Json;
using System.Xml.Linq;
using Avalonia.Threading;
using ChapterTool.Avalonia.Session.Ports;
using ChapterTool.Core.Exporting;
using ChapterTool.Infrastructure.Services;

namespace ChapterTool.Avalonia.ViewModels.Tools;

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
        if (liveRefreshService != null)
        {
            liveRefreshService.EntryAdded -= OnEntryAdded;
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

public sealed class TextToolFormatSelector(IExportPreferencePort exportPreferences)
{
    private static IReadOnlyList<ChapterExportFormat> Formats => ChapterExportFormats.All;

    private IExportPreferencePort ExportPreferences { get; } = exportPreferences;

    public IReadOnlyList<string> Labels { get; } = Formats.Select(ChapterExportFormats.DisplayName).ToArray();

    public int SelectedIndex
    {
        get;
        set => field = Math.Clamp(value, 0, Formats.Count - 1);
    } = Math.Clamp(exportPreferences.SaveFormatIndex, 0, Formats.Count - 1);

    public TextToolKind Kind => KindFor(Formats[SelectedIndex]);

    public void Apply(int index)
    {
        SelectedIndex = index;
        ExportPreferences.SaveFormatIndex = SelectedIndex;
    }

    private static TextToolKind KindFor(ChapterExportFormat format) =>
        format switch
        {
            ChapterExportFormat.Xml => TextToolKind.Xml,
            ChapterExportFormat.Json => TextToolKind.Json,
            _ => TextToolKind.Plain
        };
}
