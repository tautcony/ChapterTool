using System.Text;

namespace ChapterTool.Avalonia.Services;

public static class ChapterNameTemplateReader
{
    public static async ValueTask<string> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
