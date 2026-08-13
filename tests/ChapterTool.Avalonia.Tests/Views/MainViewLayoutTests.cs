using ChapterTool.Avalonia.UI.Views;

namespace ChapterTool.Avalonia.Tests.Views;

public sealed class MainViewLayoutTests
{
    [Theory]
    [InlineData(759, true)]
    [InlineData(760, true)]
    [InlineData(761, false)]
    [InlineData(800, false)]
    [InlineData(960, false)]
    public void Advanced_options_use_narrow_layout_at_or_below_the_breakpoint(double width, bool expectedNarrow)
    {
        Assert.Equal(expectedNarrow, MainView.IsNarrowAdvancedOptions(width));
    }
}
