using System.Buffers.Binary;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing.Disc.MovieObject;

namespace ChapterTool.Core.Tests.Importing;

public sealed class MovieObjectNavigationTests
{
    [Fact]
    public void ParserReadsCommonHeaderSectionAndInstructionFields()
    {
        var command = EncodeCommand(
            operandCount: 2,
            group: 2,
            subgroup: 0,
            operand1Immediate: false,
            operand2Immediate: true,
            setOption: 3,
            destination: 7,
            source: 9);
        using var stream = new MemoryStream(BuildMovieObject(command));

        var file = MovieObjectFile.Read(stream);

        var parsed = Assert.Single(file.Objects);
        var parsedCommand = Assert.Single(parsed.Commands);
        Assert.Equal(2, parsedCommand.Instruction.OperandCount);
        Assert.Equal(2, parsedCommand.Instruction.Group);
        Assert.Equal(3, parsedCommand.Instruction.SetOption);
        Assert.False(parsedCommand.Instruction.Operand1Immediate);
        Assert.True(parsedCommand.Instruction.Operand2Immediate);
        Assert.Equal(7U, parsedCommand.DestinationOperand);
        Assert.Equal(9U, parsedCommand.SourceOperand);
    }

    [Fact]
    public void ParserRejectsTruncatedCommandData()
    {
        var command = EncodeCommand(1, 0, 2, true, false, branchOption: 0, destination: 1);
        using var stream = new MemoryStream(BuildMovieObject(command)[..^1]);

        Assert.ThrowsAny<Exception>(() => MovieObjectFile.Read(stream));
    }

    [Fact]
    public void ParserReadsRepositoryMovieObjectFixture()
    {
        var path = Path.Combine(FixtureResolver.RepositoryRoot, "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Bdmv", "Detective Conan The Bride of Halloween", "DISC1", "BDMV", "MovieObject.bdmv");

        var file = MovieObjectFile.TryRead(path, out var error);

        Assert.True(file != null, error);
        Assert.NotEmpty(file.Objects);
        Assert.Contains(file.Objects, static movieObject => movieObject.Commands.Count > 0);
    }

