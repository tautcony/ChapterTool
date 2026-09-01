using ChapterTool.Core.Diagnostics;

#pragma warning disable SA1107, SA1501, SA1503, SA1516, SA1520

namespace ChapterTool.Core.Importing.Disc.MovieObject;

internal sealed record HdmvNavigationLimits(
    int MaximumInstructions = 100_000,
    int MaximumTransitions = 10_000,
    int MaximumCallDepth = 32,
    int MaximumEvents = 4_096,
    int MaximumVisitedStates = 100_000,
    int MaximumProfileVariants = 8);

internal sealed record HdmvPlayerProfile
{
    internal HdmvPlayerProfile(string name = "default")
    {
        Name = name;
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

    internal string Name { get; }
    internal uint[] Psr { get; }

    internal static HdmvPlayerProfile Default => new();

    internal HdmvPlayerProfile WithPsr(int index, uint value, string name)
    {
        var profile = new HdmvPlayerProfile(name);
        Array.Copy(Psr, profile.Psr, Psr.Length);
        profile.Psr[index] = value;
        return profile;
    }
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
    bool LimitReached)
{
    internal IReadOnlyList<HdmvNavigationControlEvent> ControlEvents { get; init; } = [];
}

internal sealed record HdmvNavigationControlEvent(
    string InstructionType,
    uint? PlayItemId,
    uint? MarkId,
    int SourceObject,
    int ProgramCounter,
    string PlayerProfile);

internal sealed class HdmvNavigationResolver
{
    private const uint PsrFlag = 0x80000000;
    private readonly HdmvNavigationLimits limits;

    internal HdmvNavigationResolver(HdmvNavigationLimits? limits = null) => this.limits = limits ?? new();

