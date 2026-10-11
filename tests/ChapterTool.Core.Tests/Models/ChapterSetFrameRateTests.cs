using ChapterTool.Core.Models;

namespace ChapterTool.Core.Tests.Models;

public sealed class ChapterSetFrameRateTests
{
    [Fact]
    public void RetainedNamedConstructionAndDeconstructionProjectCanonicalRate()
    {
        var set = new ChapterSet(Title: "Title", SourceName: null, ImportFormat: ChapterImportFormat.Unknown,
            FramesPerSecond: 23.976, Duration: TimeSpan.Zero, Chapters: [], ExactFrameRate: new ChapterFrameRate(48000, 2002));
        var (title, source, format, fps, duration, chapters, exact) = set;
        Assert.Equal("Title", title);
        Assert.Null(source);
        Assert.Equal(ChapterImportFormat.Unknown, format);
        Assert.Equal(24000d / 1001, fps);
        Assert.Equal(TimeSpan.Zero, duration);
        Assert.Empty(chapters);
        Assert.Equal(new ChapterFrameRate(24000, 1001), exact);
        Assert.Equal(FrameRateValue.Rational(24000, 1001), set.FrameRate);
    }

    [Fact]
    public void NumericUpdateClearsExactAuthorityAndCanonicalConstructionBypassesNumericNormalization()
    {
        var original = new ChapterSet("Title", null, ChapterImportFormat.Unknown,
            FrameRateValue.Rational(long.MaxValue, long.MaxValue - 1), TimeSpan.Zero, []);
        var updated = original with { FramesPerSecond = 24 };
        Assert.Equal(FrameRateValue.Approximate(24), updated.FrameRate);
        Assert.Null(updated.ExactFrameRate);
        Assert.Equal(FrameRateKind.Rational, original.FrameRate.Kind);
        Assert.Equal(long.MaxValue, original.ExactFrameRate!.Value.Numerator);
    }

    [Fact]
    public void RetainedInitOrderUsesLastExplicitAssignment()
    {
        var original = new ChapterSet("Title", null, ChapterImportFormat.Unknown, 0, TimeSpan.Zero, []);
        var ratio = new ChapterFrameRate(24, 1);
        Assert.Equal(FrameRateValue.Rational(ratio), (original with { FramesPerSecond = 24, ExactFrameRate = ratio }).FrameRate);
        Assert.Equal(FrameRateValue.Approximate(24), (original with { ExactFrameRate = ratio, FramesPerSecond = 24 }).FrameRate);
        var exact = original with { ExactFrameRate = ratio };
        Assert.Equal(FrameRateValue.Approximate(24), (exact with { ExactFrameRate = null }).FrameRate);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    [InlineData(double.Epsilon)]
    public void InvalidNumericConstructionFailsImmediately(double input)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChapterSet("Title", null, ChapterImportFormat.Unknown, input, TimeSpan.Zero, []));
    }
}
