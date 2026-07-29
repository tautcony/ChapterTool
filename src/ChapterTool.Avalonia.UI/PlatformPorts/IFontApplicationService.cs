using ChapterTool.Contracts.Configuration;

namespace ChapterTool.Avalonia.UI.PlatformPorts;

public interface IFontApplicationService
{
    FontSettings Resolve(FontSettings settings);

    void Apply(FontSettings settings);
}
