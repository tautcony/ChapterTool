using System.Text;
using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Importing.Cue;

/// <summary>
/// Imports embedded CUE sheet chapter data from TAK files.
/// </summary>
/// <param name="parser">The CUE sheet parser.</param>
public sealed class TakCueImporter(CueSheetParser? parser = null) : IChapterImporter
{
    private readonly CueSheetParser parser = parser ?? new CueSheetParser();

    /// <summary>
    /// Gets the stable importer identifier.
    /// </summary>
    public string Id => "tak-cue";

    /// <summary>
    /// Gets the supported file extensions for this importer.
    /// </summary>
    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".tak"
    };

    /// <summary>
    /// Imports chapters from the supplied request.
    /// </summary>
    /// <param name="request">The import request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The operation result.</returns>
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

        if (bytes.Length < 4 || Encoding.ASCII.GetString(bytes, 0, 4) != "tBaK")
        {
            return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.InvalidContainerHeader, "The file is not a TAK container."));
        }

        var cue = ExtractCue(bytes.AsSpan(Math.Min(4, bytes.Length)));
        if (cue is null)
        {
            return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.EmbeddedCueNotFound, "No TAK cuesheet marker was found."));
        }

        return CueSheetParser.Parse(cue, request.Path);
    }

    /// <summary>
    /// Executes the ExtractCue operation.
    /// </summary>
    /// <param name="data">The TAK file bytes to scan for an embedded CUE sheet.</param>
    /// <returns>The operation result.</returns>
    public static string? ExtractCue(ReadOnlySpan<byte> data)
    {
        var marker = "cuesheet"u8;
        var markerIndex = data.IndexOf(marker);
        if (markerIndex < 0)
        {
            markerIndex = IndexOfAsciiIgnoreCase(data, marker);
        }

        if (markerIndex < 0)
        {
            return null;
        }

        var begin = markerIndex + marker.Length + 1;
        if (begin >= data.Length)
        {
            return null;
        }

        var end = begin;
        var zeroCount = 0;
        for (; end < data.Length; end++)
        {
            if (data[end] == 0)
            {
                zeroCount++;
                if (zeroCount == 6)
                {
                    end -= 5;
                    break;
                }
            }
            else
            {
                zeroCount = 0;
            }
        }

        while (end > begin && (data[end - 1] == 0 || data[end - 1] == '\r' || data[end - 1] == '\n'))
        {
            end--;
        }

        var cueBytes = data[begin..end];
        return cueBytes.Length <= 10 ? null : Encoding.UTF8.GetString(cueBytes);
    }

    private static int IndexOfAsciiIgnoreCase(ReadOnlySpan<byte> data, ReadOnlySpan<byte> value)
    {
        for (var i = 0; i <= data.Length - value.Length; i++)
        {
            var matches = true;
            for (var j = 0; j < value.Length; j++)
            {
                var candidate = data[i + j];
                if (candidate is >= (byte)'A' and <= (byte)'Z')
                {
                    candidate = (byte)(candidate + 32);
                }

                if (candidate != value[j])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return i;
            }
        }

        return -1;
    }

    private static ChapterDiagnostic Error(ChapterDiagnosticCode code, string message) =>
        new(DiagnosticSeverity.Error, code, message);
}
