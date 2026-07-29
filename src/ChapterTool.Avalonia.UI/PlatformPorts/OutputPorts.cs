using Avalonia.Controls;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Avalonia.UI.PlatformPorts;

public sealed record ChapterOutputDocument(
    string FileName,
    string MediaType,
    byte[] Content,
    IReadOnlyList<ChapterDiagnostic> Diagnostics)
{
    public long Length => Content.LongLength;
}

public interface IChapterOutputSink
{
    ValueTask<IReadOnlyList<ChapterDiagnostic>> WriteAsync(
        ChapterOutputDocument document,
        string? directory,
        CancellationToken cancellationToken);
}

public interface ISecondarySurfaceService
{
    ValueTask ShowAsync(string surfaceId, object? parameter, CancellationToken cancellationToken);

    ValueTask CloseAsync(string surfaceId, CancellationToken cancellationToken);
}

/// <summary>Exposes a secondary tool as content inside a single-view host.</summary>
public interface IInViewSecondarySurface : IWindowService
{
    Control? Content { get; }

    string? SurfaceId { get; }

    event EventHandler? ContentChanged;
}

public interface IRelatedMediaActionPort
{
    ValueTask<bool> OpenAsync(string path, CancellationToken cancellationToken);
}

public interface IExternalActionPort
{
    bool IsAvailable { get; }

    ValueTask<bool> ExecuteAsync(string actionId, object? parameter, CancellationToken cancellationToken);
}
