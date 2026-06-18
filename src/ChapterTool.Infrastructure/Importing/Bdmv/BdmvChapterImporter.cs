using System.Text.RegularExpressions;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Services;
using ChapterTool.Core.Transform;

namespace ChapterTool.Infrastructure.Importing.Bdmv;

public sealed partial class BdmvChapterImporter : IChapterImporter
{
    private readonly IExternalToolLocator toolLocator;
    private readonly IProcessRunner processRunner;

    public BdmvChapterImporter(IExternalToolLocator toolLocator, IProcessRunner processRunner, IChapterTimeFormatter formatter)
    {
        this.toolLocator = toolLocator;
        this.processRunner = processRunner;
    }

    public string Id => "bdmv";

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BDMV"
    };

    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        var playlistDirectory = Path.Combine(request.Path, "BDMV", "PLAYLIST");
        if (!Directory.Exists(playlistDirectory))
        {
            return ChapterImportResult.Failed(Error("InvalidStructure", "Blu-ray BDMV/PLAYLIST directory was not found."));
        }

        var location = await toolLocator.LocateAsync("eac3to", cancellationToken);
        if (!location.Found || string.IsNullOrWhiteSpace(location.Path))
        {
            return ChapterImportResult.Failed(Error("MissingDependency", location.Message ?? "eac3to was not found."));
        }

        var listResult = await RunAsync(location.Path, request.Path, [request.Path, "-showall"], cancellationToken);
        if (!listResult.Success)
        {
            return listResult;
        }

        var candidates = ParsePlaylistList(listResult.Diagnostics.SingleOrDefault()?.Message ?? string.Empty);
        if (candidates.Count == 0)
        {
            return ChapterImportResult.Failed(Error("DependencyOutputUnrecognized", "eac3to playlist output was not recognized."));
        }

        var options = new List<ChapterSourceOption>();
        foreach (var candidate in candidates)
        {
            if (!candidate.HasChapters)
            {
                continue;
            }

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
                    ReadDiscTitle(request.Path),
                    candidate.SourceName,
                    candidate.Index,
                    "BDMV",
                    candidate.Duration);
            }
            catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or IOException)
            {
                continue;
            }

            options.Add(new ChapterSourceOption($"playlist-{candidate.Index}", $"{candidate.SourceName}__{info.Chapters.Count}", info));
        }

        if (options.Count == 0)
        {
            return ChapterImportResult.Failed(Error("NoChaptersFound", "No BDMV playlists with chapters were parsed."));
        }

        return new ChapterImportResult(true, [new ChapterInfoGroup(request.Path, options)], []);
    }

    private async ValueTask<ChapterImportResult> RunAsync(string executable, string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(new ProcessRunRequest(executable, arguments, workingDirectory, TimeSpan.FromSeconds(60)), cancellationToken);
        if (result.Cancelled)
        {
            return ChapterImportResult.Failed(Error("DependencyExecutionCancelled", "eac3to was cancelled."));
        }

        if (result.TimedOut)
        {
            return ChapterImportResult.Failed(Error("DependencyExecutionTimedOut", "eac3to timed out."));
        }

        if (result.ExitCode is not 0 || (string.IsNullOrWhiteSpace(result.StandardOutput) && !string.IsNullOrWhiteSpace(result.StandardError)))
        {
            return ChapterImportResult.Failed(Error("DependencyExecutionFailed", result.StandardError.Length == 0 ? "eac3to failed." : result.StandardError));
        }

        return new ChapterImportResult(true, [], [new ChapterDiagnostic(DiagnosticSeverity.Info, "Stdout", result.StandardOutput)]);
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

        var file = Directory.EnumerateFiles(meta, "*.xml").FirstOrDefault();
        if (file is null)
        {
            return string.Empty;
        }

        var text = File.ReadAllText(file);
        var match = DiscTitleRegex().Match(text);
        return match.Success ? match.Groups["Title"].Value : string.Empty;
    }

    private static ChapterDiagnostic Error(string code, string message) =>
        new(DiagnosticSeverity.Error, code, message);

    private sealed record PlaylistCandidate(int Index, string MplsName, string SourceName, TimeSpan Duration, bool HasChapters);

    [GeneratedRegex(@"^\s*\d+\)\s+")]
    private static partial Regex PlaylistHeaderRegex();

    [GeneratedRegex(@"^\s*(?<Index>\d+)\)\s+(?<Mpls>.+?\.mpls)(?:\s+\([^)]+\))?,\s+(?:(?<Source>.*?\.m2ts(?:\+.*?\.m2ts)*)\s*,\s*)?(?<Duration>\d{1,2}:\d{2}:\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex PlaylistLineRegex();

    [GeneratedRegex(@"<di:name>\s*(?<Title>.*?)\s*</di:name>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DiscTitleRegex();
}
