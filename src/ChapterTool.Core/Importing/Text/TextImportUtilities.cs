using System.Text;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Text;

internal static class TextImportUtilities
{
    /// <summary>
    /// Executes the ReadTextAsync operation.
    /// </summary>
    /// <param name="request">The import request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The operation result.</returns>
    public static async ValueTask<string> ReadTextAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            using var reader = new StreamReader(request.Content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        return await File.ReadAllTextAsync(request.Path, cancellationToken);
    }

    /// <summary>
    /// Executes the SingleGroup operation.
    /// </summary>
    /// <param name="path">The source path.</param>
    /// <param name="info">The chapter data to process.</param>
    /// <returns>The operation result.</returns>
    public static ChapterImportResult SingleGroup(string path, ChapterSet info)
    {
        var entry = new ChapterImportEntry("default", info.Title, info);
        var group = new ChapterImportSource(path, [entry]);
        return ChapterImportResult.Succeeded(group);
    }
}
