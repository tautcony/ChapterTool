using System.Collections.ObjectModel;
using ChapterTool.Core.Services;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.ViewModels;

public sealed class TextToolViewModel : ObservableViewModel
{
    private readonly Func<string> refreshText;
    private readonly Action? clearAction;
    private string text;

    public TextToolViewModel(Func<string> refreshText, Action? clearAction = null)
    {
        this.refreshText = refreshText;
        this.clearAction = clearAction;
        text = refreshText();
        RefreshCommand = new UiCommand((_, _) =>
        {
            Text = this.refreshText();
            return ValueTask.CompletedTask;
        });
        ClearCommand = new UiCommand((_, _) =>
        {
            this.clearAction?.Invoke();
            Text = string.Empty;
            return ValueTask.CompletedTask;
        }, _ => this.clearAction is not null);
    }

    public string Text
    {
        get => text;
        private set => SetProperty(ref text, value);
    }

    public UiCommand RefreshCommand { get; }

    public UiCommand ClearCommand { get; }
}

public sealed class ColorSettingsViewModel : ObservableViewModel
{
    private readonly ISettingsStore<ThemeColorSettings>? store;

    public ColorSettingsViewModel(ISettingsStore<ThemeColorSettings>? store)
    {
        this.store = store;
        Slots = new ObservableCollection<ColorSlotViewModel>(
            ThemeColorSettings.Default.OrderedSlots.Select(static slot => new ColorSlotViewModel(slot.Name, slot.Value)));
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
        var values = settings.OrderedSlots.ToArray();
        for (var index = 0; index < Slots.Count && index < values.Length; index++)
        {
            Slots[index].Value = values[index].Value;
        }
    }

    private async ValueTask SaveAsync(CancellationToken cancellationToken)
    {
        if (store is null || Slots.Count < 6)
        {
            return;
        }

        var defaults = ThemeColorSettings.Default.OrderedSlots.ToArray();
        await store.SaveAsync(
            new ThemeColorSettings(
                NormalizeColor(Slots[0].Value, defaults[0].Value),
                NormalizeColor(Slots[1].Value, defaults[1].Value),
                NormalizeColor(Slots[2].Value, defaults[2].Value),
                NormalizeColor(Slots[3].Value, defaults[3].Value),
                NormalizeColor(Slots[4].Value, defaults[4].Value),
                NormalizeColor(Slots[5].Value, defaults[5].Value)),
            cancellationToken);
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
    private string value = value;

    public string Name { get; } = name;

    public string Value
    {
        get => value;
        set => SetProperty(ref this.value, value);
    }
}

public sealed class LanguageToolViewModel(MainWindowViewModel owner) : ObservableViewModel
{
    private string selectedLanguage = string.Equals(owner.UiLanguage, "en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : "";

    public string SelectedLanguage
    {
        get => selectedLanguage;
        set
        {
            if (SetProperty(ref selectedLanguage, value))
            {
                OnPropertyChanged(nameof(SelectedLanguageIndex));
            }
        }
    }

    public int SelectedLanguageIndex
    {
        get => string.Equals(SelectedLanguage, "en-US", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        set => SelectedLanguage = value == 1 ? "en-US" : "";
    }

    public UiCommand ApplyCommand { get; } = new(async (parameter, token) =>
    {
        var language = parameter is LanguageToolViewModel viewModel
            ? viewModel.SelectedLanguage
            : parameter?.ToString() == "en-US" ? "en-US" : "";
        await owner.SaveUiLanguageAsync(language, token);
    });
}

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
    private bool autoGenerateNames = owner.AutoGenerateNames;
    private bool useTemplateNames = owner.UseTemplateNames;

    public bool AutoGenerateNames
    {
        get => autoGenerateNames;
        set => SetProperty(ref autoGenerateNames, value);
    }

    public bool UseTemplateNames
    {
        get => useTemplateNames;
        set => SetProperty(ref useTemplateNames, value);
    }

    public UiCommand ApplyCommand { get; } = new((parameter, _) =>
    {
        if (parameter is TemplateNamesToolViewModel viewModel)
        {
            owner.AutoGenerateNames = viewModel.AutoGenerateNames;
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
