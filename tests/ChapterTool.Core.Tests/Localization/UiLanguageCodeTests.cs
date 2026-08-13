using ChapterTool.Core.Localization;

namespace ChapterTool.Core.Tests.Localization;

public sealed class UiLanguageCodeTests
{
    [Theory]
    [InlineData("zh", "zh-CN")]
    [InlineData("zh-Hans", "zh-CN")]
    [InlineData("ja", "ja-JP")]
    [InlineData("en", "en-US")]
    [InlineData("en-US", "en-US")]
    public void NormalizeAcceptsShortAndCanonicalCodes(string input, string expected)
    {
        Assert.True(UiLanguageCode.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
        Assert.Equal(expected, UiLanguageCode.Normalize(input));
    }

    [Fact]
    public void UnrecognizedCodesFallBackToEnglish()
    {
        Assert.False(UiLanguageCode.TryNormalize("fr", out var normalized));
        Assert.Equal(UiLanguageCode.English, normalized);
        Assert.Equal(UiLanguageCode.English, UiLanguageCode.Normalize(null));
    }
}
