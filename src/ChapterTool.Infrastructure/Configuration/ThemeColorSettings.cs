namespace ChapterTool.Infrastructure.Configuration;

public sealed record ThemeColorSettings(
    string BackChange,
    string TextBack,
    string MouseOverColor,
    string MouseDownColor,
    string BorderBackColor,
    string TextFrontColor)
{
    public static ThemeColorSettings Default { get; } = new(
        "#F0F0F0",
        "#FFFFFF",
        "#E5F1FB",
        "#CCE4F7",
        "#ADADAD",
        "#000000");

    public IReadOnlyList<ThemeColorSlot> OrderedSlots =>
    [
        new("BackChange", BackChange),
        new("TextBack", TextBack),
        new("MouseOverColor", MouseOverColor),
        new("MouseDownColor", MouseDownColor),
        new("BorderBackColor", BorderBackColor),
        new("TextFrontColor", TextFrontColor)
    ];
}

public sealed record ThemeColorSlot(string Name, string Value);
