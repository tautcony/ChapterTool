using ChapterTool.Avalonia.UI.Views;

namespace ChapterTool.Avalonia.Tests.Views;

public sealed class MainViewLayoutTests
{
    [Theory]
    [InlineData(800, true)]
    [InlineData(859, true)]
    [InlineData(860, true)]
    [InlineData(861, false)]
    [InlineData(960, false)]
    public void Advanced_options_use_narrow_layout_at_or_below_the_breakpoint(double width, bool expectedNarrow)
    {
        Assert.Equal(expectedNarrow, MainView.IsNarrowAdvancedOptions(width));
    }
}
