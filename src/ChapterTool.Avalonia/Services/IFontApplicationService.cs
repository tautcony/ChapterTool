using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Services;

public interface IFontApplicationService
{
    FontSettings Resolve(FontSettings settings);

    void Apply(FontSettings settings);
}
