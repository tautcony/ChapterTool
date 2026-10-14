namespace ChapterTool.Core.Diagnostics;

/// <summary>
/// Identifies a diagnostic by the component that produced it and the failure or status reason.
/// </summary>
/// <param name="Source">The component or data source associated with the diagnostic.</param>
/// <param name="Reason">The failure or status reason.</param>
public readonly record struct ChapterDiagnosticCode(ChapterDiagnosticSource Source, ChapterDiagnosticReason Reason)
{
    /// <summary>Returns the stable display code for this diagnostic.</summary>
    public override string ToString() => this.ToDisplayCode();

    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Dependency"/> with reason <see cref="ChapterDiagnosticReason.CannotStart"/>.</summary>
    public static readonly ChapterDiagnosticCode DependencyCannotStart = new(ChapterDiagnosticSource.Dependency, ChapterDiagnosticReason.CannotStart);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.DependencyExecution"/> with reason <see cref="ChapterDiagnosticReason.Cancelled"/>.</summary>
    public static readonly ChapterDiagnosticCode DependencyExecutionCancelled = new(ChapterDiagnosticSource.DependencyExecution, ChapterDiagnosticReason.Cancelled);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.DependencyExecution"/> with reason <see cref="ChapterDiagnosticReason.Failed"/>.</summary>
    public static readonly ChapterDiagnosticCode DependencyExecutionFailed = new(ChapterDiagnosticSource.DependencyExecution, ChapterDiagnosticReason.Failed);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.DependencyExecution"/> with reason <see cref="ChapterDiagnosticReason.TimedOut"/>.</summary>
    public static readonly ChapterDiagnosticCode DependencyExecutionTimedOut = new(ChapterDiagnosticSource.DependencyExecution, ChapterDiagnosticReason.TimedOut);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.DependencyOutput"/> with reason <see cref="ChapterDiagnosticReason.Empty"/>.</summary>
    public static readonly ChapterDiagnosticCode DependencyOutputEmpty = new(ChapterDiagnosticSource.DependencyOutput, ChapterDiagnosticReason.Empty);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.DependencyOutput"/> with reason <see cref="ChapterDiagnosticReason.Missing"/>.</summary>
    public static readonly ChapterDiagnosticCode DependencyOutputMissing = new(ChapterDiagnosticSource.DependencyOutput, ChapterDiagnosticReason.Missing);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.DependencyOutput"/> with reason <see cref="ChapterDiagnosticReason.Truncated"/>.</summary>
    public static readonly ChapterDiagnosticCode DependencyOutputTruncated = new(ChapterDiagnosticSource.DependencyOutput, ChapterDiagnosticReason.Truncated);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.DependencyOutput"/> with reason <see cref="ChapterDiagnosticReason.Unrecognized"/>.</summary>
    public static readonly ChapterDiagnosticCode DependencyOutputUnrecognized = new(ChapterDiagnosticSource.DependencyOutput, ChapterDiagnosticReason.Unrecognized);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.CueEncoding"/> with reason <see cref="ChapterDiagnosticReason.Unrecognized"/>.</summary>
    public static readonly ChapterDiagnosticCode CueEncodingFallback = new(ChapterDiagnosticSource.CueEncoding, ChapterDiagnosticReason.Unrecognized);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.TextEncoding"/> with reason <see cref="ChapterDiagnosticReason.Unrecognized"/>.</summary>
    public static readonly ChapterDiagnosticCode TextEncodingFallback = new(ChapterDiagnosticSource.TextEncoding, ChapterDiagnosticReason.Unrecognized);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.EmbeddedCue"/> with reason <see cref="ChapterDiagnosticReason.NotFound"/>.</summary>
    public static readonly ChapterDiagnosticCode EmbeddedCueNotFound = new(ChapterDiagnosticSource.EmbeddedCue, ChapterDiagnosticReason.NotFound);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Chapters"/> with reason <see cref="ChapterDiagnosticReason.Empty"/>.</summary>
    public static readonly ChapterDiagnosticCode EmptyChapters = new(ChapterDiagnosticSource.Chapters, ChapterDiagnosticReason.Empty);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.CueFile"/> with reason <see cref="ChapterDiagnosticReason.Empty"/>.</summary>
    public static readonly ChapterDiagnosticCode EmptyCueFile = new(ChapterDiagnosticSource.CueFile, ChapterDiagnosticReason.Empty);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Xml"/> with reason <see cref="ChapterDiagnosticReason.Empty"/>.</summary>
    public static readonly ChapterDiagnosticCode EmptyXml = new(ChapterDiagnosticSource.Xml, ChapterDiagnosticReason.Empty);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Ffprobe"/> with reason <see cref="ChapterDiagnosticReason.CannotStart"/>.</summary>
    public static readonly ChapterDiagnosticCode FfprobeCannotStart = new(ChapterDiagnosticSource.Ffprobe, ChapterDiagnosticReason.CannotStart);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.FfprobeOutput"/> with reason <see cref="ChapterDiagnosticReason.Empty"/>.</summary>
    public static readonly ChapterDiagnosticCode FfprobeEmptyOutput = new(ChapterDiagnosticSource.FfprobeOutput, ChapterDiagnosticReason.Empty);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Ffprobe"/> with reason <see cref="ChapterDiagnosticReason.MissingDependency"/>.</summary>
    public static readonly ChapterDiagnosticCode FfprobeMissingDependency = new(ChapterDiagnosticSource.Ffprobe, ChapterDiagnosticReason.MissingDependency);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.FfprobeOutput"/> with reason <see cref="ChapterDiagnosticReason.Truncated"/>.</summary>
    public static readonly ChapterDiagnosticCode FfprobeOutputTruncated = new(ChapterDiagnosticSource.FfprobeOutput, ChapterDiagnosticReason.Truncated);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Ffprobe"/> with reason <see cref="ChapterDiagnosticReason.ParseFailed"/>.</summary>
    public static readonly ChapterDiagnosticCode FfprobeParseFailed = new(ChapterDiagnosticSource.Ffprobe, ChapterDiagnosticReason.ParseFailed);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.FfprobeProcess"/> with reason <see cref="ChapterDiagnosticReason.Cancelled"/>.</summary>
    public static readonly ChapterDiagnosticCode FfprobeProcessCancelled = new(ChapterDiagnosticSource.FfprobeProcess, ChapterDiagnosticReason.Cancelled);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.FfprobeProcess"/> with reason <see cref="ChapterDiagnosticReason.Failed"/>.</summary>
    public static readonly ChapterDiagnosticCode FfprobeProcessFailed = new(ChapterDiagnosticSource.FfprobeProcess, ChapterDiagnosticReason.Failed);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.FfprobeProcess"/> with reason <see cref="ChapterDiagnosticReason.TimedOut"/>.</summary>
    public static readonly ChapterDiagnosticCode FfprobeProcessTimedOut = new(ChapterDiagnosticSource.FfprobeProcess, ChapterDiagnosticReason.TimedOut);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.FlacEmbeddedCue"/> with reason <see cref="ChapterDiagnosticReason.NotFound"/>.</summary>
    public static readonly ChapterDiagnosticCode FlacEmbeddedCueNotFound = new(ChapterDiagnosticSource.FlacEmbeddedCue, ChapterDiagnosticReason.NotFound);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ImporterFallback"/> with reason <see cref="ChapterDiagnosticReason.Used"/>.</summary>
    public static readonly ChapterDiagnosticCode ImporterFallbackUsed = new(ChapterDiagnosticSource.ImporterFallback, ChapterDiagnosticReason.Used);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Input"/> with reason <see cref="ChapterDiagnosticReason.NotFound"/>.</summary>
    public static readonly ChapterDiagnosticCode InputNotFound = new(ChapterDiagnosticSource.Input, ChapterDiagnosticReason.NotFound);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Input"/> with reason <see cref="ChapterDiagnosticReason.Failed"/>.</summary>
    public static readonly ChapterDiagnosticCode InputReadFailed = new(ChapterDiagnosticSource.Input, ChapterDiagnosticReason.Failed);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Input"/> with reason <see cref="ChapterDiagnosticReason.Truncated"/>.</summary>
    public static readonly ChapterDiagnosticCode InputTooLarge = new(ChapterDiagnosticSource.Input, ChapterDiagnosticReason.Truncated);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Input"/> with reason <see cref="ChapterDiagnosticReason.Empty"/>.</summary>
    public static readonly ChapterDiagnosticCode InputEmpty = new(ChapterDiagnosticSource.Input, ChapterDiagnosticReason.Empty);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ChapterIndex"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidChapterIndex = new(ChapterDiagnosticSource.ChapterIndex, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ChapterPair"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidChapterPair = new(ChapterDiagnosticSource.ChapterPair, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ChapterText"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidChapterText = new(ChapterDiagnosticSource.ChapterText, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ChapterTimestamp"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidChapterTimestamp = new(ChapterDiagnosticSource.ChapterTimestamp, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Clpi"/> with reason <see cref="ChapterDiagnosticReason.NotFound"/>.</summary>
    public static readonly ChapterDiagnosticCode ClpiFileNotFound = new(ChapterDiagnosticSource.Clpi, ChapterDiagnosticReason.NotFound);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Clpi"/> with reason <see cref="ChapterDiagnosticReason.Available"/>.</summary>
    public static readonly ChapterDiagnosticCode ClpiFileLoaded = new(ChapterDiagnosticSource.Clpi, ChapterDiagnosticReason.Available);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Clpi"/> with reason <see cref="ChapterDiagnosticReason.ParseFailed"/>.</summary>
    public static readonly ChapterDiagnosticCode ClpiParseFailed = new(ChapterDiagnosticSource.Clpi, ChapterDiagnosticReason.ParseFailed);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Bdjo"/> with reason <see cref="ChapterDiagnosticReason.Unsupported"/>.</summary>
    public static readonly ChapterDiagnosticCode UnsupportedDynamicBdJNavigation = new(ChapterDiagnosticSource.Bdjo, ChapterDiagnosticReason.Unsupported);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Navigation"/> with reason <see cref="ChapterDiagnosticReason.Limit"/>.</summary>
    public static readonly ChapterDiagnosticCode NavigationLimitReached = new(ChapterDiagnosticSource.Navigation, ChapterDiagnosticReason.Limit);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.MovieObject"/> with reason <see cref="ChapterDiagnosticReason.ParseFailed"/>.</summary>
    public static readonly ChapterDiagnosticCode MovieObjectParseFailed = new(ChapterDiagnosticSource.MovieObject, ChapterDiagnosticReason.ParseFailed);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Navigation"/> with reason <see cref="ChapterDiagnosticReason.Available"/>.</summary>
    public static readonly ChapterDiagnosticCode NavigationSource = new(ChapterDiagnosticSource.Navigation, ChapterDiagnosticReason.Available);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.BdmvScan"/> with reason <see cref="ChapterDiagnosticReason.Available"/>.</summary>
    public static readonly ChapterDiagnosticCode BdmvScanCandidate = new(ChapterDiagnosticSource.BdmvScan, ChapterDiagnosticReason.Available);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.BdmvScan"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode BdmvScanRejected = new(ChapterDiagnosticSource.BdmvScan, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.InputLayout"/> with reason <see cref="ChapterDiagnosticReason.Available"/>.</summary>
    public static readonly ChapterDiagnosticCode BdmvInputLayout = new(ChapterDiagnosticSource.InputLayout, ChapterDiagnosticReason.Available);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.InputLayout"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode BdmvInputRejected = new(ChapterDiagnosticSource.InputLayout, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Parse"/> with reason <see cref="ChapterDiagnosticReason.Available"/>.</summary>
    public static readonly ChapterDiagnosticCode ParseInfo = new(ChapterDiagnosticSource.Parse, ChapterDiagnosticReason.Available);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ContainerHeader"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidContainerHeader = new(ChapterDiagnosticSource.ContainerHeader, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.EntryElement"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidEntryElement = new(ChapterDiagnosticSource.EntryElement, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Expression"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpression = new(ChapterDiagnosticSource.Expression, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Expression"/> with reason <see cref="ChapterDiagnosticReason.Incomplete"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionIncomplete = new(ChapterDiagnosticSource.Expression, ChapterDiagnosticReason.Incomplete);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Expression"/> with reason <see cref="ChapterDiagnosticReason.InsufficientOperands"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionInsufficientOperands = new(ChapterDiagnosticSource.Expression, ChapterDiagnosticReason.InsufficientOperands);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionCharacter"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionInvalidCharacter = new(ChapterDiagnosticSource.ExpressionCharacter, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.LuaExpression"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionLua = new(ChapterDiagnosticSource.LuaExpression, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.LuaExpression"/> with reason <see cref="ChapterDiagnosticReason.Cancelled"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionLuaCanceled = new(ChapterDiagnosticSource.LuaExpression, ChapterDiagnosticReason.Cancelled);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.LuaExpression"/> with reason <see cref="ChapterDiagnosticReason.CompileFailed"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionLuaCompile = new(ChapterDiagnosticSource.LuaExpression, ChapterDiagnosticReason.CompileFailed);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.LuaExpressionReturn"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionLuaInvalidReturn = new(ChapterDiagnosticSource.LuaExpressionReturn, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.LuaExpressionReturn"/> with reason <see cref="ChapterDiagnosticReason.Missing"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionLuaMissingReturn = new(ChapterDiagnosticSource.LuaExpressionReturn, ChapterDiagnosticReason.Missing);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.LuaExpression"/> with reason <see cref="ChapterDiagnosticReason.RuntimeFailed"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionLuaRuntime = new(ChapterDiagnosticSource.LuaExpression, ChapterDiagnosticReason.RuntimeFailed);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.LuaExpressionToken"/> with reason <see cref="ChapterDiagnosticReason.Unknown"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionLuaUnknownToken = new(ChapterDiagnosticSource.LuaExpressionToken, ChapterDiagnosticReason.Unknown);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionComma"/> with reason <see cref="ChapterDiagnosticReason.Misplaced"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionMisplacedComma = new(ChapterDiagnosticSource.ExpressionComma, ChapterDiagnosticReason.Misplaced);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionOperandBeforeParen"/> with reason <see cref="ChapterDiagnosticReason.Missing"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionMissingOperandBeforeParen = new(ChapterDiagnosticSource.ExpressionOperandBeforeParen, ChapterDiagnosticReason.Missing);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionOperator"/> with reason <see cref="ChapterDiagnosticReason.Missing"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionMissingOperator = new(ChapterDiagnosticSource.ExpressionOperator, ChapterDiagnosticReason.Missing);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionOperatorBeforeFunction"/> with reason <see cref="ChapterDiagnosticReason.Missing"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionMissingOperatorBeforeFunction = new(ChapterDiagnosticSource.ExpressionOperatorBeforeFunction, ChapterDiagnosticReason.Missing);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionOperatorBeforeParen"/> with reason <see cref="ChapterDiagnosticReason.Missing"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionMissingOperatorBeforeParen = new(ChapterDiagnosticSource.ExpressionOperatorBeforeParen, ChapterDiagnosticReason.Missing);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionOperator"/> with reason <see cref="ChapterDiagnosticReason.RequiresLeftOperand"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionOperatorRequiresLeftOperand = new(ChapterDiagnosticSource.ExpressionOperator, ChapterDiagnosticReason.RequiresLeftOperand);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionTernaryCondition"/> with reason <see cref="ChapterDiagnosticReason.Missing"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionTernaryMissingCondition = new(ChapterDiagnosticSource.ExpressionTernaryCondition, ChapterDiagnosticReason.Missing);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionTernaryTrueExpression"/> with reason <see cref="ChapterDiagnosticReason.Missing"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionTernaryMissingTrueExpression = new(ChapterDiagnosticSource.ExpressionTernaryTrueExpression, ChapterDiagnosticReason.Missing);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionTernaryColon"/> with reason <see cref="ChapterDiagnosticReason.Unmatched"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionTernaryUnmatchedColon = new(ChapterDiagnosticSource.ExpressionTernaryColon, ChapterDiagnosticReason.Unmatched);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionTernaryQuestion"/> with reason <see cref="ChapterDiagnosticReason.Unmatched"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionTernaryUnmatchedQuestion = new(ChapterDiagnosticSource.ExpressionTernaryQuestion, ChapterDiagnosticReason.Unmatched);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionTime"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionTime = new(ChapterDiagnosticSource.ExpressionTime, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionParentheses"/> with reason <see cref="ChapterDiagnosticReason.Unbalanced"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionUnbalancedParentheses = new(ChapterDiagnosticSource.ExpressionParentheses, ChapterDiagnosticReason.Unbalanced);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionToken"/> with reason <see cref="ChapterDiagnosticReason.Unknown"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionUnknownToken = new(ChapterDiagnosticSource.ExpressionToken, ChapterDiagnosticReason.Unknown);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionFunction"/> with reason <see cref="ChapterDiagnosticReason.Unsupported"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionUnsupportedFunction = new(ChapterDiagnosticSource.ExpressionFunction, ChapterDiagnosticReason.Unsupported);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionOperator"/> with reason <see cref="ChapterDiagnosticReason.Unsupported"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionUnsupportedOperator = new(ChapterDiagnosticSource.ExpressionOperator, ChapterDiagnosticReason.Unsupported);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExpressionToken"/> with reason <see cref="ChapterDiagnosticReason.Unsupported"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidExpressionUnsupportedToken = new(ChapterDiagnosticSource.ExpressionToken, ChapterDiagnosticReason.Unsupported);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.FrameRate"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidFrameRate = new(ChapterDiagnosticSource.FrameRate, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.FrameConversion"/> with reason <see cref="ChapterDiagnosticReason.Overflow"/>.</summary>
    public static readonly ChapterDiagnosticCode FrameConversionOverflow = new(ChapterDiagnosticSource.FrameConversion, ChapterDiagnosticReason.Overflow);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.FrameText"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidFrameText = new(ChapterDiagnosticSource.FrameText, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Ifo"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidIfo = new(ChapterDiagnosticSource.Ifo, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Ifo"/> with reason <see cref="ChapterDiagnosticReason.Available"/>.</summary>
    public static readonly ChapterDiagnosticCode IfoDiscLoaded = new(ChapterDiagnosticSource.Ifo, ChapterDiagnosticReason.Available);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Ifo"/> with reason <see cref="ChapterDiagnosticReason.Fallback"/>.</summary>
    public static readonly ChapterDiagnosticCode IfoTimingEstimated = new(ChapterDiagnosticSource.Ifo, ChapterDiagnosticReason.Fallback);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Ifo"/> with reason <see cref="ChapterDiagnosticReason.Captured"/>.</summary>
    public static readonly ChapterDiagnosticCode IfoTimingFromNav = new(ChapterDiagnosticSource.Ifo, ChapterDiagnosticReason.Captured);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Mpls"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidMpls = new(ChapterDiagnosticSource.Mpls, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Path"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidPath = new(ChapterDiagnosticSource.Path, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Structure"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidStructure = new(ChapterDiagnosticSource.Structure, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.TimecodeText"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidTimecodeText = new(ChapterDiagnosticSource.TimecodeText, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.TimeText"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidTimeText = new(ChapterDiagnosticSource.TimeText, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.WebVttCueText"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidWebVttCueText = new(ChapterDiagnosticSource.WebVttCueText, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Xml"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode InvalidXml = new(ChapterDiagnosticSource.Xml, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.CueSyntax"/> with reason <see cref="ChapterDiagnosticReason.Malformed"/>.</summary>
    public static readonly ChapterDiagnosticCode MalformedCueSyntax = new(ChapterDiagnosticSource.CueSyntax, ChapterDiagnosticReason.Malformed);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Matroska"/> with reason <see cref="ChapterDiagnosticReason.CannotStart"/>.</summary>
    public static readonly ChapterDiagnosticCode MatroskaCannotStart = new(ChapterDiagnosticSource.Matroska, ChapterDiagnosticReason.CannotStart);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Matroska"/> with reason <see cref="ChapterDiagnosticReason.MissingDependency"/>.</summary>
    public static readonly ChapterDiagnosticCode MatroskaMissingDependency = new(ChapterDiagnosticSource.Matroska, ChapterDiagnosticReason.MissingDependency);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.MatroskaChapters"/> with reason <see cref="ChapterDiagnosticReason.None"/>.</summary>
    public static readonly ChapterDiagnosticCode MatroskaNoChapters = new(ChapterDiagnosticSource.MatroskaChapters, ChapterDiagnosticReason.None);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.MatroskaOutput"/> with reason <see cref="ChapterDiagnosticReason.Truncated"/>.</summary>
    public static readonly ChapterDiagnosticCode MatroskaOutputTruncated = new(ChapterDiagnosticSource.MatroskaOutput, ChapterDiagnosticReason.Truncated);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.MatroskaProcess"/> with reason <see cref="ChapterDiagnosticReason.Cancelled"/>.</summary>
    public static readonly ChapterDiagnosticCode MatroskaProcessCancelled = new(ChapterDiagnosticSource.MatroskaProcess, ChapterDiagnosticReason.Cancelled);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.MatroskaProcess"/> with reason <see cref="ChapterDiagnosticReason.Failed"/>.</summary>
    public static readonly ChapterDiagnosticCode MatroskaProcessFailed = new(ChapterDiagnosticSource.MatroskaProcess, ChapterDiagnosticReason.Failed);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.MatroskaProcess"/> with reason <see cref="ChapterDiagnosticReason.TimedOut"/>.</summary>
    public static readonly ChapterDiagnosticCode MatroskaProcessTimedOut = new(ChapterDiagnosticSource.MatroskaProcess, ChapterDiagnosticReason.TimedOut);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.MediaRead"/> with reason <see cref="ChapterDiagnosticReason.Failed"/>.</summary>
    public static readonly ChapterDiagnosticCode MediaReadFailed = new(ChapterDiagnosticSource.MediaRead, ChapterDiagnosticReason.Failed);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Dependency"/> with reason <see cref="ChapterDiagnosticReason.Missing"/>.</summary>
    public static readonly ChapterDiagnosticCode MissingDependency = new(ChapterDiagnosticSource.Dependency, ChapterDiagnosticReason.Missing);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Input"/> with reason <see cref="ChapterDiagnosticReason.Missing"/>.</summary>
    public static readonly ChapterDiagnosticCode MissingInput = new(ChapterDiagnosticSource.Input, ChapterDiagnosticReason.Missing);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Mp4File"/> with reason <see cref="ChapterDiagnosticReason.Inaccessible"/>.</summary>
    public static readonly ChapterDiagnosticCode Mp4FileInaccessible = new(ChapterDiagnosticSource.Mp4File, ChapterDiagnosticReason.Inaccessible);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Mp4File"/> with reason <see cref="ChapterDiagnosticReason.NotFound"/>.</summary>
    public static readonly ChapterDiagnosticCode Mp4FileNotFound = new(ChapterDiagnosticSource.Mp4File, ChapterDiagnosticReason.NotFound);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Mp4Path"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode Mp4InvalidPath = new(ChapterDiagnosticSource.Mp4Path, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Mp4Metadata"/> with reason <see cref="ChapterDiagnosticReason.Malformed"/>.</summary>
    public static readonly ChapterDiagnosticCode Mp4MalformedMetadata = new(ChapterDiagnosticSource.Mp4Metadata, ChapterDiagnosticReason.Malformed);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Mp4Read"/> with reason <see cref="ChapterDiagnosticReason.Failed"/>.</summary>
    public static readonly ChapterDiagnosticCode Mp4ReadFailed = new(ChapterDiagnosticSource.Mp4Read, ChapterDiagnosticReason.Failed);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Mp4Metadata"/> with reason <see cref="ChapterDiagnosticReason.Unsupported"/>.</summary>
    public static readonly ChapterDiagnosticCode Mp4UnsupportedMetadata = new(ChapterDiagnosticSource.Mp4Metadata, ChapterDiagnosticReason.Unsupported);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.NativeLibrary"/> with reason <see cref="ChapterDiagnosticReason.Missing"/>.</summary>
    public static readonly ChapterDiagnosticCode NativeLibraryMissing = new(ChapterDiagnosticSource.NativeLibrary, ChapterDiagnosticReason.Missing);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Chapters"/> with reason <see cref="ChapterDiagnosticReason.None"/>.</summary>
    public static readonly ChapterDiagnosticCode NoChapters = new(ChapterDiagnosticSource.Chapters, ChapterDiagnosticReason.None);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Chapters"/> with reason <see cref="ChapterDiagnosticReason.NotFound"/>.</summary>
    public static readonly ChapterDiagnosticCode NoChaptersFound = new(ChapterDiagnosticSource.Chapters, ChapterDiagnosticReason.NotFound);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Rows"/> with reason <see cref="ChapterDiagnosticReason.NoneSelected"/>.</summary>
    public static readonly ChapterDiagnosticCode NoRowsSelected = new(ChapterDiagnosticSource.Rows, ChapterDiagnosticReason.NoneSelected);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Segments"/> with reason <see cref="ChapterDiagnosticReason.None"/>.</summary>
    public static readonly ChapterDiagnosticCode NoSegments = new(ChapterDiagnosticSource.Segments, ChapterDiagnosticReason.None);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.OgmFirstLine"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode OgmInvalidFirstLine = new(ChapterDiagnosticSource.OgmFirstLine, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.OrderShift"/> with reason <see cref="ChapterDiagnosticReason.Normalized"/>.</summary>
    public static readonly ChapterDiagnosticCode OrderShiftNormalized = new(ChapterDiagnosticSource.OrderShift, ChapterDiagnosticReason.Normalized);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Parse"/> with reason <see cref="ChapterDiagnosticReason.Partial"/>.</summary>
    public static readonly ChapterDiagnosticCode PartialParse = new(ChapterDiagnosticSource.Parse, ChapterDiagnosticReason.Partial);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.PremiereMarkerList"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode PremiereMarkerListInvalid = new(ChapterDiagnosticSource.PremiereMarkerList, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Save"/> with reason <see cref="ChapterDiagnosticReason.Failed"/>.</summary>
    public static readonly ChapterDiagnosticCode SaveFailed = new(ChapterDiagnosticSource.Save, ChapterDiagnosticReason.Failed);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Save"/> with reason <see cref="ChapterDiagnosticReason.Succeeded"/>.</summary>
    public static readonly ChapterDiagnosticCode Saved = new(ChapterDiagnosticSource.Save, ChapterDiagnosticReason.Succeeded);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.SelectionGroup"/> with reason <see cref="ChapterDiagnosticReason.Available"/>.</summary>
    public static readonly ChapterDiagnosticCode SelectionGroupAvailable = new(ChapterDiagnosticSource.SelectionGroup, ChapterDiagnosticReason.Available);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.SelectionOption"/> with reason <see cref="ChapterDiagnosticReason.Available"/>.</summary>
    public static readonly ChapterDiagnosticCode SelectionOptionAvailable = new(ChapterDiagnosticSource.SelectionOption, ChapterDiagnosticReason.Available);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.StandardOutput"/> with reason <see cref="ChapterDiagnosticReason.Captured"/>.</summary>
    public static readonly ChapterDiagnosticCode Stdout = new(ChapterDiagnosticSource.StandardOutput, ChapterDiagnosticReason.Captured);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Importer"/> with reason <see cref="ChapterDiagnosticReason.Unavailable"/>.</summary>
    public static readonly ChapterDiagnosticCode Unavailable = new(ChapterDiagnosticSource.Importer, ChapterDiagnosticReason.Unavailable);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.AppendSource"/> with reason <see cref="ChapterDiagnosticReason.Unsupported"/>.</summary>
    public static readonly ChapterDiagnosticCode UnsupportedAppendSource = new(ChapterDiagnosticSource.AppendSource, ChapterDiagnosticReason.Unsupported);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.CombineSource"/> with reason <see cref="ChapterDiagnosticReason.Unsupported"/>.</summary>
    public static readonly ChapterDiagnosticCode UnsupportedCombineSource = new(ChapterDiagnosticSource.CombineSource, ChapterDiagnosticReason.Unsupported);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.ExportFormat"/> with reason <see cref="ChapterDiagnosticReason.Unsupported"/>.</summary>
    public static readonly ChapterDiagnosticCode UnsupportedExportFormat = new(ChapterDiagnosticSource.ExportFormat, ChapterDiagnosticReason.Unsupported);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Input"/> with reason <see cref="ChapterDiagnosticReason.Unsupported"/>.</summary>
    public static readonly ChapterDiagnosticCode UnsupportedInput = new(ChapterDiagnosticSource.Input, ChapterDiagnosticReason.Unsupported);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.Source"/> with reason <see cref="ChapterDiagnosticReason.Unsupported"/>.</summary>
    public static readonly ChapterDiagnosticCode UnsupportedSource = new(ChapterDiagnosticSource.Source, ChapterDiagnosticReason.Unsupported);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.WebVttHeader"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode WebVttInvalidHeader = new(ChapterDiagnosticSource.WebVttHeader, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.WebVttCue"/> with reason <see cref="ChapterDiagnosticReason.Malformed"/>.</summary>
    public static readonly ChapterDiagnosticCode WebVttMalformedCue = new(ChapterDiagnosticSource.WebVttCue, ChapterDiagnosticReason.Malformed);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.WebVttTimingSettings"/> with reason <see cref="ChapterDiagnosticReason.Unsupported"/>.</summary>
    public static readonly ChapterDiagnosticCode WebVttUnsupportedTimingSettings = new(ChapterDiagnosticSource.WebVttTimingSettings, ChapterDiagnosticReason.Unsupported);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.XmlRoot"/> with reason <see cref="ChapterDiagnosticReason.Invalid"/>.</summary>
    public static readonly ChapterDiagnosticCode XmlInvalidRoot = new(ChapterDiagnosticSource.XmlRoot, ChapterDiagnosticReason.Invalid);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.XmlChapters"/> with reason <see cref="ChapterDiagnosticReason.None"/>.</summary>
    public static readonly ChapterDiagnosticCode XmlNoChapters = new(ChapterDiagnosticSource.XmlChapters, ChapterDiagnosticReason.None);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.XplChapters"/> with reason <see cref="ChapterDiagnosticReason.None"/>.</summary>
    public static readonly ChapterDiagnosticCode XplNoChapters = new(ChapterDiagnosticSource.XplChapters, ChapterDiagnosticReason.None);
    /// <summary>Identifies the diagnostic emitted by <see cref="ChapterDiagnosticSource.XplParse"/> with reason <see cref="ChapterDiagnosticReason.Failed"/>.</summary>
    public static readonly ChapterDiagnosticCode XplParseFailed = new(ChapterDiagnosticSource.XplParse, ChapterDiagnosticReason.Failed);
}
