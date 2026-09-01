namespace ChapterTool.Core.Importing.Disc.MovieObject;

internal static class HdmvProfileVariantFactory
{
    private const uint PsrFlag = 0x80000000;

    internal static IReadOnlyList<HdmvPlayerProfile> Create(MovieObjectFile file, HdmvNavigationLimits limits)
    {
        var defaultProfile = HdmvPlayerProfile.Default;
        var psrs = ReadPsrIndices(file);
        var profiles = new List<HdmvPlayerProfile> { defaultProfile };
        foreach (var psr in psrs)
        {
            foreach (var value in VariantValues(psr, defaultProfile.Psr[psr]))
            {
                if (profiles.Count >= limits.MaximumProfileVariants)
                {
                    break;
                }

                if (value == defaultProfile.Psr[psr])
                {
                    continue;
                }

                profiles.Add(defaultProfile.WithPsr(psr, value, $"psr{psr}={value}"));
            }

            if (profiles.Count >= limits.MaximumProfileVariants)
            {
                break;
            }
        }

        return profiles;
    }

    internal static IReadOnlyList<int> ReadPsrIndices(MovieObjectFile file)
    {
        var result = new SortedSet<int>();
        foreach (var command in file.Objects.SelectMany(static item => item.Commands))
        {
            var instruction = command.Instruction;
            if (instruction is { OperandCount: > 0, Operand1Immediate: false } && (command.DestinationOperand & PsrFlag) != 0)
            {
                result.Add((int)(command.DestinationOperand & 0x7f));
            }

            if (instruction is { OperandCount: > 1, Operand2Immediate: false } && (command.SourceOperand & PsrFlag) != 0)
            {
                result.Add((int)(command.SourceOperand & 0x7f));
            }
        }

        return [.. result.Where(static index => index < 128)];
    }

    private static IReadOnlyList<uint> VariantValues(int psr, uint current) => psr switch
    {
        8 => [0, 1],
        9 => [1, 2],
        10 or 12 or 13 or 14 or 15 => [0, 1],
        20 => [1, 2],
        _ => [current]
    };
}
