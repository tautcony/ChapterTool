using System.Globalization;
using Avalonia.Data.Converters;
using ChapterTool.Contracts.Shortcuts;

namespace ChapterTool.Avalonia.UI.ViewModels;

public sealed class ShortcutGestureDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ShortcutGestureText.FormatForDisplay(value?.ToString());

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
