using ChapterTool.Core.Exporting;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.ViewModels.Settings;

/// <summary>
/// Ownership module for output defaults (save dir/format/xml/encoding/bom/tolerance).
/// Bound properties remain on <see cref="SettingsToolViewModel"/> for the single-page settings UX;
/// this type documents and groups the preference keys applied live to the shell.
/// </summary>
public sealed class SettingsOutputDefaultsModule
{
    public string? SavingPath { get; set; }
    public int DefaultSaveFormatIndex { get; set; }
    public int DefaultXmlLanguageIndex { get; set; }
    public int OutputTextEncodingIndex { get; set; }
    public bool EmitBom { get; set; } = true;
    public decimal FrameAccuracyTolerance { get; set; } = 0.15m;

    public AppSettings ToAppSettings(string language, AppSettings current) =>
        current with
        {
            Language = language,
            SavingPath = SavingPath,
            DefaultSaveFormat = ChapterExportFormats.Code(ChapterExportFormats.AtIndex(DefaultSaveFormatIndex)),
            DefaultXmlLanguage = current.DefaultXmlLanguage,
            OutputTextEncoding = OutputTextEncodings.Id(OutputTextEncodings.All[Math.Clamp(OutputTextEncodingIndex, 0, OutputTextEncodings.All.Count - 1)]),
            EmitBom = EmitBom,
            FrameAccuracyTolerance = FrameAccuracyTolerance,
        };
}
