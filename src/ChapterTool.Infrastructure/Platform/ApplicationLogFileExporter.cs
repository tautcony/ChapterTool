using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ChapterTool.Contracts.PlatformPorts;

namespace ChapterTool.Infrastructure.Platform;

public sealed class ApplicationLogFileExporter(string settingsDirectory, TimeProvider? timeProvider = null)
    : IApplicationLogExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async ValueTask<ApplicationLogExportResult> ExportAsync(
        ApplicationLogExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var directory = Path.Combine(settingsDirectory, "logs");
            Directory.CreateDirectory(directory);
            var extension = request.Format == LogExportFormat.Json ? "json" : "csv";
            var stamp = timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture);
            var path = Path.Combine(directory, $"chaptertool-export-{stamp}.{extension}");
            var content = request.Format == LogExportFormat.Json
                ? SerializeJson(request.Entries)
                : SerializeCsv(request.Entries);
            await File.WriteAllTextAsync(path, content, new UTF8Encoding(false), cancellationToken);
            return ApplicationLogExportResult.Success(path);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or System.Security.SecurityException
            or JsonException
            or InvalidOperationException)
        {
            return ApplicationLogExportResult.Failure(exception.Message);
        }
    }

    private static string SerializeJson(IReadOnlyList<ApplicationLogEntry> entries) =>
        JsonSerializer.Serialize(entries
            .OrderBy(static entry => entry.Timestamp)
            .Select(ToExportValue), JsonOptions);

    private static string SerializeCsv(IReadOnlyList<ApplicationLogEntry> entries)
    {
        var builder = new StringBuilder();
        builder.Append("timestamp,level,message,category,eventId,eventName,operation,technicalDetail,exception\r\n");
        foreach (var entry in entries.OrderBy(static entry => entry.Timestamp))
        {
            var values = new[]
            {
                entry.Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                entry.Level.ToString(),
                entry.Message,
                entry.Category ?? string.Empty,
                entry.EventId.ToString(CultureInfo.InvariantCulture),
                entry.EventName ?? string.Empty,
                entry.Operation ?? string.Empty,
                entry.TechnicalDetail ?? string.Empty,
                entry.ExceptionText ?? string.Empty
            };
            builder.AppendJoin(',', values.Select(QuoteCsv));
            builder.Append("\r\n");
        }

        return builder.ToString();
    }

    private static object ToExportValue(ApplicationLogEntry entry) => new
    {
        timestamp = entry.Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        level = entry.Level.ToString(),
        message = entry.Message,
        category = entry.Category,
        eventId = entry.EventId,
        eventName = entry.EventName,
        operation = entry.Operation,
        technicalDetail = entry.TechnicalDetail,
        exception = entry.ExceptionText,
        arguments = entry.Arguments,
        structuredState = entry.StructuredState
    };

    private static string QuoteCsv(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
}
