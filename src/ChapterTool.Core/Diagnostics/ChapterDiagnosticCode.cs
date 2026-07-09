namespace ChapterTool.Core.Diagnostics;

#pragma warning disable CS1591

/// <summary>
/// Identifies a diagnostic by the component that produced it and the failure or status reason.
/// </summary>
/// <param name="Source">The component or data source associated with the diagnostic.</param>
/// <param name="Reason">The failure or status reason.</param>
public readonly record struct ChapterDiagnosticCode(ChapterDiagnosticSource Source, ChapterDiagnosticReason Reason)
{
    public override string ToString() => this.ToDisplayCode();

    public static readonly ChapterDiagnosticCode DependencyCannotStart = new(ChapterDiagnosticSource.Dependency, ChapterDiagnosticReason.CannotStart);
    public static readonly ChapterDiagnosticCode DependencyExecutionCancelled = new(ChapterDiagnosticSource.DependencyExecution, ChapterDiagnosticReason.Cancelled);
    public static readonly ChapterDiagnosticCode DependencyExecutionFailed = new(ChapterDiagnosticSource.DependencyExecution, ChapterDiagnosticReason.Failed);
    public static readonly ChapterDiagnosticCode DependencyExecutionTimedOut = new(ChapterDiagnosticSource.DependencyExecution, ChapterDiagnosticReason.TimedOut);
    public static readonly ChapterDiagnosticCode DependencyOutputEmpty = new(ChapterDiagnosticSource.DependencyOutput, ChapterDiagnosticReason.Empty);
    public static readonly ChapterDiagnosticCode DependencyOutputMissing = new(ChapterDiagnosticSource.DependencyOutput, ChapterDiagnosticReason.Missing);
    public static readonly ChapterDiagnosticCode DependencyOutputTruncated = new(ChapterDiagnosticSource.DependencyOutput, ChapterDiagnosticReason.Truncated);
    public static readonly ChapterDiagnosticCode DependencyOutputUnrecognized = new(ChapterDiagnosticSource.DependencyOutput, ChapterDiagnosticReason.Unrecognized);
    public static readonly ChapterDiagnosticCode EmbeddedCueNotFound = new(ChapterDiagnosticSource.EmbeddedCue, ChapterDiagnosticReason.NotFound);
    public static readonly ChapterDiagnosticCode EmptyChapters = new(ChapterDiagnosticSource.Chapters, ChapterDiagnosticReason.Empty);
    public static readonly ChapterDiagnosticCode EmptyCueFile = new(ChapterDiagnosticSource.CueFile, ChapterDiagnosticReason.Empty);
    public static readonly ChapterDiagnosticCode EmptyXml = new(ChapterDiagnosticSource.Xml, ChapterDiagnosticReason.Empty);
    public static readonly ChapterDiagnosticCode FfprobeCannotStart = new(ChapterDiagnosticSource.Ffprobe, ChapterDiagnosticReason.CannotStart);
    public static readonly ChapterDiagnosticCode FfprobeEmptyOutput = new(ChapterDiagnosticSource.FfprobeOutput, ChapterDiagnosticReason.Empty);
    public static readonly ChapterDiagnosticCode FfprobeMissingDependency = new(ChapterDiagnosticSource.Ffprobe, ChapterDiagnosticReason.MissingDependency);
    public static readonly ChapterDiagnosticCode FfprobeOutputTruncated = new(ChapterDiagnosticSource.FfprobeOutput, ChapterDiagnosticReason.Truncated);
    public static readonly ChapterDiagnosticCode FfprobeParseFailed = new(ChapterDiagnosticSource.Ffprobe, ChapterDiagnosticReason.ParseFailed);
    public static readonly ChapterDiagnosticCode FfprobeProcessCancelled = new(ChapterDiagnosticSource.FfprobeProcess, ChapterDiagnosticReason.Cancelled);
    public static readonly ChapterDiagnosticCode FfprobeProcessFailed = new(ChapterDiagnosticSource.FfprobeProcess, ChapterDiagnosticReason.Failed);
    public static readonly ChapterDiagnosticCode FfprobeProcessTimedOut = new(ChapterDiagnosticSource.FfprobeProcess, ChapterDiagnosticReason.TimedOut);
    public static readonly ChapterDiagnosticCode FlacEmbeddedCueNotFound = new(ChapterDiagnosticSource.FlacEmbeddedCue, ChapterDiagnosticReason.NotFound);
    public static readonly ChapterDiagnosticCode ImporterFallbackUsed = new(ChapterDiagnosticSource.ImporterFallback, ChapterDiagnosticReason.Used);
    public static readonly ChapterDiagnosticCode InputNotFound = new(ChapterDiagnosticSource.Input, ChapterDiagnosticReason.NotFound);
    public static readonly ChapterDiagnosticCode InvalidChapterIndex = new(ChapterDiagnosticSource.ChapterIndex, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidChapterPair = new(ChapterDiagnosticSource.ChapterPair, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidChapterText = new(ChapterDiagnosticSource.ChapterText, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidChapterTimestamp = new(ChapterDiagnosticSource.ChapterTimestamp, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidContainerHeader = new(ChapterDiagnosticSource.ContainerHeader, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidEntryElement = new(ChapterDiagnosticSource.EntryElement, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidExpression = new(ChapterDiagnosticSource.Expression, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidExpressionIncomplete = new(ChapterDiagnosticSource.Expression, ChapterDiagnosticReason.Incomplete);
    public static readonly ChapterDiagnosticCode InvalidExpressionInsufficientOperands = new(ChapterDiagnosticSource.Expression, ChapterDiagnosticReason.InsufficientOperands);
    public static readonly ChapterDiagnosticCode InvalidExpressionInvalidCharacter = new(ChapterDiagnosticSource.ExpressionCharacter, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidExpressionLua = new(ChapterDiagnosticSource.LuaExpression, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidExpressionLuaCanceled = new(ChapterDiagnosticSource.LuaExpression, ChapterDiagnosticReason.Cancelled);
    public static readonly ChapterDiagnosticCode InvalidExpressionLuaCompile = new(ChapterDiagnosticSource.LuaExpression, ChapterDiagnosticReason.CompileFailed);
    public static readonly ChapterDiagnosticCode InvalidExpressionLuaInvalidReturn = new(ChapterDiagnosticSource.LuaExpressionReturn, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidExpressionLuaMissingReturn = new(ChapterDiagnosticSource.LuaExpressionReturn, ChapterDiagnosticReason.Missing);
    public static readonly ChapterDiagnosticCode InvalidExpressionLuaRuntime = new(ChapterDiagnosticSource.LuaExpression, ChapterDiagnosticReason.RuntimeFailed);
    public static readonly ChapterDiagnosticCode InvalidExpressionLuaUnknownToken = new(ChapterDiagnosticSource.LuaExpressionToken, ChapterDiagnosticReason.Unknown);
    public static readonly ChapterDiagnosticCode InvalidExpressionMisplacedComma = new(ChapterDiagnosticSource.ExpressionComma, ChapterDiagnosticReason.Misplaced);
    public static readonly ChapterDiagnosticCode InvalidExpressionMissingOperandBeforeParen = new(ChapterDiagnosticSource.ExpressionOperandBeforeParen, ChapterDiagnosticReason.Missing);
    public static readonly ChapterDiagnosticCode InvalidExpressionMissingOperator = new(ChapterDiagnosticSource.ExpressionOperator, ChapterDiagnosticReason.Missing);
    public static readonly ChapterDiagnosticCode InvalidExpressionMissingOperatorBeforeFunction = new(ChapterDiagnosticSource.ExpressionOperatorBeforeFunction, ChapterDiagnosticReason.Missing);
    public static readonly ChapterDiagnosticCode InvalidExpressionMissingOperatorBeforeParen = new(ChapterDiagnosticSource.ExpressionOperatorBeforeParen, ChapterDiagnosticReason.Missing);
    public static readonly ChapterDiagnosticCode InvalidExpressionOperatorRequiresLeftOperand = new(ChapterDiagnosticSource.ExpressionOperator, ChapterDiagnosticReason.RequiresLeftOperand);
    public static readonly ChapterDiagnosticCode InvalidExpressionTernaryMissingCondition = new(ChapterDiagnosticSource.ExpressionTernaryCondition, ChapterDiagnosticReason.Missing);
    public static readonly ChapterDiagnosticCode InvalidExpressionTernaryMissingTrueExpression = new(ChapterDiagnosticSource.ExpressionTernaryTrueExpression, ChapterDiagnosticReason.Missing);
    public static readonly ChapterDiagnosticCode InvalidExpressionTernaryUnmatchedColon = new(ChapterDiagnosticSource.ExpressionTernaryColon, ChapterDiagnosticReason.Unmatched);
    public static readonly ChapterDiagnosticCode InvalidExpressionTernaryUnmatchedQuestion = new(ChapterDiagnosticSource.ExpressionTernaryQuestion, ChapterDiagnosticReason.Unmatched);
    public static readonly ChapterDiagnosticCode InvalidExpressionTime = new(ChapterDiagnosticSource.ExpressionTime, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidExpressionUnbalancedParentheses = new(ChapterDiagnosticSource.ExpressionParentheses, ChapterDiagnosticReason.Unbalanced);
    public static readonly ChapterDiagnosticCode InvalidExpressionUnknownToken = new(ChapterDiagnosticSource.ExpressionToken, ChapterDiagnosticReason.Unknown);
    public static readonly ChapterDiagnosticCode InvalidExpressionUnsupportedFunction = new(ChapterDiagnosticSource.ExpressionFunction, ChapterDiagnosticReason.Unsupported);
    public static readonly ChapterDiagnosticCode InvalidExpressionUnsupportedOperator = new(ChapterDiagnosticSource.ExpressionOperator, ChapterDiagnosticReason.Unsupported);
    public static readonly ChapterDiagnosticCode InvalidExpressionUnsupportedToken = new(ChapterDiagnosticSource.ExpressionToken, ChapterDiagnosticReason.Unsupported);
    public static readonly ChapterDiagnosticCode InvalidFrameRate = new(ChapterDiagnosticSource.FrameRate, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidFrameText = new(ChapterDiagnosticSource.FrameText, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidIfo = new(ChapterDiagnosticSource.Ifo, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidMpls = new(ChapterDiagnosticSource.Mpls, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidPath = new(ChapterDiagnosticSource.Path, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidStructure = new(ChapterDiagnosticSource.Structure, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidTimecodeText = new(ChapterDiagnosticSource.TimecodeText, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidTimeText = new(ChapterDiagnosticSource.TimeText, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidWebVttCueText = new(ChapterDiagnosticSource.WebVttCueText, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode InvalidXml = new(ChapterDiagnosticSource.Xml, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode MalformedCueSyntax = new(ChapterDiagnosticSource.CueSyntax, ChapterDiagnosticReason.Malformed);
    public static readonly ChapterDiagnosticCode MatroskaCannotStart = new(ChapterDiagnosticSource.Matroska, ChapterDiagnosticReason.CannotStart);
    public static readonly ChapterDiagnosticCode MatroskaMissingDependency = new(ChapterDiagnosticSource.Matroska, ChapterDiagnosticReason.MissingDependency);
    public static readonly ChapterDiagnosticCode MatroskaNoChapters = new(ChapterDiagnosticSource.MatroskaChapters, ChapterDiagnosticReason.None);
    public static readonly ChapterDiagnosticCode MatroskaOutputTruncated = new(ChapterDiagnosticSource.MatroskaOutput, ChapterDiagnosticReason.Truncated);
    public static readonly ChapterDiagnosticCode MatroskaProcessCancelled = new(ChapterDiagnosticSource.MatroskaProcess, ChapterDiagnosticReason.Cancelled);
    public static readonly ChapterDiagnosticCode MatroskaProcessFailed = new(ChapterDiagnosticSource.MatroskaProcess, ChapterDiagnosticReason.Failed);
    public static readonly ChapterDiagnosticCode MatroskaProcessTimedOut = new(ChapterDiagnosticSource.MatroskaProcess, ChapterDiagnosticReason.TimedOut);
    public static readonly ChapterDiagnosticCode MediaReadFailed = new(ChapterDiagnosticSource.MediaRead, ChapterDiagnosticReason.Failed);
    public static readonly ChapterDiagnosticCode MissingDependency = new(ChapterDiagnosticSource.Dependency, ChapterDiagnosticReason.Missing);
    public static readonly ChapterDiagnosticCode MissingInput = new(ChapterDiagnosticSource.Input, ChapterDiagnosticReason.Missing);
    public static readonly ChapterDiagnosticCode Mp4FileInaccessible = new(ChapterDiagnosticSource.Mp4File, ChapterDiagnosticReason.Inaccessible);
    public static readonly ChapterDiagnosticCode Mp4FileNotFound = new(ChapterDiagnosticSource.Mp4File, ChapterDiagnosticReason.NotFound);
    public static readonly ChapterDiagnosticCode Mp4InvalidPath = new(ChapterDiagnosticSource.Mp4Path, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode Mp4MalformedMetadata = new(ChapterDiagnosticSource.Mp4Metadata, ChapterDiagnosticReason.Malformed);
    public static readonly ChapterDiagnosticCode Mp4ReadFailed = new(ChapterDiagnosticSource.Mp4Read, ChapterDiagnosticReason.Failed);
    public static readonly ChapterDiagnosticCode Mp4UnsupportedMetadata = new(ChapterDiagnosticSource.Mp4Metadata, ChapterDiagnosticReason.Unsupported);
    public static readonly ChapterDiagnosticCode NativeLibraryMissing = new(ChapterDiagnosticSource.NativeLibrary, ChapterDiagnosticReason.Missing);
    public static readonly ChapterDiagnosticCode NoChapters = new(ChapterDiagnosticSource.Chapters, ChapterDiagnosticReason.None);
    public static readonly ChapterDiagnosticCode NoChaptersFound = new(ChapterDiagnosticSource.Chapters, ChapterDiagnosticReason.NotFound);
    public static readonly ChapterDiagnosticCode NoRowsSelected = new(ChapterDiagnosticSource.Rows, ChapterDiagnosticReason.NoneSelected);
    public static readonly ChapterDiagnosticCode NoSegments = new(ChapterDiagnosticSource.Segments, ChapterDiagnosticReason.None);
    public static readonly ChapterDiagnosticCode OgmInvalidFirstLine = new(ChapterDiagnosticSource.OgmFirstLine, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode OrderShiftNormalized = new(ChapterDiagnosticSource.OrderShift, ChapterDiagnosticReason.Normalized);
    public static readonly ChapterDiagnosticCode PartialParse = new(ChapterDiagnosticSource.Parse, ChapterDiagnosticReason.Partial);
    public static readonly ChapterDiagnosticCode PremiereMarkerListInvalid = new(ChapterDiagnosticSource.PremiereMarkerList, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode SaveFailed = new(ChapterDiagnosticSource.Save, ChapterDiagnosticReason.Failed);
    public static readonly ChapterDiagnosticCode Saved = new(ChapterDiagnosticSource.Save, ChapterDiagnosticReason.Succeeded);
    public static readonly ChapterDiagnosticCode SelectionGroupAvailable = new(ChapterDiagnosticSource.SelectionGroup, ChapterDiagnosticReason.Available);
    public static readonly ChapterDiagnosticCode SelectionOptionAvailable = new(ChapterDiagnosticSource.SelectionOption, ChapterDiagnosticReason.Available);
    public static readonly ChapterDiagnosticCode Stdout = new(ChapterDiagnosticSource.StandardOutput, ChapterDiagnosticReason.Captured);
    public static readonly ChapterDiagnosticCode Unavailable = new(ChapterDiagnosticSource.Importer, ChapterDiagnosticReason.Unavailable);
    public static readonly ChapterDiagnosticCode UnsupportedAppendSource = new(ChapterDiagnosticSource.AppendSource, ChapterDiagnosticReason.Unsupported);
    public static readonly ChapterDiagnosticCode UnsupportedCombineSource = new(ChapterDiagnosticSource.CombineSource, ChapterDiagnosticReason.Unsupported);
    public static readonly ChapterDiagnosticCode UnsupportedExportFormat = new(ChapterDiagnosticSource.ExportFormat, ChapterDiagnosticReason.Unsupported);
    public static readonly ChapterDiagnosticCode UnsupportedInput = new(ChapterDiagnosticSource.Input, ChapterDiagnosticReason.Unsupported);
    public static readonly ChapterDiagnosticCode UnsupportedSource = new(ChapterDiagnosticSource.Source, ChapterDiagnosticReason.Unsupported);
    public static readonly ChapterDiagnosticCode WebVttInvalidHeader = new(ChapterDiagnosticSource.WebVttHeader, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode WebVttMalformedCue = new(ChapterDiagnosticSource.WebVttCue, ChapterDiagnosticReason.Malformed);
    public static readonly ChapterDiagnosticCode WebVttUnsupportedTimingSettings = new(ChapterDiagnosticSource.WebVttTimingSettings, ChapterDiagnosticReason.Unsupported);
    public static readonly ChapterDiagnosticCode XmlInvalidRoot = new(ChapterDiagnosticSource.XmlRoot, ChapterDiagnosticReason.Invalid);
    public static readonly ChapterDiagnosticCode XmlNoChapters = new(ChapterDiagnosticSource.XmlChapters, ChapterDiagnosticReason.None);
    public static readonly ChapterDiagnosticCode XplNoChapters = new(ChapterDiagnosticSource.XplChapters, ChapterDiagnosticReason.None);
    public static readonly ChapterDiagnosticCode XplParseFailed = new(ChapterDiagnosticSource.XplParse, ChapterDiagnosticReason.Failed);
}

#pragma warning restore CS1591
