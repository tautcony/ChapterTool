using System.Buffers.Binary;
using System.Text;
using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Importing.Cue;

public sealed class FlacCueImporter(CueSheetParser? parser = null) : IChapterImporter
{
    private readonly CueSheetParser parser = parser ?? new CueSheetParser();

    public string Id => "flac-cue";

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".flac"
    };

    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        await using var stream = request.Content ?? File.OpenRead(request.Path);
        string? cue;
        try
        {
            cue = ReadCue(stream);
        }
        catch (InvalidDataException exception) when (exception.Message == "InvalidContainerHeader")
        {
            return ChapterImportResult.Failed(Error("InvalidContainerHeader", "The file is not a FLAC container."));
        }

        if (cue is null)
        {
            return ChapterImportResult.Failed(Error("FlacEmbeddedCueNotFound", "No Vorbis cuesheet comment was found."));
        }

        return CueSheetParser.Parse(cue, request.Path);
    }

    private static string? ReadCue(Stream stream)
    {
        Span<byte> header = stackalloc byte[4];
        if (stream.Read(header) != 4 || Encoding.ASCII.GetString(header) != "fLaC")
        {
            throw new InvalidDataException("InvalidContainerHeader");
        }

        Span<byte> lengthBytes = stackalloc byte[3];
        while (stream.Position < stream.Length)
        {
            var blockHeader = stream.ReadByte();
            if (blockHeader < 0)
            {
                break;
            }

            if (stream.Read(lengthBytes) != 3)
            {
                break;
            }

            var isLast = (blockHeader & 0x80) != 0;
            var blockType = blockHeader & 0x7f;
            var length = (lengthBytes[0] << 16) | (lengthBytes[1] << 8) | lengthBytes[2];
            var block = new byte[length];
            if (stream.Read(block) != length)
            {
                break;
            }

            if (blockType == 4)
            {
                var cue = ReadVorbisComment(block);
                if (cue is not null)
                {
                    return cue;
                }
            }

            if (isLast)
            {
                break;
            }
        }

        return null;
    }

    private static string? ReadVorbisComment(byte[] block)
    {
        var offset = 0;
        if (!TryReadInt32(block, ref offset, out var vendorLength) || offset + vendorLength > block.Length)
        {
            return null;
        }

        offset += vendorLength;
        if (!TryReadInt32(block, ref offset, out var count))
        {
            return null;
        }

        for (var i = 0; i < count; i++)
        {
            if (!TryReadInt32(block, ref offset, out var commentLength) || offset + commentLength > block.Length)
            {
                return null;
            }

            var comment = Encoding.UTF8.GetString(block, offset, commentLength);
            offset += commentLength;
            var equals = comment.IndexOf('=');
            if (equals > 0 && string.Equals(comment[..equals], "cuesheet", StringComparison.OrdinalIgnoreCase))
            {
                return comment[(equals + 1)..];
            }
        }

        return null;
    }

    private static bool TryReadInt32(byte[] block, ref int offset, out int value)
    {
        value = 0;
        if (offset + 4 > block.Length)
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(offset, 4));
        offset += 4;
        return true;
    }

    private static ChapterDiagnostic Error(string code, string message) =>
        new(DiagnosticSeverity.Error, code, message);
}
