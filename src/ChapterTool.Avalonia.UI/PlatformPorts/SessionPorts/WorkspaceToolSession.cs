using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Contracts.PlatformPorts;

namespace ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;

public interface IWorkspaceToolSession
{
    IExpressionSessionPort Expression { get; }

    IPreferenceSink Preferences { get; }

    IExportPreferencePort ExportPreferences { get; }

    INamingPreferencePort NamingPreferences { get; }

    IChapterEditPort ChapterEdit { get; }

    IApplicationLogService LogService { get; }

    IMainShellNotificationPort Notifications { get; }

    string BuildPreview();

    string LogText();

    string CreateZonesText();

    void ClearLog();

    ValueTask ReportUnexpectedUiException(Exception exception);
}

/// <summary>Owns the narrow ports used by secondary tools beside the main shell.</summary>
public sealed class MainWindowToolSession : IWorkspaceToolSession
{
    private readonly Func<string> buildPreview;
    private readonly Func<string> logText;
    private readonly Func<string> createZonesText;
    private readonly Action clearLog;
    private readonly Func<Exception, ValueTask> reportUnexpectedUiException;

    public MainWindowToolSession(MainWindowViewModel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        PortAdapters = new MainWindowPortAdapters(owner);
        Expression = PortAdapters.Expression;
        Preferences = PortAdapters.Preferences;
        ExportPreferences = PortAdapters.ExportPreferences;
        NamingPreferences = PortAdapters.NamingPreferences;
        ChapterEdit = PortAdapters.ChapterEdit;
        LogService = owner.LogService;
        buildPreview = owner.BuildPreview;
        logText = owner.LogText;
        createZonesText = owner.CreateZonesText;
        clearLog = owner.ClearLog;
        reportUnexpectedUiException = owner.ReportUnexpectedUiException;
    }

    public MainWindowPortAdapters PortAdapters { get; }

    public IExpressionSessionPort Expression { get; }

    public IPreferenceSink Preferences { get; }

    public IExportPreferencePort ExportPreferences { get; }

    public INamingPreferencePort NamingPreferences { get; }

    public IChapterEditPort ChapterEdit { get; }

    public IApplicationLogService LogService { get; }

    public IMainShellNotificationPort Notifications => PortAdapters.Notifications;

    public string BuildPreview() => buildPreview();

    public string LogText() => logText();

    public string CreateZonesText() => createZonesText();

    public void ClearLog() => clearLog();

    public ValueTask ReportUnexpectedUiException(Exception exception) => reportUnexpectedUiException(exception);
}
