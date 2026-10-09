using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;
using ChapterTool.Avalonia.UI.ViewModels;

namespace ChapterTool.Avalonia.UI.ViewModels.Tools;

public sealed class HistoryToolViewModel : ObservableViewModel, IDisposable
{
    private readonly IHistorySessionPort history;
    private readonly IAppLocalizer localizer;
    private readonly EventHandler stateChangedHandler;
    private readonly EventHandler cultureChangedHandler;

    public HistoryToolViewModel(IHistorySessionPort history, IAppLocalizer localizer, Action? close = null)
    {
        this.history = history ?? throw new ArgumentNullException(nameof(history));
        this.localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        stateChangedHandler = (_, _) => RefreshState();
        cultureChangedHandler = (_, _) => OnPropertyChanged(nameof(SessionLifetimeText));
        history.StateChanged += stateChangedHandler;
        localizer.CultureChanged += cultureChangedHandler;
        CloseCommand = new UiCommand((_, _) =>
        {
            close?.Invoke();
            return ValueTask.CompletedTask;
        });
    }

    public IReadOnlyList<HistoryEntryViewModel> Entries => history.Entries;

    public bool CanUndo => history.CanUndo;

    public bool CanRedo => history.CanRedo;

    public string UndoDescription => history.UndoDescription;

    public string RedoDescription => history.RedoDescription;

    public string SessionLifetimeText => localizer.GetString("History.SessionLifetime");

    public UiCommand UndoCommand => history.UndoCommand;

    public UiCommand RedoCommand => history.RedoCommand;

    public UiCommand NavigateCommand => history.NavigateCommand;

    public UiCommand CloseCommand { get; }

    public void Dispose()
    {
        history.StateChanged -= stateChangedHandler;
        localizer.CultureChanged -= cultureChangedHandler;
    }

    private void RefreshState()
    {
        OnPropertyChanged(nameof(Entries));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoDescription));
        OnPropertyChanged(nameof(RedoDescription));
    }
}