    internal HdmvNavigationResult Resolve(
        MovieObjectFile file,
        int objectId,
        IReadOnlyDictionary<uint, ushort>? titleObjects = null,
        HdmvPlayerProfile? profile = null,
        int titleNumber = 1)
    {
        var state = new ExecutionState(titleObjects ?? new Dictionary<uint, ushort>(), profile ?? HdmvPlayerProfile.Default);
        state.SetTitleContext(titleNumber);
        state.EnterObject(objectId, titleNumber);
        while (state.ObjectId >= 0 && state.ObjectId < file.Objects.Count)
        {
            if (state.Instructions >= limits.MaximumInstructions || state.Transitions >= limits.MaximumTransitions ||
                state.CallStack.Count > limits.MaximumCallDepth || state.Events.Count + state.ControlEvents.Count >= limits.MaximumEvents ||
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

        return new HdmvNavigationResult(state.Events, state.Diagnostics, state.LimitReached)
        {
            ControlEvents = state.ControlEvents
        };
    }

    internal HdmvNavigationResult ResolveProfileVariants(
        MovieObjectFile file,
        int objectId,
        IReadOnlyDictionary<uint, ushort>? titleObjects = null,
        int titleNumber = 1)
    {
        var psrs = HdmvProfileVariantFactory.ReadPsrIndices(file);
        var profiles = HdmvProfileVariantFactory.Create(file, limits);

        var events = new List<HdmvNavigationEvent>();
        var controls = new List<HdmvNavigationControlEvent>();
        var diagnostics = new List<ChapterDiagnostic>();
        var seenEvents = new HashSet<string>(StringComparer.Ordinal);
        var limitReached = false;
        foreach (var profile in profiles)
        {
            diagnostics.Add(new ChapterDiagnostic(
                DiagnosticSeverity.Info,
                ChapterDiagnosticCode.NavigationSource,
                $"Evaluated HDMV player profile '{profile.Name}' (PSRs: {(psrs.Count == 0 ? "none" : string.Join(",", psrs))})."));
            var result = Resolve(file, objectId, titleObjects, profile, titleNumber);
            diagnostics.AddRange(result.Diagnostics);
            limitReached |= result.LimitReached;
            controls.AddRange(result.ControlEvents);
            foreach (var item in result.Events)
            {
                var key = string.Join(':', item.PlaylistId, item.PlayItemId, item.MarkId, item.SourceTitle, item.SourceObject, item.ProgramCounter, item.InstructionType);
                if (seenEvents.Add(key)) events.Add(item);
            }
        }

        return new HdmvNavigationResult(events, diagnostics, limitReached)
        {
            ControlEvents = [.. controls.Distinct()]
        };
    }

    private void Execute(ExecutionState state, MovieObjectCommand command, int pc)
    {
        var insn = command.Instruction;
        var dst = insn.OperandCount > 0 ? state.ReadOperand(command.DestinationOperand, insn.Operand1Immediate) : 0;
        var src = insn.OperandCount > 1 ? state.ReadOperand(command.SourceOperand, insn.Operand2Immediate) : 0;
        var next = pc + 1;

        switch (insn.Group)
        {
            case 0 when ExecuteBranchGroup(state, command, dst, src, pc, ref next):
                return;
            case 1:
            {
                if (!Compare(insn.CompareOption, dst, src)) next++;
                break;
            }
            case 2:
                ExecuteSetGroup(state, insn, command, dst, src);
                break;
        }

        state.ProgramCounter = next;
    }

    private bool ExecuteBranchGroup(ExecutionState state, MovieObjectCommand command, uint dst, uint src, int pc, ref int next)
    {
        var instruction = command.Instruction;
        switch (instruction.Subgroup)
        {
            case 0:
                return ExecuteJump(state, instruction.BranchOption, dst, ref next);
            case 1:
                return ExecuteObjectBranch(state, instruction.BranchOption, dst, next);
            case 2:
                ExecutePlay(state, instruction.BranchOption, dst, src, pc, ref next);
                break;
        }

        return false;
    }

    private static bool ExecuteJump(ExecutionState state, byte option, uint dst, ref int next)
    {
        switch (option)
        {
            case 1:
                next = dst > int.MaxValue ? int.MaxValue : (int)dst;
                break;
            case 2:
                state.ProgramCounter = int.MaxValue;
                return true;
        }

        return false;
    }

    private bool ExecuteObjectBranch(ExecutionState state, byte option, uint dst, int next)
    {
        switch (option)
        {
            case 0: state.EnterObject((int)dst, -1); return true;
            case 1 when state.TitleObjects.TryGetValue(dst, out var titleObject): state.EnterObject(titleObject, (int)dst); return true;
            case 2: state.Call((int)dst, next, limits); return true;
            case 3 when state.TitleObjects.TryGetValue(dst, out var calledObject): state.Call(calledObject, next, limits); return true;
            case 4:
                if (state.Return()) return true;
                state.ProgramCounter = int.MaxValue;
                return true;
        }

        return false;
    }

    private void ExecutePlay(ExecutionState state, byte option, uint dst, uint src, int pc, ref int next)
    {
        if (state.Events.Count < limits.MaximumEvents && option <= 2)
        {
            state.Events.Add(new HdmvNavigationEvent(dst, option == 1 ? src : null, option == 2 ? src : null,
                state.TitleNumber, state.ObjectId, pc, state.Profile.Name,
                option switch { 0 => "PlayPL", 1 => "PlayPLPI", _ => "PlayPLPM" }));
            return;
        }

        if (state.ControlEvents.Count >= limits.MaximumEvents || option is < 3 or > 5) return;
        state.ControlEvents.Add(new HdmvNavigationControlEvent(
            option switch { 3 => "PlayStop", 4 => "LinkPI", _ => "LinkMK" },
            option == 4 ? dst : null, option == 5 ? dst : null,
            state.ObjectId, pc, state.Profile.Name));
        if (option == 3) next = int.MaxValue;
    }

    private static void ExecuteSetGroup(ExecutionState state, MovieObjectInstruction instruction, MovieObjectCommand command, uint dst, uint src)
    {
        switch (instruction.Subgroup)
        {
            case 0:
                ExecuteSet(state, instruction.SetOption, command, dst, src);
                break;
            case 1:
                ExecuteSetSystem(state, instruction.SetOption, dst, src);
                break;
        }
    }

    private static bool Compare(byte option, uint left, uint right) => option switch
    {
        1 => (right & ~left) == 0,
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
        if (!TryApplySetOperation(state, option, dst, src, out var newDst, out var newSrc)) return;

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

    private static bool TryApplySetOperation(ExecutionState state, byte option, uint dst, uint src, out uint newDst, out uint newSrc)
    {
        if (option is < 1 or > 15)
        {
            newDst = 0;
            newSrc = src;
            return false;
        }

        newDst = SetOperationResults[option](state, dst, src);
        newSrc = option == 2 ? dst : src;
        return true;
    }

    private static readonly Func<ExecutionState, uint, uint, uint>[] SetOperationResults =
    [
        (_, _, _) => 0,
        (_, _, src) => src,
        (_, _, src) => src,
        (_, dst, src) => SaturatingAdd(dst, src),
        (_, dst, src) => dst > src ? dst - src : 0,
        (_, dst, src) => SaturatingMultiply(dst, src),
        (_, dst, src) => src == 0 ? uint.MaxValue : dst / src,
        (_, dst, src) => src == 0 ? uint.MaxValue : dst % src,
        (state, _, src) => state.NextRandom(src),
        (_, dst, src) => dst & src,
        (_, dst, src) => dst | src,
        (_, dst, src) => dst ^ src,
        (_, dst, src) => src < 32 ? dst | (1u << (int)src) : dst,
        (_, dst, src) => src < 32 ? dst & ~(1u << (int)src) : dst,
        (_, dst, src) => src < 32 ? dst << (int)src : 0,
        (_, dst, src) => src < 32 ? dst >> (int)src : 0
    ];

    private static void ExecuteSetSystem(ExecutionState state, byte option, uint dst, uint src)
    {
        if (SetSystemOperations.TryGetValue(option, out var operation))
        {
            operation(state, dst, src);
        }
    }

    private static readonly IReadOnlyDictionary<byte, Action<ExecutionState, uint, uint>> SetSystemOperations =
        new Dictionary<byte, Action<ExecutionState, uint, uint>>
        {
            [1] = ApplySetSystemOption1,
            [2] = (state, _, src) => state.Psr[9] = src & 0xffff,
            [3] = ApplySetSystemOption3,
            [4] = Control("EnableButton"),
            [5] = Control("DisableButton"),
            [6] = ApplySetSystemOption6,
            [7] = Control("PopupOff"),
            [8] = Control("StillOn"),
            [9] = Control("StillOff"),
            [10] = (state, dst, _) => state.Psr[22] = state.Psr[22] & ~1U | dst & 1,
            [11] = Control("SetStreamSS"),
            [16] = (state, dst, _) => state.Psr[103] = dst
        };

    private static void ApplySetSystemOption1(ExecutionState state, uint dst, uint src)
    {
        if ((dst & 0x80000000) != 0) state.Psr[1] = dst >> 16 & 0x0fff;
        if ((src & 0x80000000) != 0) state.Psr[0] = src >> 16 & 0xff;
        if ((src & 0x00008000) != 0) state.Psr[3] = src & 0xff;
        if ((dst & 0x00008000) != 0) state.Psr[2] = state.Psr[2] & 0xfffff000 | dst & 0x0fff;
        state.Psr[2] = state.Psr[2] & 0x7fffffff | (dst & 0x4000) << 17;
    }

    private static void ApplySetSystemOption3(ExecutionState state, uint dst, uint src)
    {
        if ((dst & 0x80000000) != 0) state.Psr[10] = dst & 0xffff;
        if ((src & 0x80000000) != 0) state.Psr[11] = src & 0xff;
        AddControlDiagnostic(state, "SetButtonPage");
    }

    private static void ApplySetSystemOption6(ExecutionState state, uint dst, uint src)
    {
        if ((dst & 0x80000000) != 0) state.Psr[14] = state.Psr[14] & 0xffff00ff | (dst & 0xff) << 8;
        if ((src & 0x80000000) != 0) state.Psr[14] = state.Psr[14] & 0xffffff00 | src >> 16 & 0xff;
    }

    private static Action<ExecutionState, uint, uint> Control(string instruction) =>
        (state, _, _) => AddControlDiagnostic(state, instruction);

    private static void AddControlDiagnostic(ExecutionState state, string instruction) =>
        state.Diagnostics.Add(DiagnosticSeverity.Info, ChapterDiagnosticCode.NavigationSource,
            $"HDMV {instruction} was recognized as a bounded UI or playback-control instruction.");

    private static uint SaturatingAdd(uint left, uint right) => (ulong)left + right >= uint.MaxValue ? uint.MaxValue : left + right;

    private static uint SaturatingMultiply(uint left, uint right) => (ulong)left * right >= uint.MaxValue ? uint.MaxValue : left * right;

    private sealed class ExecutionState
    {
        internal ExecutionState(IReadOnlyDictionary<uint, ushort> titleObjects, HdmvPlayerProfile profile)
        {
            TitleObjects = titleObjects.ToDictionary(static pair => pair.Key, static pair => (int)pair.Value);
            Psr = [.. profile.Psr];
            Profile = profile;
            Gpr = new uint[4096];
        }

        internal int ObjectId { get; private set; } = -1;
        internal int ProgramCounter { get; set; }
        internal int TitleNumber { get; set; }
        internal int Instructions { get; set; }
        internal int Transitions { get; set; }
        internal bool LimitReached { get; set; }
        internal uint[] Psr { get; }
        internal HdmvPlayerProfile Profile { get; }
        internal uint[] Gpr { get; }
        internal Dictionary<uint, int> TitleObjects { get; }
        internal List<MovieObjectReturn> CallStack { get; } = [];
        internal HashSet<string> Visited { get; } = new(StringComparer.Ordinal);
        internal List<HdmvNavigationEvent> Events { get; } = [];
        internal List<HdmvNavigationControlEvent> ControlEvents { get; } = [];
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
