using System.Globalization;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Media;

public sealed class MediaChapterImporter(
    IMediaChapterReader reader,
    IEnumerable<string>? supportedExtensions = null) : IChapterImporter
{
    private static readonly IReadOnlySet<string> DefaultSupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".m4a",
        ".m4v",
        ".mov",
        ".qt",
        ".3gp",
        ".3g2",
        ".asf",
        ".wmv",
        ".wma",
        ".flac",
        ".mp3",
        ".aac",
        ".ogg",
        ".oga",
        ".ogv",
        ".opus",
        ".wav",
        ".nut",
        ".aa",
        ".aax",
        ".ffmetadata",
        ".ffmeta"
    };

    public string Id => "ffprobe-media";

    public IReadOnlySet<string> SupportedExtensions { get; } = supportedExtensions is null
        ? DefaultSupportedExtensions
        : new HashSet<string>(supportedExtensions, StringComparer.OrdinalIgnoreCase);

    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        var read = await reader.ReadAsync(request.Path, cancellationToken);
        if (!read.Success)
        {
            return ChapterImportResult.Failed(new ChapterDiagnostic(
                DiagnosticSeverity.Error,
                read.DiagnosticCode ?? "MediaReadFailed",
                read.Message ?? "Media chapter reader failed.",
                Details: read.Details));
        }

        if (read.Chapters.Count == 0)
        {
            return ChapterImportResult.Failed(Error("NoChaptersFound", "No media chapters were read."));
        }

        var diagnostics = new List<ChapterDiagnostic>();
        var normalized = NormalizeEntries(read.Chapters, diagnostics);
        if (normalized.Count == 0)
        {
            diagnostics.Add(Error("InvalidChapterTimestamp", "No media chapter had a valid non-negative start timestamp."));
            return new ChapterImportResult(false, [], diagnostics);
        }

        var options = CreateOptions(request.Path, normalized);
        return new ChapterImportResult(
            true,
            [new ChapterInfoGroup(request.Path, options)],
            diagnostics);
    }

    private static IReadOnlyList<ChapterSourceOption> CreateOptions(string path, IReadOnlyList<NormalizedMediaChapter> chapters)
    {
        if (chapters.Any(static chapter => !string.IsNullOrWhiteSpace(EditionUid(chapter.Entry))))
        {
            return CreateEditionOptions(path, chapters);
        }

        var info = CreateInfo(
            Path.GetFileNameWithoutExtension(path),
            Path.GetFileName(path),
            0,
            chapters,
            renumberFallbacks: true);
        return [new ChapterSourceOption("default", "FFprobe Chapters", info, MediaReferences: [CreateReference(path)])];
    }

    private static List<ChapterSourceOption> CreateEditionOptions(string path, IReadOnlyList<NormalizedMediaChapter> chapters)
    {
        var editionKeys = new List<string>();
        foreach (var chapter in chapters)
        {
            var editionUid = EditionUid(chapter.Entry);
            if (!string.IsNullOrWhiteSpace(editionUid) && !editionKeys.Contains(editionUid, StringComparer.Ordinal))
            {
                editionKeys.Add(editionUid);
            }
        }

        var hasUntagged = chapters.Any(static chapter => string.IsNullOrWhiteSpace(EditionUid(chapter.Entry)));
        var options = new List<ChapterSourceOption>(editionKeys.Count + (hasUntagged ? 1 : 0));
        var editionIndex = 0;
        foreach (var key in editionKeys)
        {
            options.Add(CreateEditionOption(path, editionIndex++, chapters.Where(chapter => EditionUid(chapter.Entry) == key).ToList()));
        }

        if (hasUntagged)
        {
            options.Add(CreateEditionOption(path, editionIndex, chapters.Where(static chapter => string.IsNullOrWhiteSpace(EditionUid(chapter.Entry))).ToList()));
        }

        return options;
    }

    private static ChapterSourceOption CreateEditionOption(string path, int editionIndex, IReadOnlyList<NormalizedMediaChapter> chapters)
    {
        var title = $"Edition {editionIndex + 1:D2}";
        var info = CreateInfo(title, Path.GetFileName(path), editionIndex, chapters, renumberFallbacks: true);
        return new ChapterSourceOption($"edition-{editionIndex}", title, info, CanCombine: false, MediaReferences: [CreateReference(path)]);
    }

    private static ChapterInfo CreateInfo(
        string title,
        string? sourceName,
        int sourceIndex,
        IReadOnlyList<NormalizedMediaChapter> chapters,
        bool renumberFallbacks)
    {
        var ordered = chapters
            .OrderBy(static chapter => chapter.Start)
            .ThenBy(static chapter => chapter.Entry.Id ?? int.MaxValue)
            .ThenBy(static chapter => chapter.Entry.SourceOrder)
            .ToList();
        var modelChapters = ordered
            .Select((chapter, index) => new Chapter(
                index + 1,
                chapter.Start,
                ChapterName(chapter.Entry, renumberFallbacks ? index + 1 : chapter.Entry.SourceOrder + 1),
                End: chapter.End))
            .ToList();
        var duration = ordered
            .Select(static chapter => chapter.End)
            .Where(static end => end.HasValue)
            .Select(static end => end!.Value)
            .DefaultIfEmpty(TimeSpan.Zero)
            .Max();

        return new ChapterInfo(title, sourceName, sourceIndex, "MEDIA", 0, duration, modelChapters);
    }

    private static List<NormalizedMediaChapter> NormalizeEntries(
        IReadOnlyList<MediaChapterEntry> entries,
        List<ChapterDiagnostic> diagnostics)
    {
        var normalized = new List<NormalizedMediaChapter>(entries.Count);
        foreach (var entry in entries)
        {
            var start = Timestamp(entry.StartTime, entry.Start, entry.TimeBase);
            if (!start.HasValue || start.Value < TimeSpan.Zero)
            {
                diagnostics.Add(new ChapterDiagnostic(
                    DiagnosticSeverity.Warning,
                    "InvalidChapterTimestamp",
                    $"Skipped media chapter at source index {entry.SourceOrder} because it has no valid non-negative start timestamp."));
                continue;
            }

            var end = Timestamp(entry.EndTime, entry.End, entry.TimeBase);
            normalized.Add(new NormalizedMediaChapter(
                entry,
                start.Value,
                end.HasValue && end.Value > start.Value ? end : null));
        }

        return normalized;
    }

    private static TimeSpan? Timestamp(string? decimalSeconds, long? integerValue, string? timeBase)
    {
        if (!string.IsNullOrWhiteSpace(decimalSeconds)
            && decimal.TryParse(decimalSeconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= 0)
        {
            return SecondsToTimeSpan(parsed);
        }

        if (integerValue.HasValue && TryParseTimeBase(timeBase, out var numerator, out var denominator))
        {
            return SecondsToTimeSpan(integerValue.Value * numerator / denominator);
        }

        return null;
    }

    private static bool TryParseTimeBase(string? value, out decimal numerator, out decimal denominator)
    {
        numerator = 0;
        denominator = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !decimal.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out numerator)
            || !decimal.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out denominator)
            || denominator == 0)
        {
            return false;
        }

        return true;
    }

    private static TimeSpan SecondsToTimeSpan(decimal seconds) =>
        TimeSpan.FromTicks((long)decimal.Round(seconds * TimeSpan.TicksPerSecond, 0, MidpointRounding.AwayFromZero));

    private static string ChapterName(MediaChapterEntry entry, int number) =>
        TagValue(entry, "title") is { Length: > 0 } title
            ? title
            : TagValue(entry, "TITLE") is { Length: > 0 } upperTitle
                ? upperTitle
                : $"Chapter {number:D2}";

    private static string? EditionUid(MediaChapterEntry entry) => TagValue(entry, "EDITION_UID");

    private static string? TagValue(MediaChapterEntry entry, string key) =>
        entry.Tags.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static SourceMediaReference CreateReference(string path) =>
        new(Path.GetFileName(path), Path.GetFileName(path), path);

    private static ChapterDiagnostic Error(string code, string message) =>
        new(DiagnosticSeverity.Error, code, message);

    private sealed record NormalizedMediaChapter(MediaChapterEntry Entry, TimeSpan Start, TimeSpan? End);
}
