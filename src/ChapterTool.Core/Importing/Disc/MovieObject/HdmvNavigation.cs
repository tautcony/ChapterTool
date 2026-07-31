using ChapterTool.Core.Diagnostics;

 #pragma warning disable SA1107, SA1501, SA1503, SA1516, SA1520

namespace ChapterTool.Core.Importing.Disc.MovieObject;

internal sealed record HdmvNavigationLimits(
    int MaximumInstructions = 100_000,
    int MaximumTransitions = 10_000,
    int MaximumCallDepth = 32,
    int MaximumEvents = 4_096,
    int MaximumVisitedStates = 100_000);

internal sealed record HdmvPlayerProfile
{
    internal HdmvPlayerProfile()
    {
        Psr = new uint[128];
        Psr[0] = 1;
        Psr[1] = 0xff;
        Psr[2] = 0x0fff0fff;
        Psr[3] = 1;
        Psr[4] = 0xffff;
        Psr[5] = 0xffff;
        Psr[10] = 0xffff;
        Psr[12] = 0xff;
        Psr[13] = 0xff;
        Psr[14] = 0xffff;
        Psr[15] = 0x0000ffff;
        Psr[16] = 0xffffff;
        Psr[17] = 0xffffff;
        Psr[18] = 0xffffff;
        Psr[19] = 0xffff;
        Psr[20] = 2;
        Psr[31] = 0x02000000;
    }

    internal uint[] Psr { get; }

    internal static HdmvPlayerProfile Default => new();
}

internal sealed record HdmvNavigationEvent(
    uint PlaylistId,
    uint? PlayItemId,
    uint? MarkId,
    int SourceTitle,
    int SourceObject,
    int ProgramCounter,
    string PlayerProfile,
    string InstructionType);

internal sealed record HdmvNavigationResult(
    IReadOnlyList<HdmvNavigationEvent> Events,
    IReadOnlyList<ChapterDiagnostic> Diagnostics,
    bool LimitReached);

internal sealed class HdmvNavigationResolver
{
    private const uint PsrFlag = 0x80000000;
    private readonly HdmvNavigationLimits limits;

    internal HdmvNavigationResolver(HdmvNavigationLimits? limits = null) => this.limits = limits ?? new();

    internal HdmvNavigationResult Resolve(
        MovieObjectFile file,
        int objectId,
        IReadOnlyList<ushort>? titleObjects = null,
        HdmvPlayerProfile? profile = null,
        int titleNumber = 1)
    {
        var state = new ExecutionState(titleObjects ?? [], profile ?? HdmvPlayerProfile.Default);
        state.SetTitleContext(titleNumber);
        state.EnterObject(objectId, titleNumber);
        while (state.ObjectId >= 0 && state.ObjectId < file.Objects.Count)
        {
            if (state.Instructions >= limits.MaximumInstructions || state.Transitions >= limits.MaximumTransitions ||
                state.CallStack.Count > limits.MaximumCallDepth || state.Events.Count >= limits.MaximumEvents ||
                state.Visited.Count >= limits.MaximumVisitedStates)
            {
                state.LimitReached = true;
                state.Diagnostics.Add(DiagnosticSeverity.Warning, ChapterDiagnosticCode.NavigationLimitReached,
                    "HDMV navigation stopped at a configured execution limit.");
                break;
            }

            if (!state.Visited.Add(state.Key))
            {
                state.LimitReached = true;
                state.Diagnostics.Add(DiagnosticSeverity.Warning, ChapterDiagnosticCode.NavigationLimitReached,
                    "HDMV navigation stopped after revisiting a state.");
                break;
            }

            var obj = file.Objects[state.ObjectId];
            if (state.ProgramCounter < 0 || state.ProgramCounter >= obj.Commands.Count)
            {
                if (!state.Return()) break;
                continue;
            }

            var pc = state.ProgramCounter;
            var command = obj.Commands[pc];
            state.Instructions++;
            Execute(state, command, pc);
        }

        return new HdmvNavigationResult(state.Events, state.Diagnostics, state.LimitReached);
    }

