using System.Text.Json;
using System.Text.Json.Serialization;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing.Media;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Infrastructure.Configuration;

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
                ChapterDiagnosticCode.FfprobeMissingDependency,
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
                ChapterDiagnosticCode.FfprobeCannotStart,
                $"ffprobe could not be started: {exception.Message}");
        }

        if (result.Cancelled)
        {
            return MediaChapterReadResult.Failed(ChapterDiagnosticCode.FfprobeProcessCancelled, "ffprobe was cancelled.", ProcessDetails(result));
        }

        if (result.TimedOut)
        {
            return MediaChapterReadResult.Failed(ChapterDiagnosticCode.FfprobeProcessTimedOut, "ffprobe timed out.", ProcessDetails(result));
        }

        if (result.ExitCode is not 0)
        {
            return MediaChapterReadResult.Failed(ChapterDiagnosticCode.FfprobeProcessFailed, "ffprobe exited with a non-zero code.", ProcessDetails(result));
        }

        if (result.OutputTruncated)
        {
            return MediaChapterReadResult.Failed(ChapterDiagnosticCode.FfprobeOutputTruncated, "ffprobe output exceeded the capture limit and cannot be parsed safely.", ProcessDetails(result));
        }

        if (string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return MediaChapterReadResult.Failed(ChapterDiagnosticCode.FfprobeEmptyOutput, "ffprobe did not return chapter JSON.", ProcessDetails(result));
        }

        return Parse(result.StandardOutput, result);
    }

    private static MediaChapterReadResult Parse(string json, ProcessRunResult result)
    {
        try
        {
            var output = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.FfprobeChapterOutput);
            var rawChapters = output?.Chapters;
            if (rawChapters is null || rawChapters.Length == 0)
            {
                return MediaChapterReadResult.Failed(ChapterDiagnosticCode.FfprobeParseFailed, "ffprobe JSON did not contain a chapters array.", ProcessDetails(result));
            }

            var chapters = new List<MediaChapterEntry>();
            var sourceOrder = 0;
            foreach (var chapter in rawChapters)
            {
                var id = chapter.Id is >= int.MinValue and <= int.MaxValue ? (int?)chapter.Id : null;
                chapters.Add(new MediaChapterEntry(
                    id,
                    chapter.TimeBase,
                    chapter.Start,
                    chapter.End,
                    chapter.StartTime,
                    chapter.EndTime,
                    chapter.Tags ?? new Dictionary<string, string>(StringComparer.Ordinal),
                    sourceOrder++));
            }

            return MediaChapterReadResult.Succeeded(chapters.ToArray());
        }
        catch (JsonException exception)
        {
            return MediaChapterReadResult.Failed(ChapterDiagnosticCode.FfprobeParseFailed, exception.Message, ProcessDetails(result));
        }
    }

    private static string ProcessDetails(ProcessRunResult result)
    {
        var stderr = string.IsNullOrWhiteSpace(result.StandardError)
            ? string.Empty
            : $" Stderr: {result.StandardError.Trim()}";
        return $"Command: {result.FileName} {string.Join(" ", result.Arguments)} ExitCode: {result.ExitCode?.ToString() ?? "<none>"}{stderr}";
    }
}

internal sealed record FfprobeChapterOutput(FfprobeChapter[]? Chapters);

internal sealed record FfprobeChapter(
    long? Id,
    [property: JsonPropertyName("time_base")] string? TimeBase,
    long? Start,
    long? End,
    [property: JsonPropertyName("start_time")] string? StartTime,
    [property: JsonPropertyName("end_time")] string? EndTime,
    Dictionary<string, string>? Tags);
