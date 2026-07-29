using ChapterTool.Contracts.Configuration;

namespace ChapterTool.Avalonia.UI.PlatformPorts;

public interface IUiSettingsStore
{
    ValueTask<ChapterToolSettings> LoadAsync(CancellationToken cancellationToken);

    ValueTask SaveAsync(ChapterToolSettings settings, CancellationToken cancellationToken);

    ValueTask UpdateAsync(
        Func<ChapterToolSettings, ChapterToolSettings> update,
        CancellationToken cancellationToken);
}
