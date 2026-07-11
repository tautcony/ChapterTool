namespace ChapterTool.Avalonia.Services;

public interface IFontFamilyCatalog
{
    IReadOnlyList<FontFamilyCatalogEntry> Families { get; }

    bool TryResolve(string? familyName, out string resolvedFamilyName);
}
