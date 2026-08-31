using System.Collections;
using System.Text.Json;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Infrastructure.Platform;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Infrastructure.Tests;

public sealed class ApplicationLogFileExporterTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"chaptertool-log-export-{Guid.NewGuid():N}");

    [Fact]
    public async Task JsonExportUsesDistinctPathUtf8AndDeterministicOrder()
    {
        var exporter = new ApplicationLogFileExporter(directory);
        var entries = new[]
        {
            Entry("后一个", DateTimeOffset.Parse("2026-08-30T10:00:02Z")),
            Entry("先一个", DateTimeOffset.Parse("2026-08-30T10:00:01Z"))
        };

        var result = await exporter.ExportAsync(new ApplicationLogExportRequest(LogExportFormat.Json, entries));

        Assert.True(result.Succeeded, result.Error);
        Assert.Contains("chaptertool-export-", Path.GetFileName(result.Path), StringComparison.Ordinal);
        Assert.DoesNotMatch("chaptertool-\\d{8}\\.log", Path.GetFileName(result.Path!));
        var bytes = await File.ReadAllBytesAsync(result.Path!);
        Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        using var document = JsonDocument.Parse(bytes);
        Assert.Equal("先一个", document.RootElement[0].GetProperty("message").GetString());
        Assert.Equal("后一个", document.RootElement[1].GetProperty("message").GetString());
    }

    [Fact]
    public async Task CsvExportUsesStableHeaderAndRfc4180Quoting()
    {
        var exporter = new ApplicationLogFileExporter(directory);
        var entry = Entry("值,\"quoted\"\nnext", DateTimeOffset.Parse("2026-08-30T10:00:01Z"));

        var result = await exporter.ExportAsync(new ApplicationLogExportRequest(LogExportFormat.Csv, [entry]));

        Assert.True(result.Succeeded, result.Error);
        var content = await File.ReadAllTextAsync(result.Path!);
        Assert.StartsWith("timestamp,level,message,category,eventId,eventName,operation,technicalDetail,exception\r\n", content, StringComparison.Ordinal);
        Assert.Contains("\"值,\"\"quoted\"\"\nnext\"", content, StringComparison.Ordinal);
        Assert.EndsWith("\r\n", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidSettingsDirectoryReturnsRecoverableFailure()
    {
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "not-a-directory");
        await File.WriteAllTextAsync(file, "occupied");
        var exporter = new ApplicationLogFileExporter(file);

        var result = await exporter.ExportAsync(new ApplicationLogExportRequest(LogExportFormat.Json, []));

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public async Task ArgumentExceptionDuringExportReturnsRecoverableFailure()
    {
        await AssertEnumerationFailureReturnsResult(new ArgumentException("invalid export argument"));
    }

    [Fact]
    public async Task SecurityExceptionDuringExportReturnsRecoverableFailure()
    {
        await AssertEnumerationFailureReturnsResult(new System.Security.SecurityException("export access denied"));
    }

    [Fact]
    public async Task JsonExceptionDuringExportReturnsRecoverableFailure()
    {
        await AssertEnumerationFailureReturnsResult(new JsonException("invalid log value"));
    }

    [Fact]
    public async Task NotSupportedExceptionDuringExportReturnsRecoverableFailure()
    {
        await AssertEnumerationFailureReturnsResult(new NotSupportedException("unsupported log value"));
    }

    [Fact]
    public async Task InvalidOperationExceptionDuringExportReturnsRecoverableFailure()
    {
        await AssertEnumerationFailureReturnsResult(new InvalidOperationException("log enumeration failed"));
    }

    [Fact]
    public async Task CancellationIsPropagatedInsteadOfReturnedAsFailure()
    {
        var exporter = new ApplicationLogFileExporter(directory);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await exporter.ExportAsync(
                new ApplicationLogExportRequest(LogExportFormat.Json, [Entry("cancelled", DateTimeOffset.UtcNow)]),
                cancellation.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ApplicationLogEntry Entry(string message, DateTimeOffset timestamp) => new(
        timestamp,
        LogLevel.Warning,
        message,
        Category: "测试,Category",
        EventId: 42,
        EventName: "Export",
        Operation: "Load");

    private async Task AssertEnumerationFailureReturnsResult(Exception exception)
    {
        var exporter = new ApplicationLogFileExporter(directory);
        var entries = new ThrowingEntries(exception);

        var result = await exporter.ExportAsync(new ApplicationLogExportRequest(LogExportFormat.Json, entries));

        Assert.False(result.Succeeded);
        Assert.Null(result.Path);
        Assert.Contains(exception.Message, result.Error, StringComparison.Ordinal);
    }

    private sealed class ThrowingEntries(Exception exception) : IReadOnlyList<ApplicationLogEntry>
    {
        public int Count => 1;

        public ApplicationLogEntry this[int index] => Entry("unread", DateTimeOffset.UtcNow);

        public IEnumerator<ApplicationLogEntry> GetEnumerator() => throw exception;

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
