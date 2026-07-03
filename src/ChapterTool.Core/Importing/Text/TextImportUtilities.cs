using System.Text;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Text;

internal static class TextImportUtilities
{
    public static async ValueTask<string> ReadTextAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            using var reader = new StreamReader(request.Content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        return await File.ReadAllTextAsync(request.Path, cancellationToken);
    }

    public static ChapterImportResult SingleGroup(string path, ChapterInfo info)
    {
        var option = new ChapterSourceOption("default", info.Title, info);
        var group = new ChapterInfoGroup(path, [option]);
        return ChapterImportResult.Succeeded(group);
    }
}