    private void Execute(ExecutionState state, MovieObjectCommand command, int pc)
    {
        var insn = command.Instruction;
        var dst = insn.OperandCount > 0 ? state.ReadOperand(command.DestinationOperand, insn.Operand1Immediate) : 0;
        var src = insn.OperandCount > 1 ? state.ReadOperand(command.SourceOperand, insn.Operand2Immediate) : 0;
        var next = pc + 1;

        switch (insn.Group)
        {
            case 0 when insn.Subgroup == 0:
                if (insn.BranchOption == 1) next = dst > int.MaxValue ? int.MaxValue : (int)dst;
                else if (insn.BranchOption == 2) { state.ProgramCounter = int.MaxValue; return; }
                break;
            case 0 when insn.Subgroup == 1:
                switch (insn.BranchOption)
                {
                    case 0: state.EnterObject((int)dst, -1); return;
                    case 1 when state.TitleObjects.TryGetValue(dst, out var titleObject): state.EnterObject(titleObject, (int)dst); return;
                    case 2: state.Call((int)dst, next, limits); return;
                    case 3 when state.TitleObjects.TryGetValue(dst, out var calledObject): state.Call(calledObject, next, limits); return;
                    case 4:
                        if (state.Return()) return;
                        state.ProgramCounter = int.MaxValue;
                        return;
                }
                break;
            case 0 when insn.Subgroup == 2:
                if (state.Events.Count < limits.MaximumEvents && insn.BranchOption <= 2)
                {
                    state.Events.Add(new HdmvNavigationEvent(
                        dst,
                        insn.BranchOption == 1 ? src : null,
                        insn.BranchOption == 2 ? src : null,
                        state.TitleNumber,
                        state.ObjectId,
                        pc,
                        "default",
                        insn.BranchOption switch { 0 => "PlayPL", 1 => "PlayPLPI", _ => "PlayPLPM" }));
                }
                break;
            case 1:
                if (!Compare(insn.CompareOption, dst, src)) next++;
                break;
            case 2 when insn.Subgroup == 0:
                ExecuteSet(state, insn.SetOption, command, dst, src);
                break;
            case 2 when insn.Subgroup == 1:
                ExecuteSetSystem(state, insn.SetOption, dst);
                break;
        }

        state.ProgramCounter = next;
    }

    private static bool Compare(byte option, uint left, uint right) => option switch
    {
        1 => (left & ~right) != 0,
        2 => left == right,
        3 => left != right,
        4 => left >= right,
        5 => left > right,
        6 => left <= right,
        7 => left < right,
        _ => false
    };

    private static void ExecuteSet(ExecutionState state, byte option, MovieObjectCommand command, uint dst, uint src)
    {
        var newDst = dst;
        var newSrc = src;
        switch (option)
        {
            case 1: newDst = src; break;
            case 2: (newDst, newSrc) = (src, dst); break;
            case 3: newDst = SaturatingAdd(dst, src); break;
            case 4: newDst = dst > src ? dst - src : 0; break;
            case 5: newDst = SaturatingMultiply(dst, src); break;
            case 6: newDst = src == 0 ? uint.MaxValue : dst / src; break;
            case 7: newDst = src == 0 ? uint.MaxValue : dst % src; break;
            case 8: newDst = state.NextRandom(src); break;
            case 9: newDst &= src; break;
            case 10: newDst |= src; break;
            case 11: newDst ^= src; break;
            case 12: newDst |= 1u << (int)(src & 31); break;
            case 13: newDst &= ~(1u << (int)(src & 31)); break;
            case 14: newDst <<= (int)(src & 31); break;
            case 15: newDst >>= (int)(src & 31); break;
            default: return;
        }

        if (!state.WriteOperand(command.DestinationOperand, command.Instruction.Operand1Immediate, newDst))
        {
            state.Diagnostics.Add(DiagnosticSeverity.Warning, ChapterDiagnosticCode.NavigationSource,
                "HDMV normal SET attempted to write a non-writable operand.");
        }

        if (newSrc != src && !state.WriteOperand(command.SourceOperand, command.Instruction.Operand2Immediate, newSrc))
        {
            state.Diagnostics.Add(DiagnosticSeverity.Warning, ChapterDiagnosticCode.NavigationSource,
                "HDMV normal SET attempted to write a non-writable operand.");
        }
    }

    private static void ExecuteSetSystem(ExecutionState state, byte option, uint dst)
    {
        switch (option)
        {
            case 2: state.Psr[9] = dst; break;
            case 10: state.Psr[21] = dst; break;
            case 16: state.Psr[8] = dst; break;
        }
    }

