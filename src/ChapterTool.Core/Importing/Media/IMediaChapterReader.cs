namespace ChapterTool.Core.Importing.Media;

public interface IMediaChapterReader
{
    ValueTask<MediaChapterReadResult> ReadAsync(string path, CancellationToken cancellationToken);
}
