using ChapterTool.Core.Boundaries;
using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Importing;

internal static class PortableInputReader
{
    internal static async ValueTask<byte[]?> ReadAllBytesAsync(
        ChapterImportRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            var copy = await PortableInputPolicy.CopyToBoundedMemoryAsync(request.Content, cancellationToken);
            if (copy.Exceeded || copy.Stream is null)
            {
                return null;
            }

            await using var memory = copy.Stream;
            return memory.ToArray();
        }

        var info = new FileInfo(request.Path);
        if (info.Exists && !PortableInputPolicy.IsWithinLimit(info.Length))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(request.Path, cancellationToken);
    }

    internal static ChapterDiagnostic TooLargeDiagnostic() =>
        new(
            DiagnosticSeverity.Error,
            ChapterDiagnosticCode.InputTooLarge,
            "The input exceeds the portable size limit.");
}