    private static uint SaturatingAdd(uint left, uint right) => (ulong)left + right >= uint.MaxValue ? uint.MaxValue : left + right;

    private static uint SaturatingMultiply(uint left, uint right) => (ulong)left * right >= uint.MaxValue ? uint.MaxValue : left * right;

    private sealed class ExecutionState
    {
        internal ExecutionState(IReadOnlyList<ushort> titleObjects, HdmvPlayerProfile profile)
        {
            TitleObjects = titleObjects.Select((value, index) => (value, index)).ToDictionary(static pair => (uint)(pair.index + 1), static pair => (int)pair.value);
            Psr = profile.Psr.ToArray();
            Gpr = new uint[4096];
        }

        internal int ObjectId { get; private set; } = -1;
        internal int ProgramCounter { get; set; }
        internal int TitleNumber { get; set; }
        internal int Instructions { get; set; }
        internal int Transitions { get; set; }
        internal bool LimitReached { get; set; }
        internal uint[] Psr { get; }
        internal uint[] Gpr { get; }
        internal Dictionary<uint, int> TitleObjects { get; }
        internal List<MovieObjectReturn> CallStack { get; } = [];
        internal HashSet<string> Visited { get; } = new(StringComparer.Ordinal);
        internal List<HdmvNavigationEvent> Events { get; } = [];
        internal List<ChapterDiagnostic> Diagnostics { get; } = [];
        private ulong random = 1;

        internal string Key => string.Join(":", ObjectId, ProgramCounter, string.Join(',', Gpr.Take(32)), string.Join(',', Psr.Take(32)), string.Join(',', CallStack.Select(static c => c.ObjectId + "/" + c.ProgramCounter)));

        internal void EnterObject(int objectId, int titleNumber)
        {
            ObjectId = objectId;
            ProgramCounter = 0;
            TitleNumber = titleNumber;
            Transitions++;
        }

        internal void SetTitleContext(int titleNumber)
        {
            if (titleNumber > 0 && Psr[4] == 0xffff)
            {
                Psr[4] = (uint)titleNumber;
            }
        }

        internal void Call(int objectId, int returnPc, HdmvNavigationLimits limits)
        {
            if (CallStack.Count >= limits.MaximumCallDepth)
            {
                LimitReached = true;
                Diagnostics.Add(DiagnosticSeverity.Warning, ChapterDiagnosticCode.NavigationLimitReached, "HDMV call depth limit reached.");
                ProgramCounter = int.MaxValue;
                return;
            }

            CallStack.Add(new MovieObjectReturn(ObjectId, returnPc, TitleNumber));
            EnterObject(objectId, -1);
        }

        internal bool Return()
        {
            if (CallStack.Count == 0) return false;
            var item = CallStack[^1];
            CallStack.RemoveAt(CallStack.Count - 1);
            ObjectId = item.ObjectId;
            ProgramCounter = item.ProgramCounter;
            TitleNumber = item.TitleNumber;
            Transitions++;
            return true;
        }

        internal uint ReadOperand(uint value, bool immediate)
        {
            if (immediate) return value;
            if ((value & PsrFlag) != 0)
            {
                var index = (int)(value & 0x7f);
                return index < Psr.Length ? Psr[index] : 0;
            }

            return value < Gpr.Length ? Gpr[(int)value] : 0;
        }

        internal bool WriteOperand(uint value, bool immediate, uint result)
        {
            if (immediate || (value & PsrFlag) != 0 || value >= Gpr.Length) return false;
            Gpr[(int)value] = result;
            return true;
        }

        internal uint NextRandom(uint range)
        {
            random = random * 6364136223846793005UL + 1;
            return range == 0 ? 1 : (uint)(random >> 32) % range + 1;
        }
    }

    private sealed record MovieObjectReturn(int ObjectId, int ProgramCounter, int TitleNumber);
}

internal static class ChapterDiagnosticListExtensions
{
    internal static void Add(this List<ChapterDiagnostic> diagnostics, DiagnosticSeverity severity, ChapterDiagnosticCode code, string message) =>
        diagnostics.Add(new ChapterDiagnostic(severity, code, message));
}
