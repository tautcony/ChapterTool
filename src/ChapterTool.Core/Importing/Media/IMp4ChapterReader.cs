namespace ChapterTool.Core.Importing.Media;

public interface IMp4ChapterReader
{
    ValueTask<Mp4ChapterReadResult> ReadAsync(string path, CancellationToken cancellationToken);
}
