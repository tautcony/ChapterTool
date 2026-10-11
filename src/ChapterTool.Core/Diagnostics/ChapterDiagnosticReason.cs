namespace ChapterTool.Core.Diagnostics;

/// <summary>
/// Failure or status reasons for chapter diagnostics.
/// </summary>
public enum ChapterDiagnosticReason
{
    /// <summary>Identifies the <c>Available</c> diagnostic outcome.</summary>
    Available,
    /// <summary>Identifies the <c>Cancelled</c> diagnostic outcome.</summary>
    Cancelled,
    /// <summary>Identifies the <c>CannotStart</c> diagnostic outcome.</summary>
    CannotStart,
    /// <summary>Identifies the <c>Captured</c> diagnostic outcome.</summary>
    Captured,
    /// <summary>Identifies the <c>CompileFailed</c> diagnostic outcome.</summary>
    CompileFailed,
    /// <summary>Identifies the <c>Empty</c> diagnostic outcome.</summary>
    Empty,
    /// <summary>Identifies the <c>Failed</c> diagnostic outcome.</summary>
    Failed,
    /// <summary>Identifies the <c>Fallback</c> diagnostic outcome.</summary>
    Fallback,
    /// <summary>Identifies the <c>Inaccessible</c> diagnostic outcome.</summary>
    Inaccessible,
    /// <summary>Identifies the <c>Incomplete</c> diagnostic outcome.</summary>
    Incomplete,
    /// <summary>Identifies the <c>InsufficientOperands</c> diagnostic outcome.</summary>
    InsufficientOperands,
    /// <summary>Identifies the <c>Invalid</c> diagnostic outcome.</summary>
    Invalid,
    /// <summary>Identifies the <c>Limit</c> diagnostic outcome.</summary>
    Limit,
    /// <summary>Identifies the <c>Malformed</c> diagnostic outcome.</summary>
    Malformed,
    /// <summary>Identifies the <c>Misplaced</c> diagnostic outcome.</summary>
    Misplaced,
    /// <summary>Identifies the <c>Missing</c> diagnostic outcome.</summary>
    Missing,
    /// <summary>Identifies the <c>MissingDependency</c> diagnostic outcome.</summary>
    MissingDependency,
    /// <summary>Identifies the <c>None</c> diagnostic outcome.</summary>
    None,
    /// <summary>Identifies the <c>NoneSelected</c> diagnostic outcome.</summary>
    NoneSelected,
    /// <summary>Identifies the <c>Normalized</c> diagnostic outcome.</summary>
    Normalized,
    /// <summary>Identifies the <c>NotFound</c> diagnostic outcome.</summary>
    NotFound,
    /// <summary>Identifies the <c>ParseFailed</c> diagnostic outcome.</summary>
    ParseFailed,
    /// <summary>Identifies the <c>Partial</c> diagnostic outcome.</summary>
    Partial,
    /// <summary>Identifies the <c>RequiresLeftOperand</c> diagnostic outcome.</summary>
    RequiresLeftOperand,
    /// <summary>Identifies the <c>RuntimeFailed</c> diagnostic outcome.</summary>
    RuntimeFailed,
    /// <summary>Identifies the <c>Succeeded</c> diagnostic outcome.</summary>
    Succeeded,
    /// <summary>Identifies the <c>TimedOut</c> diagnostic outcome.</summary>
    TimedOut,
    /// <summary>Identifies the <c>Truncated</c> diagnostic outcome.</summary>
    Truncated,
    /// <summary>Identifies the <c>Unavailable</c> diagnostic outcome.</summary>
    Unavailable,
    /// <summary>Identifies the <c>Unbalanced</c> diagnostic outcome.</summary>
    Unbalanced,
    /// <summary>Identifies the <c>Unknown</c> diagnostic outcome.</summary>
    Unknown,
    /// <summary>Identifies the <c>Unmatched</c> diagnostic outcome.</summary>
    Unmatched,
    /// <summary>Identifies the <c>Unrecognized</c> diagnostic outcome.</summary>
    Unrecognized,
    /// <summary>Identifies the <c>Unsupported</c> diagnostic outcome.</summary>
    Unsupported,
    /// <summary>Identifies the <c>Used</c> diagnostic outcome.</summary>
    Used,
    /// <summary>Identifies the <c>Overflow</c> diagnostic outcome.</summary>
    Overflow
}
