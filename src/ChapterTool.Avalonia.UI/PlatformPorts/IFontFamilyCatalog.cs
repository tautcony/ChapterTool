namespace ChapterTool.Avalonia.UI.PlatformPorts;

public interface IFontFamilyCatalog
{
    IReadOnlyList<FontFamilyCatalogEntry> Families { get; }

    bool TryResolve(string? familyName, out string resolvedFamilyName);
}
