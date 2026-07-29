using ChapterTool.Contracts.Configuration;

namespace ChapterTool.Avalonia.UI.PlatformPorts;

public interface IThemeApplicationService
{
    void Apply(ThemeSettings settings);
}
