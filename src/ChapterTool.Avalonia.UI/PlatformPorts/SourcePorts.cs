using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.UI.PlatformPorts;

public interface IChapterSourcePicker
{
    ValueTask<ChapterSourceDocument?> PickSourceAsync(CancellationToken cancellationToken);

    ValueTask<ChapterSourceDocument?> FromDropAsync(object drop, CancellationToken cancellationToken);
}

public interface IChapterSourceLoader
{
    ValueTask<ChapterImportResult> LoadAsync(
        ChapterSourceDocument source,
        IChapterImportProgressReporter? progress,
        CancellationToken cancellationToken);
}

public sealed record ChapterSourceReadResult(
    ChapterSourceDocument? Source,
    IReadOnlyList<ChapterDiagnostic> Diagnostics)
{
    public bool Success => Source is not null && Diagnostics.All(static diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);

    public static ChapterSourceReadResult FromSource(ChapterSourceDocument source) => new(source, []);

    public static ChapterSourceReadResult Failed(params ChapterDiagnostic[] diagnostics) => new(null, diagnostics);
}
