using ChapterTool.Infrastructure.Platform;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Infrastructure.Tests;

public sealed class ApplicationLogPanelProviderTests
{
    [Fact]
    public void CapturesSeverityCategoryEventAndStructuredState()
    {
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");

        logger.LogWarning(
            new EventId(42, "Log.Test"),
            "Import diagnostic {Code} at {Location}",
            "PartialParse",
            "line 5");

        var entry = Assert.Single(service.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("ChapterTool.Tests", entry.Category);
        Assert.Equal(42, entry.EventId);
        Assert.Equal("Log.Test", entry.EventName);
        Assert.Equal("Import diagnostic PartialParse at line 5", entry.Message);
        Assert.Equal("PartialParse", entry.StructuredState?["Code"]);
        Assert.Equal("line 5", entry.StructuredState?["Location"]);
    }

    [Fact]
    public void PreservesMessageKeysArgumentsAndTechnicalDetails()
    {
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");
        var state = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageKey"] = "Log.Diagnostic",
            ["operation"] = "Load",
            ["code"] = "FfprobeProcessFailed",
            ["TechnicalDetail"] = "exitCode=1 stderr=failed"
        };

        logger.Log(LogLevel.Error, new EventId(0, "Log.Diagnostic"), state, null, static (values, _) => values["MessageKey"]?.ToString() ?? string.Empty);

        var entry = Assert.Single(service.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Log.Diagnostic", entry.MessageKey);
        Assert.Equal("Log.Diagnostic", entry.Message);
        Assert.Equal("Load", entry.Arguments?["operation"]);
        Assert.Equal("FfprobeProcessFailed", entry.Arguments?["code"]);
        Assert.Equal("exitCode=1 stderr=failed", entry.TechnicalDetail);
        Assert.DoesNotContain(entry.Arguments!, pair => pair.Key is "MessageKey" or "TechnicalDetail");
    }

    [Fact]
    public void EnforcesBoundedRetentionAndClearOnlyRemovesRecentEntries()
    {
        var service = new ApplicationLogPanelProvider(capacity: 2);
        var logger = service.CreateLogger("ChapterTool.Tests");

        logger.LogInformation("First");
        logger.LogInformation("Second");
        logger.LogInformation("Third");

        Assert.Equal(["Second", "Third"], service.Entries.Select(static entry => entry.Message));

        service.Clear();
        Assert.Empty(service.Entries);

        logger.LogInformation("Fourth");
        Assert.Equal("Fourth", Assert.Single(service.Entries).Message);
    }

    [Fact]
    public void NotifiesSubscribersAfterAcceptedEntriesAreStored()
    {
        var service = new ApplicationLogPanelProvider(capacity: 2);
        var logger = service.CreateLogger("ChapterTool.Tests");
        var notifications = new List<ApplicationLogEntrySnapshot>();

        service.EntryAdded += (_, entry) =>
            notifications.Add(new ApplicationLogEntrySnapshot(entry.Message, service.Entries.Select(static item => item.Message).ToArray()));

        logger.LogInformation("First");
        logger.LogInformation("Second");
        logger.LogInformation("Third");

        Assert.Equal(["First", "Second", "Third"], notifications.Select(static notification => notification.Message));
        Assert.Equal(["Second", "Third"], notifications[^1].CurrentHistory);
        Assert.Equal(["Second", "Third"], service.Entries.Select(static entry => entry.Message));
    }

    [Fact]
    public void DoesNotNotifyForFilteredEntries()
    {
        var service = new ApplicationLogPanelProvider(minimumLevel: LogLevel.Warning);
        var logger = service.CreateLogger("ChapterTool.Tests");
        var notifications = 0;

        service.EntryAdded += (_, _) => notifications++;

        logger.LogInformation("Hidden");
        logger.LogWarning("Visible");

        Assert.Equal(1, notifications);
        Assert.Equal("Visible", Assert.Single(service.Entries).Message);
    }

    [Fact]
    public void ClearDoesNotDisableFutureNotifications()
    {
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");
        var messages = new List<string>();

        service.EntryAdded += (_, entry) => messages.Add(entry.Message);

        logger.LogInformation("Before clear");
        service.Clear();
        logger.LogInformation("After clear");

        Assert.Equal(["Before clear", "After clear"], messages);
        Assert.Equal("After clear", Assert.Single(service.Entries).Message);
    }

    private sealed record ApplicationLogEntrySnapshot(string Message, IReadOnlyList<string> CurrentHistory);
}
