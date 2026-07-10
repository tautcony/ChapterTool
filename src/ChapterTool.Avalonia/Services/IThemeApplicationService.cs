using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Services;

public interface IThemeApplicationService
{
    void Apply(ThemeSettings settings);
}
