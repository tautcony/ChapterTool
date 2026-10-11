using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>Describes a selectable rate with its provenance.</summary>
#pragma warning disable SA1313 // Preserve established named constructor arguments.
public sealed record FrameRateOption
{
    /// <summary>Creates an option with a canonical rate.</summary>
    public FrameRateOption(string Code, string DisplayName, FrameRateValue Rate, bool IsValid, int LegacyMplsCode)
    {
        this.Code = Code;
        this.DisplayName = DisplayName;
        this.Rate = Rate;
        this.IsValid = IsValid;
        this.LegacyMplsCode = LegacyMplsCode;
    }

    /// <summary>Adapts the retained numeric option constructor.</summary>
    public FrameRateOption(string Code, string DisplayName, decimal Value, bool IsValid, int LegacyMplsCode,
        ChapterFrameRate? ExactRate = null)
        : this(Code, DisplayName, ExactRate is { } ratio ? FrameRateValue.Rational(ratio) : Normalize(Value), IsValid, LegacyMplsCode)
    {
    }

    /// <summary>Gets the stable selection code.</summary>
    public string Code { get; init; }

    /// <summary>Gets the display label.</summary>
    public string DisplayName { get; init; }

    /// <summary>Gets the canonical rate.</summary>
    public FrameRateValue Rate { get; init; }

    /// <summary>Gets whether the option is available for computation.</summary>
    public bool IsValid { get; init; }

    /// <summary>Gets the MPLS code, or -1 for a custom rate.</summary>
    public int LegacyMplsCode { get; init; }

    /// <summary>Gets or initializes the retained numeric projection.</summary>
    public decimal Value { get => Rate.ToDecimal(); init => Rate = Normalize(value); }

    /// <summary>Gets or initializes the retained rational projection.</summary>
    public ChapterFrameRate? ExactRate
    {
        get => Rate.RationalRate;
        init => Rate = value is { } ratio ? FrameRateValue.Rational(ratio) : Normalize(Value);
    }

    /// <summary>Deconstructs the retained option fields.</summary>
    public void Deconstruct(out string code, out string displayName, out decimal value, out bool isValid,
        out int legacyMplsCode, out ChapterFrameRate? exactRate)
    {
        code = this.Code;
        displayName = this.DisplayName;
        value = this.Value;
        isValid = this.IsValid;
        legacyMplsCode = this.LegacyMplsCode;
        exactRate = this.ExactRate;
    }

    private static FrameRateValue Normalize(decimal value) =>
        FrameRateValue.TryFromDecimal(value, metadata: true, out var rate, out _)
            ? rate : throw new ArgumentOutOfRangeException(nameof(value));
}
#pragma warning restore SA1313
