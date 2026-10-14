using System.Globalization;
using System.Numerics;
using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Models;

/// <summary>Identifies the authority of a frame rate.</summary>
public enum FrameRateKind
{
    /// <summary>No video frame rate is known.</summary>
    Unknown,

    /// <summary>Source metadata or explicit selection supplies an integer ratio.</summary>
    Rational,

    /// <summary>A numeric input supplies a decimal value without rational authority.</summary>
    Approximate
}

/// <summary>An immutable frame rate that preserves its authority and canonical payload.</summary>
public readonly record struct FrameRateValue
{
    private readonly ChapterFrameRate rational;
    private readonly decimal approximate;

    private FrameRateValue(FrameRateKind kind, ChapterFrameRate rational, decimal approximate)
    {
        Kind = kind;
        this.rational = rational;
        this.approximate = approximate;
    }

    /// <summary>Gets the rate authority. A default value is Unknown.</summary>
    public FrameRateKind Kind { get; }

    /// <summary>Gets whether the value supplies a usable computation rate.</summary>
    public bool IsKnown => Kind != FrameRateKind.Unknown;

    /// <summary>Gets the explicit absence of a video frame rate.</summary>
    public static FrameRateValue Unknown => default;

    /// <summary>Resolves every contributing rate. Empty, missing, or differing rates are Unknown.</summary>
    public static FrameRateValue ResolveUniform(IEnumerable<FrameRateValue> rates)
    {
        ArgumentNullException.ThrowIfNull(rates);
        using var enumerator = rates.GetEnumerator();
        if (!enumerator.MoveNext() || !enumerator.Current.IsKnown)
        {
            return Unknown;
        }

        var first = enumerator.Current;
        while (enumerator.MoveNext())
        {
            if (enumerator.Current != first)
            {
                return Unknown;
            }
        }

        return first;
    }

    /// <summary>Gets the rational payload, or null for another kind.</summary>
    public ChapterFrameRate? RationalRate => Kind == FrameRateKind.Rational ? rational : null;

    /// <summary>Gets the approximate payload, or null for another kind.</summary>
    public decimal? ApproximateValue => Kind == FrameRateKind.Approximate ? approximate : null;

    /// <summary>Creates an authoritative rate. Default invalid ratios are rejected.</summary>
    public static FrameRateValue Rational(ChapterFrameRate rate)
    {
        if (rate.Numerator <= 0 || rate.Denominator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), "Rational components must be positive.");
        }

        return new FrameRateValue(FrameRateKind.Rational,
            new ChapterFrameRate(rate.Numerator, rate.Denominator), 0);
    }

    /// <summary>Creates an authoritative rate from positive Int64 components.</summary>
    public static FrameRateValue Rational(long numerator, long denominator) =>
        Rational(new ChapterFrameRate(numerator, denominator));

    /// <summary>Creates a positive decimal rate without rational authority.</summary>
    public static FrameRateValue Approximate(decimal value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Frame rate must be positive.");
        }

        // G29 removes insignificant scale without changing the decimal value.
        var canonical = decimal.Parse(value.ToString("G29", CultureInfo.InvariantCulture),
            NumberStyles.Float, CultureInfo.InvariantCulture);
        return new FrameRateValue(FrameRateKind.Approximate, default, canonical);
    }

    /// <summary>Validates an explicit rational at a diagnostic-returning boundary.</summary>
    public static bool TryRational(ChapterFrameRate rate, out FrameRateValue value, out ChapterDiagnostic? diagnostic)
    {
        value = Unknown;
        if (rate.Numerator <= 0 || rate.Denominator <= 0)
        {
            diagnostic = InvalidRate();
            return false;
        }

        value = Rational(rate);
        diagnostic = null;
        return true;
    }

    /// <summary>Normalizes numeric input. Only legacy metadata can use zero for Unknown.</summary>
    public static bool TryFromDecimal(decimal input, bool metadata, out FrameRateValue value, out ChapterDiagnostic? diagnostic)
    {
        value = Unknown;
        if (metadata && input == 0)
        {
            diagnostic = null;
            return true;
        }

        if (input <= 0)
        {
            diagnostic = InvalidRate();
            return false;
        }

        value = Approximate(input);
        diagnostic = null;
        return true;
    }

    /// <summary>Normalizes invariant shortest round-trip text without decimal rounding or underflow.</summary>
    public static bool TryFromDouble(double input, bool metadata, out FrameRateValue value, out ChapterDiagnostic? diagnostic)
    {
        value = Unknown;
        if (metadata && input == 0)
        {
            diagnostic = null;
            return true;
        }

        if (!double.IsFinite(input) || input <= 0)
        {
            diagnostic = InvalidRate();
            return false;
        }

        var text = input.ToString("R", CultureInfo.InvariantCulture);
        if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || parsed <= 0 || !HasExactDecimalValue(text, parsed))
        {
            diagnostic = InvalidRate();
            return false;
        }

        return TryFromDecimal(parsed, metadata: false, out value, out diagnostic);
    }

    /// <summary>Parses invariant decimal text without grouping, exponent, or rounded-to-fit values.</summary>
    public static bool TryFromInvariantDecimal(string input, out FrameRateValue value, out ChapterDiagnostic? diagnostic)
    {
        value = Unknown;
        if (!TryParseInvariantDecimal(input, out var parsed) || parsed <= 0)
        {
            diagnostic = InvalidRate();
            return false;
        }

        return TryFromDecimal(parsed, metadata: false, out value, out diagnostic);
    }

    internal static bool TryParseInvariantDecimal(string input, out decimal value) =>
        decimal.TryParse(input, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)
            && HasExactDecimalValue(input, value);

    /// <summary>Gets a lossy projection for retained external numeric contracts and presentation.</summary>
    public double ToDouble() => Kind switch
    {
        FrameRateKind.Rational => (double)rational.Numerator / rational.Denominator,
        FrameRateKind.Approximate => (double)approximate,
        _ => 0
    };

    /// <summary>Gets a decimal projection for retained expression extensions and presentation.</summary>
    public decimal ToDecimal() => Kind switch
    {
        FrameRateKind.Rational => (decimal)rational.Numerator / rational.Denominator,
        FrameRateKind.Approximate => approximate,
        _ => 0
    };

    /// <summary>Formats the canonical payload with invariant punctuation and no exponent.</summary>
    public override string ToString() => Kind switch
    {
        FrameRateKind.Rational => string.Create(CultureInfo.InvariantCulture, $"{rational.Numerator}/{rational.Denominator}"),
        FrameRateKind.Approximate => approximate.ToString("0.############################", CultureInfo.InvariantCulture),
        _ => "Unknown"
    };

    private static ChapterDiagnostic InvalidRate() =>
        new(DiagnosticSeverity.Error, ChapterDiagnosticCode.InvalidFrameRate,
            "Frame rate must be positive and exactly representable in its declared numeric format.");

    private static bool HasExactDecimalValue(string text, decimal parsed)
    {
        var exponentIndex = text.IndexOf('E');
        var exponent = exponentIndex < 0 ? 0 : int.Parse(text.AsSpan(exponentIndex + 1), CultureInfo.InvariantCulture);
        var mantissa = exponentIndex < 0 ? text : text[..exponentIndex];
        var point = mantissa.IndexOf('.');
        var scale = (point < 0 ? 0 : mantissa.Length - point - 1) - exponent;
        var coefficient = BigInteger.Parse(mantissa.Replace(".", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);
        var bits = decimal.GetBits(parsed);
        var decimalScale = (bits[3] >> 16) & 0xff;
        var decimalCoefficient = new BigInteger((uint)bits[0])
            | (new BigInteger((uint)bits[1]) << 32)
            | (new BigInteger((uint)bits[2]) << 64);
        var commonScale = Math.Max(scale, decimalScale);
        return coefficient * BigInteger.Pow(10, commonScale - scale)
            == decimalCoefficient * BigInteger.Pow(10, commonScale - decimalScale);
    }
}
