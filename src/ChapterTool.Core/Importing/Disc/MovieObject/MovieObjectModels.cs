namespace ChapterTool.Core.Importing.Disc.MovieObject;

internal sealed record MovieObjectFile(
    string TypeIndicator,
    string VersionNumber,
    uint ExtensionDataStartAddress,
    IReadOnlyList<MovieObjectObject> Objects)
{
    internal static MovieObjectFile Read(Stream stream)
    {
        if (!stream.CanSeek || stream.Length > MovieObjectParseLimits.MaximumFileLength)
        {
            throw new InvalidDataException("MovieObject file is outside the supported bounds.");
        }

        stream.Position = 0;
        var type = stream.ReadAscii(4);
        var version = stream.ReadAscii(4);
        if (type != "MOBJ" || version is not ("0100" or "0200" or "0240" or "0300"))
        {
            throw new InvalidDataException("Invalid MovieObject header.");
        }

        var extensionAddress = stream.ReadUInt32BigEndian();
        MovieObjectParseLimits.ValidateAddress(extensionAddress, stream.Length, "extension data");
        stream.SkipBytes(28);
        if (stream.Position != 40)
        {
            throw new InvalidDataException("MovieObject header is truncated.");
        }

        var sectionLength = stream.ReadUInt32BigEndian();
        if (sectionLength < 6 || sectionLength > MovieObjectParseLimits.MaximumSectionLength ||
            sectionLength > stream.Length - stream.Position)
        {
            throw new InvalidDataException("MovieObject section length is outside the supported bounds.");
        }

        using var section = MplsBoundedStream.Create(stream, sectionLength, 6, MovieObjectParseLimits.MaximumSectionLength, "MovieObject section");
        section.SkipBytes(4);
        var objectCount = section.ReadUInt16BigEndian();
        if (objectCount > MovieObjectParseLimits.MaximumObjects)
        {
            throw new InvalidDataException("MovieObject object count exceeds the supported limit.");
        }

        var objects = new List<MovieObjectObject>(objectCount);
        var totalCommands = 0;
        for (var objectIndex = 0; objectIndex < objectCount; objectIndex++)
        {
            var flags = section.ReadByteChecked();
            section.SkipBytes(1);
            var commandCount = section.ReadUInt16BigEndian();
            if (commandCount > MovieObjectParseLimits.MaximumCommandsPerObject ||
                totalCommands > MovieObjectParseLimits.MaximumCommands - commandCount ||
                commandCount > section.Remaining / 12)
            {
                throw new InvalidDataException("MovieObject command count exceeds the supported bounds.");
            }

            var commands = new List<MovieObjectCommand>(commandCount);
            for (var commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                commands.Add(MovieObjectCommand.Read(section));
            }

            totalCommands += commandCount;
            objects.Add(new MovieObjectObject(
                (flags & 0x80) != 0,
                (flags & 0x40) != 0,
                (flags & 0x20) != 0,
                commands));
        }

        section.Complete("MovieObject section");
        return new MovieObjectFile(type, version, extensionAddress, objects);
    }

    internal static MovieObjectFile? TryRead(string path, out string? error)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var result = Read(stream);
            error = null;
            return result;
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or IOException)
        {
            error = exception.Message;
            return null;
        }
    }

    internal static MovieObjectFile? TryReadPrimaryOrBackup(string primaryPath, string backupPath, out string? selectedPath, out string? error)
    {
        var primary = TryRead(primaryPath, out var primaryError);
        if (primary != null)
        {
            selectedPath = primaryPath;
            error = null;
            return primary;
        }

        var backup = TryRead(backupPath, out var backupError);
        selectedPath = backup == null ? null : backupPath;
        error = backup == null ? $"Primary: {primaryError}; Backup: {backupError}" : primaryError;
        return backup;
    }
}

internal sealed record MovieObjectObject(
    bool ResumeIntention,
    bool MenuCallMask,
    bool TitleSearchMask,
    IReadOnlyList<MovieObjectCommand> Commands);

internal sealed record MovieObjectCommand(
    MovieObjectInstruction Instruction,
    uint DestinationOperand,
    uint SourceOperand)
{
    internal static MovieObjectCommand Read(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[12];
        stream.ReadExactly(bytes);
        var first = (uint)bytes[0] << 24 | (uint)bytes[1] << 16 | (uint)bytes[2] << 8 | bytes[3];
        var instruction = new MovieObjectInstruction(
            (byte)(first >> 29 & 0x07),
            (byte)(first >> 27 & 0x03),
            (byte)(first >> 24 & 0x07),
            (first & 0x00800000) != 0,
            (first & 0x00400000) != 0,
            (byte)(first >> 16 & 0x0f),
            (byte)(first >> 8 & 0x0f),
            (byte)(first & 0x1f));
        var destination = (uint)bytes[4] << 24 | (uint)bytes[5] << 16 | (uint)bytes[6] << 8 | bytes[7];
        var source = (uint)bytes[8] << 24 | (uint)bytes[9] << 16 | (uint)bytes[10] << 8 | bytes[11];
        return new MovieObjectCommand(instruction, destination, source);
    }
}

internal sealed record MovieObjectInstruction(
    byte OperandCount,
    byte Group,
    byte Subgroup,
    bool Operand1Immediate,
    bool Operand2Immediate,
    byte BranchOption,
    byte CompareOption,
    byte SetOption);