    [Fact]
    public void ResolverReadsRegisterOperandsAndEmitsPlayPlEvent()
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(2, 2, 0, false, true, setOption: 1, destination: 1, source: 42),
                Command(1, 0, 2, false, false, branchOption: 0, destination: 1)
            ])
        ]);
        var result = new HdmvNavigationResolver().Resolve(file, 0);

        Assert.True(result.Events.Count > 0, string.Join("; ", result.Diagnostics.Select(static d => d.Message)));
        var emitted = Assert.Single(result.Events);
        Assert.Equal(42U, emitted.PlaylistId);
        Assert.Equal("PlayPL", emitted.InstructionType);
        Assert.False(result.LimitReached);
    }

    [Fact]
    public void ResolverSkipsPlayCommandWhenCompareIsFalse()
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(2, 2, 0, false, true, setOption: 1, destination: 0, source: 1),
                Command(2, 1, 0, false, true, compareOption: 2, destination: 0, source: 2),
                Command(1, 0, 2, true, false, branchOption: 0, destination: 10),
                Command(1, 0, 2, true, false, branchOption: 0, destination: 11)
            ])
        ]);

        var result = new HdmvNavigationResolver().Resolve(file, 0);

        var emitted = Assert.Single(result.Events);
        Assert.Equal(11U, emitted.PlaylistId);
    }

    [Theory]
    [InlineData(0b0011u, 0b0001u, 101u)]
    [InlineData(0b0001u, 0b0011u, 202u)]
    [InlineData(0b0001u, 0b0001u, 101u)]
    public void ResolverUsesDesiredSourceBitsForBitCompare(uint destination, uint source, uint expectedPlaylist)
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(2, 1, 0, true, true, compareOption: 1, destination: destination, source: source),
                Command(1, 0, 0, true, false, branchOption: 1, destination: 4),
                Command(1, 0, 0, true, false, branchOption: 1, destination: 6),
                Command(1, 0, 0, true, false, branchOption: 2),
                Command(1, 0, 2, true, false, branchOption: 0, destination: 101),
                Command(1, 0, 0, true, false, branchOption: 2),
                Command(1, 0, 2, true, false, branchOption: 0, destination: 202),
                Command(1, 0, 0, true, false, branchOption: 2)
            ])
        ]);

        var result = new HdmvNavigationResolver().Resolve(file, 0);

        Assert.Equal(expectedPlaylist, Assert.Single(result.Events).PlaylistId);
    }

    [Fact]
    public void ResolverSaturatesArithmeticAndRejectsPsrWrites()
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(2, 2, 0, false, true, setOption: 1, destination: 0, source: uint.MaxValue),
                Command(2, 2, 0, false, true, setOption: 3, destination: 0, source: 1),
                Command(2, 2, 0, false, true, setOption: 1, destination: 0x80000004, source: 7),
                Command(1, 0, 2, false, false, branchOption: 0, destination: 0)
            ])
        ]);

        var result = new HdmvNavigationResolver().Resolve(file, 0);

        Assert.True(result.Events.Count > 0, string.Join("; ", result.Diagnostics.Select(static d => d.Message)));
        Assert.Equal(uint.MaxValue, Assert.Single(result.Events).PlaylistId);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.NavigationSource);
    }

    [Fact]
    public void ResolverStopsAVisitedStateLoop()
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(1, 0, 0, true, false, branchOption: 1, destination: 0)
            ])
        ]);

        var result = new HdmvNavigationResolver(new HdmvNavigationLimits(MaximumVisitedStates: 2)).Resolve(file, 0);

        Assert.True(result.LimitReached);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.NavigationLimitReached);
    }

    [Fact]
    public void ResolverSupportsObjectJumpTitleJumpAndCallResume()
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(1, 0, 1, true, false, branchOption: 0, destination: 1)
            ]),
            new MovieObjectObject(false, false, false, [
                Command(1, 0, 2, true, false, destination: 7)
            ])
        ]);
        var jump = new HdmvNavigationResolver().Resolve(file, 0);
        Assert.Equal(7U, Assert.Single(jump.Events).PlaylistId);

        var titleJumpFile = file with
        {
            Objects =
            [
                new MovieObjectObject(false, false, false, [
                    Command(1, 0, 1, true, false, branchOption: 1, destination: 1)
                ]),
                file.Objects[1]
            ]
        };
        var titleJump = new HdmvNavigationResolver().Resolve(titleJumpFile, 0, new Dictionary<uint, ushort> { [1] = 1 });
        Assert.Equal(7U, Assert.Single(titleJump.Events).PlaylistId);

        var callFile = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(1, 0, 1, true, false, branchOption: 2, destination: 1),
                Command(1, 0, 2, true, false, destination: 8)
            ]),
            new MovieObjectObject(false, false, false, [
                Command(1, 0, 2, true, false, destination: 9),
                Command(0, 0, 1, false, false, branchOption: 4)
            ])
        ]);
        var call = new HdmvNavigationResolver().Resolve(callFile, 0);
        Assert.Equal(new uint[] { 9, 8 }, call.Events.Select(static item => item.PlaylistId));
    }

    [Fact]
    public void ResolverStopsAtInstructionAndCallDepthLimits()
    {
        var loop = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(1, 0, 0, true, false, branchOption: 1, destination: 0)
            ])
        ]);
        var instructionLimit = new HdmvNavigationResolver(new HdmvNavigationLimits(MaximumInstructions: 1)).Resolve(loop, 0);
        Assert.True(instructionLimit.LimitReached);

        var call = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(1, 0, 1, true, false, branchOption: 2, destination: 0)
            ])
        ]);
        var callLimit = new HdmvNavigationResolver(new HdmvNavigationLimits(MaximumCallDepth: 1)).Resolve(call, 0);
        Assert.True(callLimit.LimitReached);
    }

    [Fact]
    public void ResolverEvaluatesOnlyProfilesForReadPsrsAndMergesEvents()
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(1, 0, 2, false, false, destination: 0x80000009)
            ])
        ]);

        var result = new HdmvNavigationResolver(new HdmvNavigationLimits(MaximumProfileVariants: 3))
            .ResolveProfileVariants(file, 0);

        Assert.Equal(new uint[] { 0, 1, 2 }, result.Events.Select(static item => item.PlaylistId));
        Assert.Equal(["default", "psr9=1", "psr9=2"], result.Events.Select(static item => item.PlayerProfile));
        Assert.Equal(3, result.Diagnostics.Count(static diagnostic => diagnostic.Code == ChapterDiagnosticCode.NavigationSource && diagnostic.Message.StartsWith("Evaluated HDMV player profile", StringComparison.Ordinal)));
    }

    [Fact]
    public void ResolverPreservesGlobalTitleNumbersAcrossBdJEntries()
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(1, 0, 1, true, false, branchOption: 1, destination: 3)
            ]),
            new MovieObjectObject(false, false, false, [
                Command(1, 0, 2, true, false, destination: 17)
            ])
        ]);

        var titleObjects = new Dictionary<uint, ushort> { [1] = 0, [3] = 1 };
        var result = new HdmvNavigationResolver().Resolve(file, 0, titleObjects);

        Assert.Equal(17U, Assert.Single(result.Events).PlaylistId);
        Assert.Equal(3, result.Events[0].SourceTitle);
    }

    [Fact]
    public void ResolverEmitsLinkAndPlayStopControlEvents()
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(1, 0, 2, true, false, branchOption: 4, destination: 2),
                Command(1, 0, 2, true, false, branchOption: 5, destination: 3),
                Command(0, 0, 2, false, false, branchOption: 3),
                Command(1, 0, 2, true, false, destination: 99)
            ])
        ]);

        var result = new HdmvNavigationResolver().Resolve(file, 0);

        Assert.Empty(result.Events);
        Assert.Equal(["LinkPI", "LinkMK", "PlayStop"], result.ControlEvents.Select(static item => item.InstructionType));
        Assert.Equal(2U, result.ControlEvents[0].PlayItemId);
        Assert.Equal(3U, result.ControlEvents[1].MarkId);
    }

    [Theory]
    [InlineData(12, 0u, 3u, 8u)]
    [InlineData(12, 0u, 32u, 0u)]
    [InlineData(13, 3u, 1u, 1u)]
    [InlineData(13, 1u, 32u, 1u)]
    [InlineData(14, 3u, 2u, 12u)]
    [InlineData(14, 5u, 32u, 0u)]
    [InlineData(15, 8u, 1u, 4u)]
    [InlineData(15, 5u, 32u, 0u)]
    public void ResolverUsesDefinedShiftSemanticsForWideBitCounts(byte setOption, uint initial, uint count, uint expected)
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(2, 2, 0, false, true, setOption: 1, destination: 0, source: initial),
                Command(2, 2, 0, false, true, setOption: setOption, destination: 0, source: count),
                Command(1, 0, 2, false, false, branchOption: 0, destination: 0)
            ])
        ]);

        var result = new HdmvNavigationResolver().Resolve(file, 0);

        Assert.Equal(expected, Assert.Single(result.Events).PlaylistId);
    }

    [Fact]
    public void ResolverUpdatesPlaylistRelevantSetSystemRegisters()
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(2, 2, 1, true, true, setOption: 1, destination: 0x80070000),
                Command(1, 0, 2, false, false, destination: 0x80000001),
                Command(2, 2, 1, true, true, setOption: 3, destination: 0x80000005, source: 0x80000006)
            ])
        ]);

        var result = new HdmvNavigationResolver().Resolve(file, 0);

        Assert.Equal(7U, Assert.Single(result.Events).PlaylistId);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Message.Contains("SetButtonPage", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(4, "EnableButton")]
    [InlineData(5, "DisableButton")]
    [InlineData(7, "PopupOff")]
    [InlineData(8, "StillOn")]
    [InlineData(9, "StillOff")]
    [InlineData(11, "SetStreamSS")]
    public void ResolverRecognizesSetSystemControlOptions(byte setOption, string instruction)
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(2, 2, 1, true, true, setOption: setOption, destination: 0, source: 0),
                Command(1, 0, 2, true, false, branchOption: 0, destination: 1)
            ])
        ]);

        var result = new HdmvNavigationResolver().Resolve(file, 0);

        Assert.True(result.Events.Count > 0, string.Join("; ", result.Diagnostics.Select(static d => d.Message)));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains(instruction, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(2, 0x80000009, 7u, 7u)]
    [InlineData(16, 0x80000067, 5u, 5u)]
    public void ResolverUpdatesSetSystemRegistersForPlaylistSelection(byte setOption, uint playlistPsrOperand, uint registerValue, uint expectedPlaylist)
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, [
            new MovieObjectObject(false, false, false, [
                Command(2, 2, 1, true, true, setOption: setOption, destination: registerValue, source: registerValue),
                Command(1, 0, 2, false, false, branchOption: 0, destination: playlistPsrOperand)
            ])
        ]);

        var result = new HdmvNavigationResolver().Resolve(file, 0);

        Assert.Equal(expectedPlaylist, Assert.Single(result.Events).PlaylistId);
    }

    private static MovieObjectCommand Command(
        byte operandCount,
        byte group,
        byte subgroup,
        bool operand1Immediate,
        bool operand2Immediate,
        byte branchOption = 0,
        byte compareOption = 0,
        byte setOption = 0,
        uint destination = 0,
        uint source = 0) =>
        new(new MovieObjectInstruction(operandCount, group, subgroup, operand1Immediate, operand2Immediate, branchOption, compareOption, setOption), destination, source);

    private static byte[] BuildMovieObject(byte[] command)
    {
        var sectionLength = 6 + 6 + command.Length;
        var bytes = new byte[40 + 4 + sectionLength];
        "MOBJ"u8.ToArray().CopyTo(bytes, 0);
        "0100"u8.ToArray().CopyTo(bytes, 4);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(40), (uint)sectionLength);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(44), 0);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(48), 1);
        bytes[50] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(52), 1);
        command.CopyTo(bytes, 54);
        return bytes;
    }

    private static byte[] EncodeCommand(
        byte operandCount,
        byte group,
        byte subgroup,
        bool operand1Immediate,
        bool operand2Immediate,
        byte branchOption = 0,
        byte compareOption = 0,
        byte setOption = 0,
        uint destination = 0,
        uint source = 0)
    {
        var first = (uint)operandCount << 29 |
                    (uint)group << 27 |
                    (uint)subgroup << 24 |
                    (operand1Immediate ? 1U : 0U) << 23 |
                    (operand2Immediate ? 1U : 0U) << 22 |
                    (uint)branchOption << 16 |
                    (uint)compareOption << 8 |
                    setOption;
        var bytes = new byte[12];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, first);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), destination);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), source);
        return bytes;
    }
}
