namespace ChapterTool.Core.Importing;

public interface IChapterImporter
{
    string Id { get; }

    IReadOnlySet<string> SupportedExtensions { get; }

    ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken);
}
