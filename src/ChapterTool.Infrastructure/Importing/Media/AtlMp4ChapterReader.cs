using ATL;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing.Media;

namespace ChapterTool.Infrastructure.Importing.Media;

public sealed class AtlMp4ChapterReader() : IMediaChapterReader
{
    private readonly IAtlTrackChapterSource source = new AtlTrackChapterSource();

    internal AtlMp4ChapterReader(IAtlTrackChapterSource source)
        : this()
    {
        this.source = source;
    }

    public ValueTask<MediaChapterReadResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(path))
        {
            return ValueTask.FromResult(MediaChapterReadResult.Failed(ChapterDiagnosticCode.Mp4InvalidPath, "MP4 path is empty."));
        }

        try
        {
            var chapters = source.ReadChapters(path, cancellationToken);
            return ValueTask.FromResult(Normalize(chapters));
        }
        catch (FileNotFoundException ex)
        {
            return ValueTask.FromResult(MediaChapterReadResult.Failed(ChapterDiagnosticCode.Mp4FileNotFound, ex.Message));
        }
        catch (DirectoryNotFoundException ex)
        {
            return ValueTask.FromResult(MediaChapterReadResult.Failed(ChapterDiagnosticCode.Mp4FileNotFound, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return ValueTask.FromResult(MediaChapterReadResult.Failed(ChapterDiagnosticCode.Mp4FileInaccessible, ex.Message));
        }
        catch (IOException ex)
        {
            return ValueTask.FromResult(MediaChapterReadResult.Failed(ChapterDiagnosticCode.Mp4ReadFailed, ex.Message));
        }
        catch (InvalidDataException ex)
        {
            return ValueTask.FromResult(MediaChapterReadResult.Failed(ChapterDiagnosticCode.Mp4MalformedMetadata, ex.Message));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return ValueTask.FromResult(MediaChapterReadResult.Failed(ChapterDiagnosticCode.Mp4UnsupportedMetadata, ex.Message));
        }
    }

    private static MediaChapterReadResult Normalize(IReadOnlyList<AtlChapterEntry> chapters)
    {
        if (chapters.Count == 0)
        {
            return MediaChapterReadResult.Succeeded();
        }

        var entries = new List<MediaChapterEntry>(chapters.Count);
        foreach (var chapter in chapters.OrderBy(static chapter => chapter.StartTime))
        {
            if (chapter.UseOffset)
            {
                return MediaChapterReadResult.Failed(ChapterDiagnosticCode.Mp4UnsupportedMetadata, "Offset-based MP4 chapters are not supported.");
            }

            if (chapter.EndTime <= chapter.StartTime)
            {
                return MediaChapterReadResult.Failed(ChapterDiagnosticCode.Mp4MalformedMetadata, "MP4 chapter end time must be greater than start time.");
            }

            var title = string.IsNullOrWhiteSpace(chapter.Title)
                ? $"Chapter {entries.Count + 1:D2}"
                : chapter.Title;
            entries.Add(new MediaChapterEntry(
                entries.Count,
                "1/1000",
                chapter.StartTime,
                chapter.EndTime,
                null,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["title"] = title },
                entries.Count));
        }

        return MediaChapterReadResult.Succeeded(entries.ToArray());
    }
}

internal interface IAtlTrackChapterSource
{
    IReadOnlyList<AtlChapterEntry> ReadChapters(string path, CancellationToken cancellationToken);
}

internal sealed class AtlTrackChapterSource : IAtlTrackChapterSource
{
    public IReadOnlyList<AtlChapterEntry> ReadChapters(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var track = new Track(path);
        return track.Chapters
            .Select(static chapter => new AtlChapterEntry(
                chapter.Title,
                chapter.StartTime,
                chapter.EndTime,
                chapter.UseOffset))
            .ToArray();
    }
}

internal sealed record AtlChapterEntry(
    string? Title,
    uint StartTime,
    uint EndTime,
    bool UseOffset);
