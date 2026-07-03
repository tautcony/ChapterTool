using ATL;
using ChapterTool.Core.Importing.Media;

namespace ChapterTool.Infrastructure.Importing.Media;

public sealed class AtlMp4ChapterReader() : IMp4ChapterReader
{
    private readonly IAtlTrackChapterSource source = new AtlTrackChapterSource();

    internal AtlMp4ChapterReader(IAtlTrackChapterSource source)
        : this()
    {
        this.source = source;
    }

    public ValueTask<Mp4ChapterReadResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(path))
        {
            return ValueTask.FromResult(Mp4ChapterReadResult.Failed("Mp4InvalidPath", "MP4 path is empty."));
        }

        try
        {
            var chapters = source.ReadChapters(path, cancellationToken);
            return ValueTask.FromResult(Normalize(chapters));
        }
        catch (FileNotFoundException ex)
        {
            return ValueTask.FromResult(Mp4ChapterReadResult.Failed("Mp4FileNotFound", ex.Message));
        }
        catch (DirectoryNotFoundException ex)
        {
            return ValueTask.FromResult(Mp4ChapterReadResult.Failed("Mp4FileNotFound", ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return ValueTask.FromResult(Mp4ChapterReadResult.Failed("Mp4FileInaccessible", ex.Message));
        }
        catch (IOException ex)
        {
            return ValueTask.FromResult(Mp4ChapterReadResult.Failed("Mp4ReadFailed", ex.Message));
        }
        catch (InvalidDataException ex)
        {
            return ValueTask.FromResult(Mp4ChapterReadResult.Failed("Mp4MalformedMetadata", ex.Message));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return ValueTask.FromResult(Mp4ChapterReadResult.Failed("Mp4UnsupportedMetadata", ex.Message));
        }
    }

    private static Mp4ChapterReadResult Normalize(IReadOnlyList<AtlChapterEntry> chapters)
    {
        if (chapters.Count == 0)
        {
            return Mp4ChapterReadResult.Succeeded();
        }

        var clips = new List<Mp4ChapterClip>(chapters.Count);
        foreach (var chapter in chapters.OrderBy(static chapter => chapter.StartTime))
        {
            if (chapter.UseOffset)
            {
                return Mp4ChapterReadResult.Failed("Mp4UnsupportedMetadata", "Offset-based MP4 chapters are not supported.");
            }

            if (chapter.EndTime <= chapter.StartTime)
            {
                return Mp4ChapterReadResult.Failed("Mp4MalformedMetadata", "MP4 chapter end time must be greater than start time.");
            }

            var title = string.IsNullOrWhiteSpace(chapter.Title)
                ? $"Chapter {clips.Count + 1:D2}"
                : chapter.Title;
            clips.Add(new Mp4ChapterClip(title, TimeSpan.FromMilliseconds(chapter.EndTime - chapter.StartTime)));
        }

        return Mp4ChapterReadResult.Succeeded(clips.ToArray());
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
