using System.Buffers.Binary;
using System.Text;
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
        Assert.NotEmpty(file!.Objects);
        Assert.Contains(file.Objects, static movieObject => movieObject.Commands.Count > 0);
    }

    [Fact]
    public void ResolverReadsRegisterOperandsAndEmitsPlayPlEvent()
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, new[]
        {
            new MovieObjectObject(false, false, false, new[]
            {
                Command(2, 2, 0, false, true, setOption: 1, destination: 1, source: 42),
                Command(1, 0, 2, false, false, branchOption: 0, destination: 1),
            })
        });
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
        var file = new MovieObjectFile("MOBJ", "0100", 0, new[]
        {
            new MovieObjectObject(false, false, false, new[]
            {
                Command(2, 2, 0, false, true, setOption: 1, destination: 0, source: 1),
                Command(2, 1, 0, false, true, compareOption: 2, destination: 0, source: 2),
                Command(1, 0, 2, true, false, branchOption: 0, destination: 10),
                Command(1, 0, 2, true, false, branchOption: 0, destination: 11),
            })
        });

        var result = new HdmvNavigationResolver().Resolve(file, 0);

        var emitted = Assert.Single(result.Events);
        Assert.Equal(11U, emitted.PlaylistId);
    }

    [Fact]
    public void ResolverSaturatesArithmeticAndRejectsPsrWrites()
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, new[]
        {
            new MovieObjectObject(false, false, false, new[]
            {
                Command(2, 2, 0, false, true, setOption: 1, destination: 0, source: uint.MaxValue),
                Command(2, 2, 0, false, true, setOption: 3, destination: 0, source: 1),
                Command(2, 2, 0, false, true, setOption: 1, destination: 0x80000004, source: 7),
                Command(1, 0, 2, false, false, branchOption: 0, destination: 0),
            })
        });

        var result = new HdmvNavigationResolver().Resolve(file, 0);

        Assert.True(result.Events.Count > 0, string.Join("; ", result.Diagnostics.Select(static d => d.Message)));
        Assert.Equal(uint.MaxValue, Assert.Single(result.Events).PlaylistId);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.NavigationSource);
    }

    [Fact]
    public void ResolverStopsAVisitedStateLoop()
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, new[]
        {
            new MovieObjectObject(false, false, false, new[]
            {
                Command(1, 0, 0, true, false, branchOption: 1, destination: 0),
            })
        });

        var result = new HdmvNavigationResolver(new HdmvNavigationLimits(MaximumVisitedStates: 2)).Resolve(file, 0);

        Assert.True(result.LimitReached);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.NavigationLimitReached);
    }

    [Fact]
    public void ResolverSupportsObjectJumpTitleJumpAndCallResume()
    {
        var file = new MovieObjectFile("MOBJ", "0100", 0, new[]
        {
            new MovieObjectObject(false, false, false, new[]
            {
                Command(1, 0, 1, true, false, branchOption: 0, destination: 1),
            }),
            new MovieObjectObject(false, false, false, new[]
            {
                Command(1, 0, 2, true, false, destination: 7),
            })
        });
        var jump = new HdmvNavigationResolver().Resolve(file, 0);
        Assert.Equal(7U, Assert.Single(jump.Events).PlaylistId);

        var titleJumpFile = file with
        {
            Objects = new[]
            {
                new MovieObjectObject(false, false, false, new[]
                {
                    Command(1, 0, 1, true, false, branchOption: 1, destination: 1),
                }),
                file.Objects[1]
            }
        };
        var titleJump = new HdmvNavigationResolver().Resolve(titleJumpFile, 0, new ushort[] { 1 });
        Assert.Equal(7U, Assert.Single(titleJump.Events).PlaylistId);

        var callFile = new MovieObjectFile("MOBJ", "0100", 0, new[]
        {
            new MovieObjectObject(false, false, false, new[]
            {
                Command(1, 0, 1, true, false, branchOption: 2, destination: 1),
                Command(1, 0, 2, true, false, destination: 8),
            }),
            new MovieObjectObject(false, false, false, new[]
            {
                Command(1, 0, 2, true, false, destination: 9),
                Command(0, 0, 1, false, false, branchOption: 4),
            })
        });
        var call = new HdmvNavigationResolver().Resolve(callFile, 0);
        Assert.Equal(new uint[] { 9, 8 }, call.Events.Select(static item => item.PlaylistId));
    }

    [Fact]
    public void ResolverStopsAtInstructionAndCallDepthLimits()
    {
        var loop = new MovieObjectFile("MOBJ", "0100", 0, new[]
        {
            new MovieObjectObject(false, false, false, new[]
            {
                Command(1, 0, 0, true, false, branchOption: 1, destination: 0),
            })
        });
        var instructionLimit = new HdmvNavigationResolver(new HdmvNavigationLimits(MaximumInstructions: 1)).Resolve(loop, 0);
        Assert.True(instructionLimit.LimitReached);

        var call = new MovieObjectFile("MOBJ", "0100", 0, new[]
        {
            new MovieObjectObject(false, false, false, new[]
            {
                Command(1, 0, 1, true, false, branchOption: 2, destination: 0),
            })
        });
        var callLimit = new HdmvNavigationResolver(new HdmvNavigationLimits(MaximumCallDepth: 1)).Resolve(call, 0);
        Assert.True(callLimit.LimitReached);
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
        Encoding.ASCII.GetBytes("MOBJ").CopyTo(bytes, 0);
        Encoding.ASCII.GetBytes("0100").CopyTo(bytes, 4);
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
