using System.Globalization;
using System.Numerics;

namespace ChapterTool.Core.Transform;

/// <summary>Formats frame values for display without changing their stored numeric form.</summary>
public static class FrameValueFormatter
{
    /// <summary>Gets the maximum number of repeating digits shown as a cycle.</summary>
    public const int MaximumRepeatingDigits = 6;

    /// <summary>Gets the maximum number of long-division steps per value.</summary>
    public const int MaximumDivisionSteps = 512;

    /// <summary>Formats a frame value with the supplied presentation policy.</summary>
    /// <param name="input">The timestamp, rate provenance, and ordinary fallback text.</param>
    /// <param name="policy">The rounding and decimal presentation choices.</param>
    /// <returns>Structured display parts. The input value remains unchanged.</returns>
    public static FramePresentationParts Format(
        FramePresentationInput input,
        FramePresentationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(policy);

        if (policy.RoundToInteger)
        {
            return FormatRounded(input);
        }

        if (input.ExactRate is not { } rate)
        {
            return FromPlainText(input.NumericFallback);
        }

        var numerator = new BigInteger(input.StartTicks) * rate.Numerator;
        var denominator = new BigInteger(TimeSpan.TicksPerSecond) * rate.Denominator;
        var reduced = Reduce(numerator, denominator);
        var ordinaryText = FormatOrdinary(reduced.Numerator, reduced.Denominator, policy.DecimalPlaces);
        var negative = reduced.Numerator.Sign < 0;
        var absoluteNumerator = BigInteger.Abs(reduced.Numerator);
        var integer = BigInteger.DivRem(absoluteNumerator, reduced.Denominator, out var remainder);
        var integerDigits = integer.ToString(CultureInfo.InvariantCulture);
        var sign = negative ? "-" : string.Empty;

        if (!policy.ShowRepeatingDecimals)
        {
            return FromPlainText(ordinaryText, FractionText(reduced.Numerator, reduced.Denominator));
        }

        if (remainder.IsZero && policy.ShowRepeatingDecimals)
        {
            return new FramePresentationParts(
                sign,
                integerDigits,
                string.Empty,
                string.Empty,
                sign + integerDigits,
                FractionText(reduced.Numerator, reduced.Denominator),
                Describe(sign, integerDigits, string.Empty, string.Empty));
        }

        var digits = new List<char>(MaximumDivisionSteps);
        var remainderPositions = new Dictionary<BigInteger, int>();
        while (!remainder.IsZero && digits.Count < MaximumDivisionSteps)
        {
            if (remainderPositions.TryGetValue(remainder, out var cycleStart))
            {
                var prefix = new string(digits.Take(cycleStart).ToArray());
                var repeating = new string(digits.Skip(cycleStart).ToArray());
                if (repeating.Length is > 0 and <= MaximumRepeatingDigits)
                {
                    return new FramePresentationParts(
                        sign,
                        integerDigits,
                        prefix,
                        repeating,
                        ordinaryText,
                        FractionText(reduced.Numerator, reduced.Denominator),
                        Describe(sign, integerDigits, prefix, repeating));
                }

                return FromPlainText(ordinaryText, FractionText(reduced.Numerator, reduced.Denominator));
            }

            remainderPositions.Add(remainder, digits.Count);
            remainder *= 10;
            var digit = BigInteger.DivRem(remainder, reduced.Denominator, out remainder);
            digits.Add((char)('0' + (int)digit));
        }

        if (remainder.IsZero)
        {
            // A finite decimal follows the existing decimal-place policy.
            return FromPlainText(ordinaryText, FractionText(reduced.Numerator, reduced.Denominator));
        }

        return FromPlainText(ordinaryText, FractionText(reduced.Numerator, reduced.Denominator));
    }

