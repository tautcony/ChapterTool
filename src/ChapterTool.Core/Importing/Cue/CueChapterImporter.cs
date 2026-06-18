namespace ChapterTool.Core.Importing.Cue;

public sealed class CueChapterImporter(CueSheetParser? parser = null) : IChapterImporter
{
    private readonly CueSheetParser parser = parser ?? new CueSheetParser();

    public string Id => "cue";

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".cue"
    };

    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        byte[] bytes;
        if (request.Content is not null)
        {
            using var memory = new MemoryStream();
            await request.Content.CopyToAsync(memory, cancellationToken);
            bytes = memory.ToArray();
        }
        else
        {
            bytes = await File.ReadAllBytesAsync(request.Path, cancellationToken);
        }

        var text = CueTextDecoder.Decode(bytes);
        return CueSheetParser.Parse(text, request.Path);
    }
}
