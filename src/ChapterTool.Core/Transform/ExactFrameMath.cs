using System.Globalization;
using System.Numerics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>Performs frame calculations as reduced integer ratios.</summary>
internal readonly record struct ExactFrameMath(BigInteger Numerator, BigInteger Denominator)
{
    public static ExactFrameMath FromTicks(long ticks, ChapterFrameRate rate) =>
        Reduce(new BigInteger(ticks) * rate.Numerator,
            new BigInteger(TimeSpan.TicksPerSecond) * rate.Denominator);

    public static ExactFrameMath FromTicks(long ticks, decimal framesPerSecond)
    {
        var bits = decimal.GetBits(framesPerSecond);
        var scale = (bits[3] >> 16) & 0x7f;
        var numerator = new BigInteger((uint)bits[0])
            | (new BigInteger((uint)bits[1]) << 32)
            | (new BigInteger((uint)bits[2]) << 64);
        if ((bits[3] & int.MinValue) != 0)
        {
            numerator = -numerator;
        }

        var rateDenominator = BigInteger.Pow(10, scale);
        return Reduce(new BigInteger(ticks) * numerator,
            new BigInteger(TimeSpan.TicksPerSecond) * rateDenominator);
    }

    public static ExactFrameMath FromDecimal(decimal value)
    {
        var bits = decimal.GetBits(value);
        var scale = (bits[3] >> 16) & 0x7f;
        var numerator = new BigInteger((uint)bits[0])
            | (new BigInteger((uint)bits[1]) << 32)
            | (new BigInteger((uint)bits[2]) << 64);
        if ((bits[3] & int.MinValue) != 0)
        {
            numerator = -numerator;
        }

        return Reduce(numerator, BigInteger.Pow(10, scale));
    }

    public static long TicksFromFrames(long frames, ChapterFrameRate rate)
    {
        var ticks = Reduce(new BigInteger(frames) * rate.Denominator * TimeSpan.TicksPerSecond,
            rate.Numerator).RoundAwayFromZero();
        return checked((long)ticks);
    }

    public static long TicksFromFrames(decimal frames, decimal framesPerSecond)
    {
        var frameCount = FromDecimal(frames);
        var rate = FromDecimal(framesPerSecond);
        var ticks = Reduce(frameCount.Numerator * rate.Denominator * TimeSpan.TicksPerSecond,
            frameCount.Denominator * rate.Numerator).RoundAwayFromZero();
        return checked((long)ticks);
    }

    public BigInteger RoundAwayFromZero()
    {
        var rounded = BigInteger.DivRem(BigInteger.Abs(Numerator), Denominator, out var remainder);
        if (remainder * 2 >= Denominator)
        {
            rounded++;
        }

        return Numerator.Sign < 0 ? -rounded : rounded;
    }

    public bool IsWithin(decimal tolerance)
    {
        var exactTolerance = FromDecimal(tolerance);
        return BigInteger.Abs(Numerator) * exactTolerance.Denominator
            < exactTolerance.Numerator * Denominator;
    }

    public decimal ToDecimal()
    {
        var maximum = (BigInteger.One << 96) - 1;
        var absolute = BigInteger.Abs(Numerator);
        for (var scale = 28; scale >= 0; scale--)
        {
            var scaled = BigInteger.DivRem(absolute * BigInteger.Pow(10, scale), Denominator, out var remainder);
            if (remainder * 2 >= Denominator)
            {
                scaled++;
            }

            if (scaled > maximum)
            {
                continue;
            }

            var low = (int)(uint)(scaled & uint.MaxValue);
            var middle = (int)(uint)((scaled >> 32) & uint.MaxValue);
            var high = (int)(uint)((scaled >> 64) & uint.MaxValue);
            return new decimal(low, middle, high, Numerator.Sign < 0 && scaled != 0, (byte)scale);
        }

        throw new OverflowException("The frame value is outside the decimal range.");
    }

    public static ExactFrameMath operator -(ExactFrameMath left, ExactFrameMath right) =>
        Reduce(left.Numerator * right.Denominator - right.Numerator * left.Denominator,
            left.Denominator * right.Denominator);

    public static ExactFrameMath operator +(ExactFrameMath left, ExactFrameMath right) =>
        Reduce(left.Numerator * right.Denominator + right.Numerator * left.Denominator,
            left.Denominator * right.Denominator);

    public static ExactFrameMath operator /(ExactFrameMath value, int divisor) =>
        Reduce(value.Numerator, value.Denominator * divisor);

    public int CompareTo(ExactFrameMath other) =>
        (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);

    public ExactFrameMath Abs() => this with { Numerator = BigInteger.Abs(Numerator) };

    public static ExactFrameMath Min(ExactFrameMath left, ExactFrameMath right) =>
        left.Numerator * right.Denominator <= right.Numerator * left.Denominator ? left : right;

    public static ExactFrameMath Reduce(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.Sign < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        return divisor.IsZero
            ? new ExactFrameMath(numerator, BigInteger.One)
            : new ExactFrameMath(numerator / divisor, denominator / divisor);
    }

    public string FormatFixed(int decimalPlaces)
    {
        var places = Math.Clamp(decimalPlaces, 0, 6);
        var scale = BigInteger.Pow(10, places);
        var scaled = BigInteger.DivRem(BigInteger.Abs(Numerator) * scale, Denominator, out var remainder);
        if (remainder * 2 >= Denominator)
        {
            scaled++;
        }

        var sign = Numerator.Sign < 0 && !scaled.IsZero ? "-" : string.Empty;
        var digits = scaled.ToString(CultureInfo.InvariantCulture);
        if (places == 0)
        {
            return sign + digits;
        }

        digits = digits.PadLeft(places + 1, '0');
        return sign + digits[..^places] + "." + digits[^places..];
    }
}