    private static FramePresentationParts FormatRounded(FramePresentationInput input)
    {
        if (input.ExactRate is { } rate)
        {
            var ratio = Reduce(
                new BigInteger(input.StartTicks) * rate.Numerator,
                new BigInteger(TimeSpan.TicksPerSecond) * rate.Denominator);
            var absolute = BigInteger.Abs(ratio.Numerator);
            var rounded = BigInteger.DivRem(absolute, ratio.Denominator, out var remainder);
            if (remainder * 2 >= ratio.Denominator)
            {
                rounded++;
            }

            var sign = ratio.Numerator.Sign < 0 && !rounded.IsZero ? "-" : string.Empty;
            var integer = rounded.ToString(CultureInfo.InvariantCulture);
            return new FramePresentationParts(sign, integer, string.Empty, string.Empty, sign + integer,
                FractionText(ratio.Numerator, ratio.Denominator), Describe(sign, integer, string.Empty, string.Empty));
        }

        if (decimal.TryParse(input.NumericFallback, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            var rounded = decimal.Round(number, 0, MidpointRounding.AwayFromZero);
            return FromPlainText(rounded.ToString("0", CultureInfo.InvariantCulture), null);
        }

        return FromPlainText(input.NumericFallback);
    }

    private static FramePresentationParts FromPlainText(string text, string? exactFraction = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new FramePresentationParts(string.Empty, string.Empty, string.Empty, string.Empty, text, exactFraction, "missing");
        }

        var sign = text.StartsWith("-", StringComparison.Ordinal) ? "-" : string.Empty;
        var unsigned = sign.Length == 0 ? text : text[1..];
        var separator = unsigned.IndexOf('.');
        var integer = separator < 0 ? unsigned : unsigned[..separator];
        var fraction = separator < 0 ? string.Empty : unsigned[(separator + 1)..];
        if (integer.Length == 0)
        {
            integer = "0";
        }

        return new FramePresentationParts(sign, integer, fraction, string.Empty, text,
            exactFraction,
            Describe(sign, integer, fraction, string.Empty));
    }

    private static string FormatOrdinary(BigInteger numerator, BigInteger denominator, int decimalPlaces)
    {
        if (decimalPlaces < 0)
        {
            return ExactFrameMath.Reduce(numerator, denominator).ToDecimal()
                .ToString("G29", CultureInfo.InvariantCulture);
        }

        var places = Math.Clamp(decimalPlaces, 0, 6);
        var scale = BigInteger.Pow(10, places);
        var absolute = BigInteger.Abs(numerator);
        var scaled = BigInteger.DivRem(absolute * scale, denominator, out var remainder);
        if (remainder * 2 >= denominator)
        {
            scaled++;
        }

        var sign = numerator.Sign < 0 && !scaled.IsZero ? "-" : string.Empty;
        var digits = scaled.ToString(CultureInfo.InvariantCulture);
        if (places == 0)
        {
            return sign + digits;
        }

        digits = digits.PadLeft(places + 1, '0');
        var decimalIndex = digits.Length - places;
        return sign + digits[..decimalIndex] + "." + digits[decimalIndex..];
    }

    private static (BigInteger Numerator, BigInteger Denominator) Reduce(BigInteger numerator, BigInteger denominator)
    {
        var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        return divisor.IsZero ? (numerator, denominator) : (numerator / divisor, denominator / divisor);
    }

    private static string FractionText(BigInteger numerator, BigInteger denominator) =>
        $"{numerator.ToString(CultureInfo.InvariantCulture)}/{denominator.ToString(CultureInfo.InvariantCulture)}";

    private static string Describe(string sign, string integer, string prefix, string repeating)
    {
        var side = sign.Length == 0 ? string.Empty : "minus ";
        if (repeating.Length > 0)
        {
            return $"{side}{integer}; non-repeating digits {DisplayEmpty(prefix)}; repeating digits {repeating}";
        }

        return prefix.Length > 0
            ? $"{side}{integer}; fractional digits {prefix}"
            : $"{side}{integer}";
    }

    private static string DisplayEmpty(string value) => value.Length == 0 ? "none" : value;
}
