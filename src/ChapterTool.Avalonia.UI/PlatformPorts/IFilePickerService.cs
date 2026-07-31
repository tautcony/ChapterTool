namespace ChapterTool.Avalonia.UI.PlatformPorts;

using ChapterTool.Core.Session;

public interface IFilePickerService
{
    ValueTask<string?> PickSourceAsync(CancellationToken cancellationToken);

    ValueTask<string?> PickMplsAsync(CancellationToken cancellationToken);

    ValueTask<string?> PickChapterNameTemplateAsync(CancellationToken cancellationToken);

    ValueTask<string?> PickLuaExpressionScriptAsync(CancellationToken cancellationToken);

    async ValueTask<ChapterSourceDocument?> PickSourceDocumentAsync(CancellationToken cancellationToken)
    {
        var path = await PickSourceAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(path) ? null : new LocalPathChapterSource(path);
    }

    async ValueTask<ChapterSourceDocument?> PickMplsDocumentAsync(CancellationToken cancellationToken)
    {
        var path = await PickMplsAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(path) ? null : new LocalPathChapterSource(path);
    }

    ValueTask<ChapterSourceDocument?> ConvertDropAsync(object drop, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ChapterSourceDocument?>(null);
}
