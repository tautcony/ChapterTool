namespace ChapterTool.Core.Diagnostics;

#pragma warning disable CS1591

/// <summary>
/// Failure or status reasons for chapter diagnostics.
/// </summary>
public enum ChapterDiagnosticReason
{
    Available,
    Cancelled,
    CannotStart,
    Captured,
    CompileFailed,
    Empty,
    Failed,
    Inaccessible,
    Incomplete,
    InsufficientOperands,
    Invalid,
    Malformed,
    Misplaced,
    Missing,
    MissingDependency,
    None,
    NoneSelected,
    Normalized,
    NotFound,
    ParseFailed,
    Partial,
    RequiresLeftOperand,
    RuntimeFailed,
    Succeeded,
    TimedOut,
    Truncated,
    Unavailable,
    Unbalanced,
    Unknown,
    Unmatched,
    Unrecognized,
    Unsupported,
    Used
}

#pragma warning restore CS1591
