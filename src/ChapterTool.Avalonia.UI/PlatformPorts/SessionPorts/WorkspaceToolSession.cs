using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Contracts.PlatformPorts;

namespace ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;

public interface IWorkspaceToolSession
{
    IHistorySessionPort History { get; }

    IExpressionSessionPort Expression { get; }

    IPreferenceSink Preferences { get; }

    IExportPreferencePort ExportPreferences { get; }

    INamingPreferencePort NamingPreferences { get; }

    IChapterEditPort ChapterEdit { get; }

    IChapterContentOperationPort ContentOperations { get; }

    IApplicationLogService LogService { get; }

    IMainShellNotificationPort Notifications { get; }

    string BuildPreview();

    string CreateZonesText();

    ValueTask ReportUnexpectedUiException(Exception exception);
}

public interface IHistorySessionPort
{
    IReadOnlyList<HistoryEntryViewModel> Entries { get; }

    bool CanUndo { get; }

    bool CanRedo { get; }

    string UndoDescription { get; }

    string RedoDescription { get; }

    UiCommand UndoCommand { get; }

    UiCommand RedoCommand { get; }

    UiCommand NavigateCommand { get; }

    event EventHandler? StateChanged;
}

/// <summary>Owns the narrow ports used by secondary tools beside the main shell.</summary>
public sealed class MainWindowToolSession : IWorkspaceToolSession
{
    private readonly MainWindowPortAdapters portAdapters;
    private readonly Func<string> buildPreview;
    private readonly Func<string> createZonesText;
    private readonly Func<Exception, ValueTask> reportUnexpectedUiException;

    public MainWindowToolSession(MainWindowViewModel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        portAdapters = new MainWindowPortAdapters(owner);
        History = new MainWindowHistorySessionPort(owner);
        Expression = portAdapters.Expression;
        Preferences = portAdapters.Preferences;
        ExportPreferences = portAdapters.ExportPreferences;
        NamingPreferences = portAdapters.NamingPreferences;
        ChapterEdit = portAdapters.ChapterEdit;
        ContentOperations = portAdapters.ContentOperations;
        LogService = owner.LogService;
        buildPreview = owner.BuildPreview;
        createZonesText = owner.CreateZonesText;
        reportUnexpectedUiException = owner.ReportUnexpectedUiException;
    }

    public IExpressionSessionPort Expression { get; }

    public IHistorySessionPort History { get; }

    public IPreferenceSink Preferences { get; }

    public IExportPreferencePort ExportPreferences { get; }

    public INamingPreferencePort NamingPreferences { get; }

    public IChapterEditPort ChapterEdit { get; }

    public IChapterContentOperationPort ContentOperations { get; }

    public IApplicationLogService LogService { get; }

    public IMainShellNotificationPort Notifications => portAdapters.Notifications;

    public string BuildPreview() => buildPreview();

    public string CreateZonesText() => createZonesText();

    public ValueTask ReportUnexpectedUiException(Exception exception) => reportUnexpectedUiException(exception);
}

internal sealed class MainWindowHistorySessionPort : IHistorySessionPort, IDisposable
{
    private readonly MainWindowViewModel owner;

    public MainWindowHistorySessionPort(MainWindowViewModel owner)
    {
        this.owner = owner;
        owner.PropertyChanged += OnOwnerPropertyChanged;
    }

    public IReadOnlyList<HistoryEntryViewModel> Entries => owner.HistoryEntries;

    public bool CanUndo => owner.CanUndo;

    public bool CanRedo => owner.CanRedo;

    public string UndoDescription => owner.UndoDescription;

    public string RedoDescription => owner.RedoDescription;

    public UiCommand UndoCommand => owner.UndoCommand;

    public UiCommand RedoCommand => owner.RedoCommand;

    public UiCommand NavigateCommand => owner.NavigateHistoryCommand;

    public event EventHandler? StateChanged;

    public void Dispose() => owner.PropertyChanged -= OnOwnerPropertyChanged;

    private void OnOwnerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(MainWindowViewModel.HistoryEntries)
            or nameof(MainWindowViewModel.CanUndo)
            or nameof(MainWindowViewModel.CanRedo)
            or nameof(MainWindowViewModel.UndoDescription)
            or nameof(MainWindowViewModel.RedoDescription)
            or null)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
