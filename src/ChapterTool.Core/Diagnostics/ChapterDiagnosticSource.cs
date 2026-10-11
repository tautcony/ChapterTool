namespace ChapterTool.Core.Diagnostics;

/// <summary>
/// Components, formats, or data fields that can produce diagnostics.
/// </summary>
public enum ChapterDiagnosticSource
{
    /// <summary>Identifies diagnostics associated with <c>AppendSource</c>.</summary>
    AppendSource,
    /// <summary>Identifies diagnostics associated with <c>Bdjo</c>.</summary>
    Bdjo,
    /// <summary>Identifies diagnostics associated with <c>BdmvScan</c>.</summary>
    BdmvScan,
    /// <summary>Identifies diagnostics associated with <c>ChapterIndex</c>.</summary>
    ChapterIndex,
    /// <summary>Identifies diagnostics associated with <c>ChapterPair</c>.</summary>
    ChapterPair,
    /// <summary>Identifies diagnostics associated with <c>Clpi</c>.</summary>
    Clpi,
    /// <summary>Identifies diagnostics associated with <c>Chapters</c>.</summary>
    Chapters,
    /// <summary>Identifies diagnostics associated with <c>ChapterText</c>.</summary>
    ChapterText,
    /// <summary>Identifies diagnostics associated with <c>ChapterTimestamp</c>.</summary>
    ChapterTimestamp,
    /// <summary>Identifies diagnostics associated with <c>CombineSource</c>.</summary>
    CombineSource,
    /// <summary>Identifies diagnostics associated with <c>ContainerHeader</c>.</summary>
    ContainerHeader,
    /// <summary>Identifies diagnostics associated with <c>CueEncoding</c>.</summary>
    CueEncoding,
    /// <summary>Identifies diagnostics associated with <c>CueFile</c>.</summary>
    CueFile,
    /// <summary>Identifies diagnostics associated with <c>CueSyntax</c>.</summary>
    CueSyntax,
    /// <summary>Identifies diagnostics associated with <c>Dependency</c>.</summary>
    Dependency,
    /// <summary>Identifies diagnostics associated with <c>DependencyExecution</c>.</summary>
    DependencyExecution,
    /// <summary>Identifies diagnostics associated with <c>DependencyOutput</c>.</summary>
    DependencyOutput,
    /// <summary>Identifies diagnostics associated with <c>EmbeddedCue</c>.</summary>
    EmbeddedCue,
    /// <summary>Identifies diagnostics associated with <c>EntryElement</c>.</summary>
    EntryElement,
    /// <summary>Identifies diagnostics associated with <c>ExportFormat</c>.</summary>
    ExportFormat,
    /// <summary>Identifies diagnostics associated with <c>Expression</c>.</summary>
    Expression,
    /// <summary>Identifies diagnostics associated with <c>ExpressionCharacter</c>.</summary>
    ExpressionCharacter,
    /// <summary>Identifies diagnostics associated with <c>ExpressionComma</c>.</summary>
    ExpressionComma,
    /// <summary>Identifies diagnostics associated with <c>ExpressionFunction</c>.</summary>
    ExpressionFunction,
    /// <summary>Identifies diagnostics associated with <c>ExpressionOperandBeforeParen</c>.</summary>
    ExpressionOperandBeforeParen,
    /// <summary>Identifies diagnostics associated with <c>ExpressionOperator</c>.</summary>
    ExpressionOperator,
    /// <summary>Identifies diagnostics associated with <c>ExpressionOperatorBeforeFunction</c>.</summary>
    ExpressionOperatorBeforeFunction,
    /// <summary>Identifies diagnostics associated with <c>ExpressionOperatorBeforeParen</c>.</summary>
    ExpressionOperatorBeforeParen,
    /// <summary>Identifies diagnostics associated with <c>ExpressionParentheses</c>.</summary>
    ExpressionParentheses,
    /// <summary>Identifies diagnostics associated with <c>ExpressionTernaryColon</c>.</summary>
    ExpressionTernaryColon,
    /// <summary>Identifies diagnostics associated with <c>ExpressionTernaryCondition</c>.</summary>
    ExpressionTernaryCondition,
    /// <summary>Identifies diagnostics associated with <c>ExpressionTernaryQuestion</c>.</summary>
    ExpressionTernaryQuestion,
    /// <summary>Identifies diagnostics associated with <c>ExpressionTernaryTrueExpression</c>.</summary>
    ExpressionTernaryTrueExpression,
    /// <summary>Identifies diagnostics associated with <c>ExpressionTime</c>.</summary>
    ExpressionTime,
    /// <summary>Identifies diagnostics associated with <c>ExpressionToken</c>.</summary>
    ExpressionToken,
    /// <summary>Identifies diagnostics associated with <c>Ffprobe</c>.</summary>
    Ffprobe,
    /// <summary>Identifies diagnostics associated with <c>FfprobeOutput</c>.</summary>
    FfprobeOutput,
    /// <summary>Identifies diagnostics associated with <c>FfprobeProcess</c>.</summary>
    FfprobeProcess,
    /// <summary>Identifies diagnostics associated with <c>FlacEmbeddedCue</c>.</summary>
    FlacEmbeddedCue,
    /// <summary>Identifies diagnostics associated with <c>FrameRate</c>.</summary>
    FrameRate,
    /// <summary>Identifies diagnostics associated with <c>FrameText</c>.</summary>
    FrameText,
    /// <summary>Identifies diagnostics associated with <c>Ifo</c>.</summary>
    Ifo,
    /// <summary>Identifies diagnostics associated with <c>Importer</c>.</summary>
    Importer,
    /// <summary>Identifies diagnostics associated with <c>ImporterFallback</c>.</summary>
    ImporterFallback,
    /// <summary>Identifies diagnostics associated with <c>Input</c>.</summary>
    Input,
    /// <summary>Identifies diagnostics associated with <c>InputLayout</c>.</summary>
    InputLayout,
    /// <summary>Identifies diagnostics associated with <c>LuaExpression</c>.</summary>
    LuaExpression,
    /// <summary>Identifies diagnostics associated with <c>LuaExpressionReturn</c>.</summary>
    LuaExpressionReturn,
    /// <summary>Identifies diagnostics associated with <c>LuaExpressionToken</c>.</summary>
    LuaExpressionToken,
    /// <summary>Identifies diagnostics associated with <c>Matroska</c>.</summary>
    Matroska,
    /// <summary>Identifies diagnostics associated with <c>MatroskaChapters</c>.</summary>
    MatroskaChapters,
    /// <summary>Identifies diagnostics associated with <c>MatroskaOutput</c>.</summary>
    MatroskaOutput,
    /// <summary>Identifies diagnostics associated with <c>MatroskaProcess</c>.</summary>
    MatroskaProcess,
    /// <summary>Identifies diagnostics associated with <c>MediaRead</c>.</summary>
    MediaRead,
    /// <summary>Identifies diagnostics associated with <c>Mp4File</c>.</summary>
    Mp4File,
    /// <summary>Identifies diagnostics associated with <c>Mp4Metadata</c>.</summary>
    Mp4Metadata,
    /// <summary>Identifies diagnostics associated with <c>Mp4Path</c>.</summary>
    Mp4Path,
    /// <summary>Identifies diagnostics associated with <c>Mp4Read</c>.</summary>
    Mp4Read,
    /// <summary>Identifies diagnostics associated with <c>Mpls</c>.</summary>
    Mpls,
    /// <summary>Identifies diagnostics associated with <c>MovieObject</c>.</summary>
    MovieObject,
    /// <summary>Identifies diagnostics associated with <c>Navigation</c>.</summary>
    Navigation,
    /// <summary>Identifies diagnostics associated with <c>NativeLibrary</c>.</summary>
    NativeLibrary,
    /// <summary>Identifies diagnostics associated with <c>OgmFirstLine</c>.</summary>
    OgmFirstLine,
    /// <summary>Identifies diagnostics associated with <c>OrderShift</c>.</summary>
    OrderShift,
    /// <summary>Identifies diagnostics associated with <c>Parse</c>.</summary>
    Parse,
    /// <summary>Identifies diagnostics associated with <c>Path</c>.</summary>
    Path,
    /// <summary>Identifies diagnostics associated with <c>PremiereMarkerList</c>.</summary>
    PremiereMarkerList,
    /// <summary>Identifies diagnostics associated with <c>Rows</c>.</summary>
    Rows,
    /// <summary>Identifies diagnostics associated with <c>Save</c>.</summary>
    Save,
    /// <summary>Identifies diagnostics associated with <c>Segments</c>.</summary>
    Segments,
    /// <summary>Identifies diagnostics associated with <c>SelectionGroup</c>.</summary>
    SelectionGroup,
    /// <summary>Identifies diagnostics associated with <c>SelectionOption</c>.</summary>
    SelectionOption,
    /// <summary>Identifies diagnostics associated with <c>Source</c>.</summary>
    Source,
    /// <summary>Identifies diagnostics associated with <c>StandardOutput</c>.</summary>
    StandardOutput,
    /// <summary>Identifies diagnostics associated with <c>Structure</c>.</summary>
    Structure,
    /// <summary>Identifies diagnostics associated with <c>TimecodeText</c>.</summary>
    TimecodeText,
    /// <summary>Identifies diagnostics associated with <c>TextEncoding</c>.</summary>
    TextEncoding,
    /// <summary>Identifies diagnostics associated with <c>TimeText</c>.</summary>
    TimeText,
    /// <summary>Identifies diagnostics associated with <c>WebVtt</c>.</summary>
    WebVtt,
    /// <summary>Identifies diagnostics associated with <c>WebVttCue</c>.</summary>
    WebVttCue,
    /// <summary>Identifies diagnostics associated with <c>WebVttCueText</c>.</summary>
    WebVttCueText,
    /// <summary>Identifies diagnostics associated with <c>WebVttHeader</c>.</summary>
    WebVttHeader,
    /// <summary>Identifies diagnostics associated with <c>WebVttTimingSettings</c>.</summary>
    WebVttTimingSettings,
    /// <summary>Identifies diagnostics associated with <c>Xml</c>.</summary>
    Xml,
    /// <summary>Identifies diagnostics associated with <c>XmlChapters</c>.</summary>
    XmlChapters,
    /// <summary>Identifies diagnostics associated with <c>XmlRoot</c>.</summary>
    XmlRoot,
    /// <summary>Identifies diagnostics associated with <c>XplChapters</c>.</summary>
    XplChapters,
    /// <summary>Identifies diagnostics associated with <c>XplParse</c>.</summary>
    XplParse,
    /// <summary>Identifies diagnostics associated with <c>FrameConversion</c>.</summary>
    FrameConversion
}
