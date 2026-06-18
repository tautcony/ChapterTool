using System.Text.Json;
using ChapterTool.Core.Importing.Media;
using ChapterTool.Core.Services;

namespace ChapterTool.Infrastructure.Importing.Media;

public sealed class FfprobeMediaChapterReader(
    IExternalToolLocator toolLocator,
    IProcessRunner processRunner) : IMediaChapterReader
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public async ValueTask<MediaChapterReadResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var location = await toolLocator.LocateAsync("ffprobe", cancellationToken);
        if (!location.Found || string.IsNullOrWhiteSpace(location.Path))
        {
            return MediaChapterReadResult.Failed(
                "FfprobeMissingDependency",
                location.Message ?? "ffprobe was not found.");
        }

        var request = new ProcessRunRequest(
            location.Path,
            ["-v", "quiet", "-print_format", "json", "-show_chapters", path],
            Path.GetDirectoryName(path),
            DefaultTimeout);

        ProcessRunResult result;
        try
        {
            result = await processRunner.RunAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return MediaChapterReadResult.Failed(
                "FfprobeCannotStart",
                $"ffprobe could not be started: {exception.Message}");
        }

        if (result.Cancelled)
        {
            return MediaChapterReadResult.Failed("FfprobeProcessCancelled", "ffprobe was cancelled.", ProcessDetails(result));
        }

        if (result.TimedOut)
        {
            return MediaChapterReadResult.Failed("FfprobeProcessTimedOut", "ffprobe timed out.", ProcessDetails(result));
        }

        if (result.ExitCode is not 0)
        {
            return MediaChapterReadResult.Failed("FfprobeProcessFailed", "ffprobe exited with a non-zero code.", ProcessDetails(result));
        }

        if (string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return MediaChapterReadResult.Failed("FfprobeEmptyOutput", "ffprobe did not return chapter JSON.", ProcessDetails(result));
        }

        return Parse(result.StandardOutput, result);
    }

    private static MediaChapterReadResult Parse(string json, ProcessRunResult result)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("chapters", out var chaptersElement)
                || chaptersElement.ValueKind != JsonValueKind.Array)
            {
                return MediaChapterReadResult.Failed("FfprobeParseFailed", "ffprobe JSON did not contain a chapters array.", ProcessDetails(result));
            }

            var chapters = new List<MediaChapterEntry>();
            var sourceOrder = 0;
            foreach (var chapterElement in chaptersElement.EnumerateArray())
            {
                if (chapterElement.ValueKind != JsonValueKind.Object)
                {
                    return MediaChapterReadResult.Failed("FfprobeParseFailed", "ffprobe chapter entry was not an object.", ProcessDetails(result));
                }

                chapters.Add(new MediaChapterEntry(
                    Int32Property(chapterElement, "id"),
                    StringProperty(chapterElement, "time_base"),
                    Int64Property(chapterElement, "start"),
                    Int64Property(chapterElement, "end"),
                    StringProperty(chapterElement, "start_time"),
                    StringProperty(chapterElement, "end_time"),
                    Tags(chapterElement),
                    sourceOrder++));
            }

            return MediaChapterReadResult.Succeeded(chapters.ToArray());
        }
        catch (JsonException exception)
        {
            return MediaChapterReadResult.Failed("FfprobeParseFailed", exception.Message, ProcessDetails(result));
        }
    }

    private static int? Int32Property(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out number))
        {
            return number;
        }

        return null;
    }

    private static long? Int64Property(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out number))
        {
            return number;
        }

        return null;
    }

    private static string? StringProperty(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static Dictionary<string, string> Tags(JsonElement element)
    {
        if (!element.TryGetProperty("tags", out var tagsElement) || tagsElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>();
        }

        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in tagsElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                tags[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return tags;
    }

    private static string ProcessDetails(ProcessRunResult result)
    {
        var stderr = string.IsNullOrWhiteSpace(result.StandardError)
            ? string.Empty
            : $" Stderr: {result.StandardError.Trim()}";
        return $"Command: {result.FileName} {string.Join(" ", result.Arguments)} ExitCode: {result.ExitCode?.ToString() ?? "<none>"}{stderr}";
    }
}
