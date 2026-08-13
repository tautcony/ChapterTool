using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Importing.Text;

/// <summary>
/// Imports plain text chapter timestamps.
/// </summary>
/// <param name="timeFormatter">The chapter time formatter.</param>
public sealed class TextChapterImporter(IChapterTimeFormatter timeFormatter) : IChapterImporter
{
    private readonly PremiereMarkerListImporter premiereImporter = new(timeFormatter);
    private readonly OgmChapterImporter ogmImporter = new(timeFormatter);

    /// <summary>
    /// Gets the stable importer identifier.
    /// </summary>
    public string Id => "text";

    /// <summary>
    /// Gets the supported file extensions for this importer.
    /// </summary>
    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".txt"
    };

    /// <summary>
    /// Imports chapters from the supplied request.
    /// </summary>
    /// <param name="request">The import request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        var decoded = await TextImportUtilities.ReadTextAsync(request, cancellationToken);
        var result = premiereImporter.CanImportText(decoded.Text)
            ? premiereImporter.ImportText(decoded.Text, request.Path)
            : ogmImporter.ImportText(decoded.Text, request.Path);
        return TextImportUtilities.WithEncodingFallback(result, decoded.UsedEncodingFallback);
    }

    /// <summary>
    /// Imports chapters from text content.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="path">The source path.</param>
    /// <returns>The operation result.</returns>
    public ChapterImportResult ImportText(string text, string path = "") =>
        premiereImporter.CanImportText(text)
            ? premiereImporter.ImportText(text, path)
            : ogmImporter.ImportText(text, path);
}
