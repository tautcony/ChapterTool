using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Importing.Text;

public sealed class TextChapterImporter(IChapterTimeFormatter timeFormatter) : IChapterImporter
{
    private readonly PremiereMarkerListImporter premiereImporter = new(timeFormatter);
    private readonly OgmChapterImporter ogmImporter = new(timeFormatter);

    public string Id => "text";

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".txt"
    };

    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        var text = await TextImportUtilities.ReadTextAsync(request, cancellationToken);
        if (premiereImporter.CanImportText(text))
        {
            return premiereImporter.ImportText(text, request.Path);
        }

        return ogmImporter.ImportText(text, request.Path);
    }

    public ChapterImportResult ImportText(string text, string path = "") =>
        premiereImporter.CanImportText(text)
            ? premiereImporter.ImportText(text, path)
            : ogmImporter.ImportText(text, path);
}
