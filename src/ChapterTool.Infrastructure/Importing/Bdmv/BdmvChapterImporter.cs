using System.Text.RegularExpressions;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Text;
using ChapterTool.Core.Models;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Core.Transform;

namespace ChapterTool.Infrastructure.Importing.Bdmv;

public sealed partial class BdmvChapterImporter : IChapterImporter
{
    private readonly IExternalToolLocator toolLocator;
    private readonly IProcessRunner processRunner;
    private readonly OgmChapterImporter ogmChapterImporter;

    public BdmvChapterImporter(IExternalToolLocator toolLocator, IProcessRunner processRunner, IChapterTimeFormatter formatter)
    {
        this.toolLocator = toolLocator;
        this.processRunner = processRunner;
        ogmChapterImporter = new OgmChapterImporter(formatter);
    }

    public string Id => "bdmv";

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BDMV"
    };

    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        var playlistDirectory = Path.Combine(request.Path, "BDMV", "PLAYLIST");
        Report(request.Progress, 0.05, "Status.LoadingSource");
        if (!Directory.Exists(playlistDirectory))
        {
            return ChapterImportResult.Failed(Error("InvalidStructure", "Blu-ray BDMV/PLAYLIST directory was not found."));
        }

        Report(request.Progress, 0.10, "Status.LoadingSource.Validate");
        var location = await toolLocator.LocateAsync("eac3to", cancellationToken);
        if (!location.Found || string.IsNullOrWhiteSpace(location.Path))
        {
            return ChapterImportResult.Failed(Error("MissingDependency", location.Message ?? "eac3to was not found."));
        }

        Report(request.Progress, 0.15, "Status.LoadingSource.Discover");
        var listResult = await RunAsync(location.Path, ToolWorkingDirectory(location.Path), [request.Path, "-showall"], cancellationToken);
        if (!listResult.Success)
        {
            return ChapterImportResult.Failed([.. listResult.Diagnostics]);
        }

        var candidates = ParsePlaylistList(listResult.Text ?? string.Empty);
        if (candidates.Count == 0)
        {
            return ChapterImportResult.Failed(Error("DependencyOutputUnrecognized", "eac3to playlist output was not recognized."));
        }

        var options = new List<ChapterSourceOption>();
        var diagnostics = new List<ChapterDiagnostic>();
        var discTitle = ReadDiscTitle(request.Path);
        var chapterCandidates = candidates.Where(static candidate => candidate.HasChapters).ToArray();
        for (var candidateIndex = 0; candidateIndex < chapterCandidates.Length; candidateIndex++)
        {
            var candidate = chapterCandidates[candidateIndex];
            var baseProgress = 0.20 + candidateIndex * 0.75 / Math.Max(chapterCandidates.Length, 1);
            Report(request.Progress, baseProgress, "Status.LoadingSource.Export");

            var playlistPath = Path.Combine(playlistDirectory, candidate.MplsName);
            if (!File.Exists(playlistPath))
            {
                continue;
            }

            ChapterInfo info;
            try
            {
                info = MplsChapterImporter.ReadPlaylistInfo(
                    playlistPath,
                    discTitle,
                    candidate.SourceName,
                    candidate.Index,
                    "BDMV",
                    candidate.Duration);
            }
            catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or IOException)
            {
                continue;
            }

            var export = await ExportChaptersAsync(location.Path, request.Path, candidate.Index, cancellationToken);
            diagnostics.AddRange(export.Diagnostics);
            if (!export.Success || string.IsNullOrWhiteSpace(export.Text))
            {
                continue;
            }

            Report(request.Progress, Math.Min(baseProgress + 0.05, 0.95), "Status.LoadingSource.Parse");
            var parsed = ogmChapterImporter.ImportText(export.Text, playlistPath);
            diagnostics.AddRange(parsed.Diagnostics);
            if (!parsed.Success)
            {
                continue;
            }

            var chapterInfo = parsed.Groups
                .SelectMany(static group => group.Options)
                .Select(static option => option.ChapterInfo)
                .FirstOrDefault();
            if (chapterInfo is null || chapterInfo.Chapters.Count == 0)
            {
                diagnostics.Add(Error("NoChaptersFound", $"eac3to exported no parseable chapters for {candidate.MplsName}."));
                continue;
            }

            var bdmvInfo = info with
            {
                Chapters = chapterInfo.Chapters,
                Duration = candidate.Duration == TimeSpan.Zero ? info.Duration : candidate.Duration
            };

            options.Add(new ChapterSourceOption(
                $"playlist-{candidate.Index}",
                $"{candidate.SourceName}__{bdmvInfo.Chapters.Count}",
                bdmvInfo,
                MediaReferences: MediaReferences(candidate.SourceName)));
        }

        if (options.Count == 0)
        {
            return ChapterImportResult.Failed(FailureDiagnostics(diagnostics));
        }

        return new ChapterImportResult(true, [new ChapterInfoGroup(request.Path, options)], diagnostics);
    }

    private static void Report(IProgress<ChapterLoadProgress>? progress, double value, string message) =>
        progress?.Report(new ChapterLoadProgress(value, message));

    private async ValueTask<ProcessTextResult> RunAsync(string executable, string? workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ProcessRunResult result;
        try
        {
            result = await processRunner.RunAsync(new ProcessRunRequest(executable, arguments, workingDirectory, TimeSpan.FromSeconds(60)), cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return ProcessTextResult.Failed(Error("DependencyCannotStart", $"eac3to could not be started: {exception.Message}"));
        }

        if (result.Cancelled)
        {
            return ProcessTextResult.Failed(Error("DependencyExecutionCancelled", "eac3to was cancelled."));
        }

        if (result.TimedOut)
        {
            return ProcessTextResult.Failed(Error("DependencyExecutionTimedOut", "eac3to timed out."));
        }

        if (result.ExitCode is not 0 || (string.IsNullOrWhiteSpace(result.StandardOutput) && !string.IsNullOrWhiteSpace(result.StandardError)))
        {
            return ProcessTextResult.Failed(Error("DependencyExecutionFailed", result.StandardError.Length == 0 ? "eac3to failed." : result.StandardError));
        }

        if (result.OutputTruncated)
        {
            return ProcessTextResult.Failed(Error("DependencyOutputTruncated", "eac3to output exceeded the capture limit and cannot be parsed safely."));
        }

        return ProcessTextResult.Succeeded(result.StandardOutput);
    }

    private async ValueTask<ChapterExportResult> ExportChaptersAsync(string executable, string bdmvRoot, int titleIndex, CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"ChapterTool-eac3to-{Guid.NewGuid():N}.txt");
        try
        {
            var arguments = new[] { bdmvRoot, $"{titleIndex})", $"1:{tempPath}", "-showall" };
            ProcessRunResult result;
            try
            {
                result = await processRunner.RunAsync(
                    new ProcessRunRequest(executable, arguments, Path.GetTempPath(), TimeSpan.FromSeconds(60), RedirectOutput: false, CreateNoWindow: true),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                return new ChapterExportResult(
                    false,
                    null,
                    [Error("DependencyCannotStart", $"eac3to chapter export could not be started: {exception.Message}")]);
            }

            if (ToExecutionDiagnostic(result, "eac3to chapter export") is { } execution)
            {
                return new ChapterExportResult(false, null, [execution]);
            }

            if (!File.Exists(tempPath))
            {
                return new ChapterExportResult(
                    false,
                    null,
                    [Error("DependencyOutputMissing", "eac3to did not create a chapter export file.")]);
            }

            var text = await File.ReadAllTextAsync(tempPath, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new ChapterExportResult(false, null, [Error("DependencyOutputEmpty", "eac3to chapter export was empty.")]);
            }

            return new ChapterExportResult(true, text, []);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static ChapterDiagnostic? ToExecutionDiagnostic(ProcessRunResult result, string operation)
    {
        if (result.Cancelled)
        {
            return Error("DependencyExecutionCancelled", $"{operation} was cancelled.");
        }

        if (result.TimedOut)
        {
            return Error("DependencyExecutionTimedOut", $"{operation} timed out.");
        }

        if (result.ExitCode is not 0)
        {
            return Error("DependencyExecutionFailed", result.StandardError.Length == 0 ? $"{operation} failed." : result.StandardError);
        }

        if (result.OutputTruncated)
        {
            return Error("DependencyOutputTruncated", $"{operation} output exceeded the capture limit and cannot be parsed safely.");
        }

        return null;
    }

    private static List<PlaylistCandidate> ParsePlaylistList(string text)
    {
        var candidates = new List<PlaylistCandidate>();
        foreach (var block in PlaylistBlocks(text))
        {
            var match = PlaylistLineRegex().Match(block[0]);
            if (!match.Success)
            {
                continue;
            }

            var duration = TimeSpan.TryParse(match.Groups["Duration"].Value, out var parsedDuration) ? parsedDuration : TimeSpan.Zero;
            var sourceName = match.Groups["Source"].Value.Trim();
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                sourceName = block
                    .Skip(1)
                    .Select(static line => line.Trim())
                    .FirstOrDefault(static line => line.Contains(".m2ts", StringComparison.OrdinalIgnoreCase))
                    ?? match.Groups["Mpls"].Value;
            }

            candidates.Add(new PlaylistCandidate(
                int.Parse(match.Groups["Index"].Value),
                match.Groups["Mpls"].Value,
                sourceName,
                duration,
                block.Any(static line => line.Contains("- Chapters", StringComparison.OrdinalIgnoreCase))));
        }

        return candidates;
    }

    private static IEnumerable<IReadOnlyList<string>> PlaylistBlocks(string text)
    {
        var current = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (PlaylistHeaderRegex().IsMatch(line))
            {
                if (current.Count > 0)
                {
                    yield return current.ToArray();
                    current.Clear();
                }
            }

            if (current.Count > 0 || PlaylistHeaderRegex().IsMatch(line))
            {
                current.Add(line);
            }
        }

        if (current.Count > 0)
        {
            yield return current.ToArray();
        }
    }

    private static string ReadDiscTitle(string root)
    {
        var meta = Path.Combine(root, "BDMV", "META", "DL");
        if (!Directory.Exists(meta))
        {
            return string.Empty;
        }

        try
        {
            var file = Directory.EnumerateFiles(meta, "*.xml").FirstOrDefault();
            if (file is null)
            {
                return string.Empty;
            }

            var text = File.ReadAllText(file);
            var match = DiscTitleRegex().Match(text);
            return match.Success ? match.Groups["Title"].Value : string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static IReadOnlyList<SourceMediaReference> MediaReferences(string sourceName) =>
        sourceName
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static part => part.EndsWith(".m2ts", StringComparison.OrdinalIgnoreCase))
            .Select(static part => new SourceMediaReference(part, Path.Combine("..", "STREAM", part)))
            .ToList();

    private static string? ToolWorkingDirectory(string executable)
    {
        var directory = Path.GetDirectoryName(executable);
        return string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)
            ? null
            : directory;
    }

    private static ChapterDiagnostic[] FailureDiagnostics(IReadOnlyList<ChapterDiagnostic> diagnostics)
    {
        var errors = diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        return errors.Length == 0
            ? [Error("NoChaptersFound", "No BDMV playlists with chapters were parsed.")]
            : errors;
    }

    private static ChapterDiagnostic Error(string code, string message) =>
        new(DiagnosticSeverity.Error, code, message);

    private sealed record PlaylistCandidate(int Index, string MplsName, string SourceName, TimeSpan Duration, bool HasChapters);

    private sealed record ChapterExportResult(bool Success, string? Text, IReadOnlyList<ChapterDiagnostic> Diagnostics);

    private sealed record ProcessTextResult(bool Success, string? Text, IReadOnlyList<ChapterDiagnostic> Diagnostics)
    {
        public static ProcessTextResult Succeeded(string text) => new(true, text, []);

        public static ProcessTextResult Failed(params ChapterDiagnostic[] diagnostics) => new(false, null, diagnostics);
    }

    [GeneratedRegex(@"^\s*\d+\)\s+")]
    private static partial Regex PlaylistHeaderRegex();

    [GeneratedRegex(@"^\s*(?<Index>\d+)\)\s+(?<Mpls>.+?\.mpls)(?:\s+\([^)]+\))?,\s+(?:(?<Source>.*?\.m2ts(?:\+.*?\.m2ts)*)\s*,\s*)?(?<Duration>\d{1,2}:\d{2}:\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex PlaylistLineRegex();

    [GeneratedRegex(@"<di:name>\s*(?<Title>.*?)\s*</di:name>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DiscTitleRegex();
}
