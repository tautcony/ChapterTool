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

    string CreateZonesText();

    ValueTask ReportUnexpectedUiException(Exception exception);
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
        Expression = portAdapters.Expression;
        Preferences = portAdapters.Preferences;
        ExportPreferences = portAdapters.ExportPreferences;
        NamingPreferences = portAdapters.NamingPreferences;
        ChapterEdit = portAdapters.ChapterEdit;
        LogService = owner.LogService;
        buildPreview = owner.BuildPreview;
        createZonesText = owner.CreateZonesText;
        reportUnexpectedUiException = owner.ReportUnexpectedUiException;
    }

    public IExpressionSessionPort Expression { get; }

    public IPreferenceSink Preferences { get; }

    public IExportPreferencePort ExportPreferences { get; }

    public INamingPreferencePort NamingPreferences { get; }

    public IChapterEditPort ChapterEdit { get; }

    public IApplicationLogService LogService { get; }

    public IMainShellNotificationPort Notifications => portAdapters.Notifications;

    public string BuildPreview() => buildPreview();

    public string CreateZonesText() => createZonesText();

    public ValueTask ReportUnexpectedUiException(Exception exception) => reportUnexpectedUiException(exception);
}
