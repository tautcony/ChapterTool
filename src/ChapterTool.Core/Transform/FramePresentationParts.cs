namespace ChapterTool.Core.Transform;

/// <summary>Structured display data for one frame value.</summary>
public sealed record FramePresentationParts(
    string Sign,
    string IntegerDigits,
    string NonRepeatingDigits,
    string RepeatingDigits,
    string PlainNumericText,
    string? ExactFractionText,
    string AccessibleDescription)
{
    /// <summary>Gets whether the value has a recurring fractional block.</summary>
    public bool HasRepeatingDigits => RepeatingDigits.Length > 0;

    /// <summary>Gets whether the value has fractional digits.</summary>
    public bool HasFractionalDigits => NonRepeatingDigits.Length > 0 || HasRepeatingDigits;

    /// <summary>Gets the sign and integer digits as one display segment.</summary>
    public string SignedIntegerDigits => Sign + IntegerDigits;

    /// <summary>Gets the decimal separator when fractional digits are present.</summary>
    public string DecimalPoint => HasFractionalDigits ? "." : string.Empty;
}
