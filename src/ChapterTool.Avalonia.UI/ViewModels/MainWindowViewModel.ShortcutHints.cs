using ChapterTool.Contracts.Shortcuts;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Builds localized action tooltips from the active shortcut mapping.</summary>
public sealed partial class MainWindowViewModel
{
    public string LoadButtonTooltip => ActionTooltip(ShortcutCatalog.LoadId, "Main.LoadTooltip");

    public string SaveButtonTooltip
    {
        get
        {
            var tooltip = ActionTooltip(ShortcutCatalog.SaveId, "Main.SaveShortcutHint");
            var directory = EffectiveSaveDirectoryDisplay;
            return string.IsNullOrWhiteSpace(directory) ? tooltip : $"{directory} ({tooltip})";
        }
    }

    public string ReloadButtonTooltip => ActionTooltip(ShortcutCatalog.ReloadId, "Main.Reload");

    public string LogButtonTooltip => ActionTooltip(ShortcutCatalog.LogId, "Shortcuts.Action.Log");

    public string PreviewButtonTooltip => ActionTooltip(ShortcutCatalog.PreviewId, "Shortcuts.Action.Preview");

    public string UndoButtonTooltip => ActionTooltip(ShortcutCatalog.UndoId, "Shortcuts.Action.Undo");

    public string RedoButtonTooltip => ActionTooltip(ShortcutCatalog.RedoId, "Shortcuts.Action.Redo");

    private string ActionTooltip(string actionId, string labelKey)
    {
        var label = Localizer.GetString(labelKey);
        var gestures = ActiveShortcutMapping.GesturesForAction(actionId);
        if (gestures.Count == 0)
        {
            return label;
        }

        var display = string.Join(", ", gestures.Select(ShortcutGestureText.FormatForDisplay));
        var hint = Localizer.Format(
            "Main.ShortcutHintFormat",
            new Dictionary<string, object?> { ["0"] = display });
        return $"{label} ({hint})";
    }

    private void NotifyShortcutHintProperties()
    {
        OnPropertyChanged(nameof(LoadButtonTooltip));
        OnPropertyChanged(nameof(SaveButtonTooltip));
        OnPropertyChanged(nameof(ReloadButtonTooltip));
        OnPropertyChanged(nameof(LogButtonTooltip));
        OnPropertyChanged(nameof(PreviewButtonTooltip));
        OnPropertyChanged(nameof(UndoButtonTooltip));
        OnPropertyChanged(nameof(RedoButtonTooltip));
    }
}
