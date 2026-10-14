using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Core.Transform.Expressions.Lua;

namespace ChapterTool.Core.Tests.Transform;

public sealed class CanonicalExpressionTests
{
    [Fact]
    public void UnknownRateAllowsTimeArithmeticAndExplicitNilHandling()
    {
        var set = Set(12_345_678, FrameRateValue.Unknown);
        var service = new ChapterExpressionService();
        var result = service.Apply(set, true, "if fps == nil and chapter.fps == nil and fpsRateKind == 'Unknown' then return t + 1 end");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(22_345_678, result.Info.Chapters[0].StartTime.Ticks);
        Assert.Equal(FrameRateValue.Unknown, result.Info.FrameRate);
        Assert.Empty(result.Info.Chapters[0].FramesInfo);

        var invalid = service.Apply(set, true, "t + 1 / fps");
        Assert.NotEmpty(invalid.Diagnostics);
        Assert.Same(set, invalid.Info);
    }

    [Theory]
    [InlineData("t")]
    [InlineData("chapter.time")]
    public void UnchangedLuaProjectionPreservesTicksAboveSafeInteger(string expression)
    {
        const long ticks = 9_007_199_254_740_993L;
        var set = Set(ticks, FrameRateValue.Rational(24000, 1001));
        var result = new ChapterExpressionService().Apply(set, true, expression);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(ticks, result.Info.Chapters[0].StartTime.Ticks);
        Assert.Equal(set.FrameRate, result.Info.FrameRate);
    }

    [Fact]
    public void RationalGlobalsExposeTextAndOnlySafeNumericComponents()
    {
        var rate = FrameRateValue.Rational(48000, 2002);
        var exact = Evaluate(rate, "if fpsRateKind == 'Rational' and fpsNumeratorText == '24000' and fpsDenominatorText == '1001' and fpsNumerator == 24000 and fpsDenominator == 1001 and fps ~= nil then return t end");
        Assert.True(exact.Success);
        Assert.True(exact.TimeUnchanged);
        var huge = Evaluate(FrameRateValue.Rational(long.MaxValue, 2),
            "if fpsNumeratorText == '9223372036854775807' and fpsDenominatorText == '2' and fpsNumerator == nil and fpsDenominator == nil then return t end");
        Assert.True(huge.Success);
        var approximate = Evaluate(FrameRateValue.Approximate(23.976m),
            "if fpsRateKind == 'Approximate' and fps == 23.976 and fpsNumeratorText == nil and fpsDenominatorText == nil and fpsNumerator == nil and fpsDenominator == nil then return t end");
        Assert.True(approximate.Success);
    }

    [Fact]
    public void ExplicitExpressionRateDoesNotRewriteUnknownSourceMetadata()
    {
        var set = Set(10_000_000, FrameRateValue.Unknown);
        var result = new ChapterExpressionService().Apply(set, true, "t + 1 / fps", FrameRateValue.Rational(24, 1));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(10_416_667, result.Info.Chapters[0].StartTime.Ticks);
        Assert.Equal(FrameRateValue.Unknown, result.Info.FrameRate);
        Assert.Equal("25", result.Info.Chapters[0].FramesInfo);
    }

    [Fact]
    public void AnalysisUsesCanonicalGlobalsAndDoesNotSupplyUnknownRate()
    {
        var service = new ExpressionAuthoringService();
        var result = service.Analyze("if fps == nil then return t + 1 end", 0, 0L, FrameRateValue.Unknown);
        Assert.Empty(result.Diagnostics);
        Assert.NotEmpty(service.Analyze("t + 1 / fps", 0, 0L, FrameRateValue.Unknown).Diagnostics);
        foreach (var symbol in new[] { "fpsRateKind", "fpsNumeratorText", "fpsDenominatorText", "fpsNumerator", "fpsDenominator" })
        {
            Assert.Contains(service.Symbols, value => value.Text == symbol);
        }
    }

    private static ChapterExpressionEvaluationResult Evaluate(FrameRateValue rate, string expression)
    {
        var set = Set(12_345_678, rate);
        return new LuaExpressionScriptService().Evaluate(expression,
            new CanonicalChapterExpressionContext(set.Chapters[0], 1, 1, set.Chapters[0].StartTime.Ticks, rate, set.Chapters));
    }

    private static ChapterSet Set(long ticks, FrameRateValue rate) =>
        new("Title", null, ChapterImportFormat.Unknown, rate, TimeSpan.Zero, [new Chapter(1, TimeSpan.FromTicks(ticks), "A")]);
}
