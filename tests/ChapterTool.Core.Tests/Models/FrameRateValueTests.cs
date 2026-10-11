using System.Globalization;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Tests.Models;

public sealed class FrameRateValueTests
{
    [Fact]
    public void DefaultIsUnknownWithoutPayload()
    {
        var rate = default(FrameRateValue);
        Assert.Equal(FrameRateValue.Unknown, rate);
        Assert.False(rate.IsKnown);
        Assert.Null(rate.RationalRate);
        Assert.Null(rate.ApproximateValue);
        Assert.Equal("Unknown", rate.ToString());
        Assert.Equal(0, rate.ToDouble());
    }

    [Fact]
    public void EquivalentRatiosHaveCanonicalEqualityAndHash()
    {
        var first = FrameRateValue.Rational(48000, 2002);
        var second = FrameRateValue.Rational(24000, 1001);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal("24000/1001", first.ToString());
    }

    [Fact]
    public void ApproximateScaleNormalizesWithoutRationalAuthority()
    {
        var first = FrameRateValue.Approximate(24.00m);
        var second = FrameRateValue.Approximate(24m);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(decimal.GetBits(first.ApproximateValue!.Value), decimal.GetBits(second.ApproximateValue!.Value));
        Assert.NotEqual(first, FrameRateValue.Rational(24, 1));
        Assert.Null(first.RationalRate);
        Assert.Equal("24", first.ToString());
    }

    [Fact]
    public void InvalidPayloadsCannotBecomeKnownValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FrameRateValue.Rational(default));
        Assert.Throws<ArgumentOutOfRangeException>(() => FrameRateValue.Rational(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => FrameRateValue.Rational(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => FrameRateValue.Approximate(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FrameRateValue.Approximate(-24));
        Assert.False(FrameRateValue.TryRational(default, out var rate, out var diagnostic));
        Assert.Equal(FrameRateValue.Unknown, rate);
        Assert.Equal(ChapterDiagnosticCode.InvalidFrameRate, diagnostic!.Code);
    }

    [Theory]
    [InlineData(24d, "24")]
    [InlineData(23.976d, "23.976")]
    [InlineData(0.1d, "0.1")]
    [InlineData(1e-28, "0.0000000000000000000000000001")]
    [InlineData(1e28, "10000000000000000000000000000")]
    public void NumericDoubleUsesShortestRoundTripText(double input, string expected)
    {
        Assert.True(FrameRateValue.TryFromDouble(input, metadata: false, out var rate, out var diagnostic));
        Assert.Null(diagnostic);
        Assert.Equal(FrameRateKind.Approximate, rate.Kind);
        Assert.Equal(expected, rate.ToString());
    }

    [Fact]
    public void NumericRatioDoesNotAcquireRationalAuthority()
    {
        var input = 24000d / 1001d;
        Assert.True(FrameRateValue.TryFromDouble(input, metadata: false, out var rate, out _));
        Assert.Equal(FrameRateKind.Approximate, rate.Kind);
        Assert.Equal(decimal.Parse(input.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture), rate.ApproximateValue);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-24d)]
    [InlineData(double.MaxValue)]
    [InlineData(double.Epsilon)]
    [InlineData(1e-29)]
    [InlineData(1.2345678901234567e-20)]
    public void InvalidOrRoundedDoubleReturnsDiagnostic(double input)
    {
        Assert.False(FrameRateValue.TryFromDouble(input, metadata: true, out _, out var diagnostic));
        Assert.Equal(ChapterDiagnosticCode.InvalidFrameRate, diagnostic!.Code);
    }

    [Fact]
    public void ZeroIsUnknownOnlyAtMetadataBoundary()
    {
        Assert.True(FrameRateValue.TryFromDouble(0, metadata: true, out var rate, out var diagnostic));
        Assert.Equal(FrameRateValue.Unknown, rate);
        Assert.Null(diagnostic);
        Assert.False(FrameRateValue.TryFromDouble(0, metadata: false, out _, out diagnostic));
        Assert.NotNull(diagnostic);
        Assert.True(FrameRateValue.TryFromDecimal(0, metadata: true, out rate, out diagnostic));
        Assert.Equal(FrameRateValue.Unknown, rate);
        Assert.False(FrameRateValue.TryFromDecimal(0, metadata: false, out _, out diagnostic));
        Assert.NotNull(diagnostic);
    }

    [Fact]
    public void DecimalBoundsRemainExact()
    {
        foreach (var input in new[] { decimal.MaxValue, 0.0000000000000000000000000001m, 23.976m })
        {
            Assert.True(FrameRateValue.TryFromDecimal(input, metadata: false, out var rate, out var diagnostic));
            Assert.Null(diagnostic);
            Assert.Equal(input, rate.ApproximateValue);
        }

        Assert.False(FrameRateValue.TryFromDecimal(-1, metadata: true, out _, out var invalid));
        Assert.NotNull(invalid);
        Assert.False(FrameRateValue.TryFromDouble((double)decimal.MaxValue, metadata: false, out _, out invalid));
        Assert.NotNull(invalid);
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    [InlineData("ja-JP")]
    [InlineData("fr-FR")]
    public void FormattingIsInvariant(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal("23.976", FrameRateValue.Approximate(23.976m).ToString());
            Assert.Equal("24000/1001", FrameRateValue.Rational(24000, 1001).ToString());
            Assert.True(FrameRateValue.TryFromDouble(23.976, metadata: false, out var rate, out _));
            Assert.Equal("23.976", rate.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
