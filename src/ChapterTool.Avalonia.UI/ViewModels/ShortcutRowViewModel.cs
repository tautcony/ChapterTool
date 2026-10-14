using ChapterTool.Contracts.Shortcuts;

namespace ChapterTool.Avalonia.UI.ViewModels;

public sealed class ShortcutRowViewModel : ObservableViewModel
{
    private readonly Action changed;
    private readonly Func<string, string>? resolveName;
    private string gesture;

    public ShortcutRowViewModel(ShortcutAction action, string gesture, Action changed, Func<string, string>? resolveName = null)
    {
        Action = action;
        this.gesture = gesture;
        this.changed = changed;
        this.resolveName = resolveName;
        ResetCommand = new UiCommand((_, _) =>
        {
            Reset();
            return ValueTask.CompletedTask;
        });
    }

    public ShortcutAction Action { get; }

    public string ActionId => Action.Id;

    public string NameKey => Action.NameKey;

    public string DisplayName => resolveName?.Invoke(NameKey) ?? NameKey;

    public void RefreshLocalizedName() => OnPropertyChanged(nameof(DisplayName));

    public string DefaultGesture => Action.DefaultGesture;

    public bool IsEditable => Action.IsEditable;

    public bool IsFixedSymbol => !Action.IsEditable && Gesture is "Insert" or "Delete";

    public bool HasConflict
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(Validation));
                OnPropertyChanged(nameof(IsValid));
            }
        }
    }

    public UiCommand ResetCommand { get; }

    public string Gesture
    {
        get => gesture;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (SetProperty(ref gesture, normalized))
            {
                OnPropertyChanged(nameof(DisplayGesture));
                OnPropertyChanged(nameof(Validation));
                OnPropertyChanged(nameof(IsValid));
                changed();
            }
        }
    }

    public string DisplayGesture => ShortcutGestureText.FormatForDisplay(Gesture);

    public string? Validation => !Action.IsEditable
        ? null
        : HasConflict
            ? "This shortcut is already assigned"
            : ShortcutGestureText.Validate(Gesture) switch
    {
        ShortcutGestureStatus.Malformed => "Invalid gesture",
        ShortcutGestureStatus.Reserved => "A modifier is required",
        _ => null
    };

    public bool IsValid => Validation is null;

    public void SetConflict(bool value) => HasConflict = value;

    public void Reset() => Gesture = DefaultGesture;
}
