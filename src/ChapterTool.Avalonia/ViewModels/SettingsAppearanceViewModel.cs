using System.Collections.ObjectModel;
using Avalonia.Media;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.ViewModels;

/// <summary>
/// Theme preset and font family selection for the settings appearance page.
/// </summary>
public sealed class SettingsAppearanceViewModel : ObservableViewModel
{
    private readonly IAppLocalizer localizer;
    private readonly IThemeApplicationService? themeApplicationService;
    private readonly IFontFamilyCatalog? fontFamilyCatalog;
    private readonly IFontApplicationService? fontApplicationService;
    private readonly ObservableCollection<ThemePresetOptionViewModel> themePresets = [];
    private readonly ObservableCollection<FontFamilyOptionViewModel> uiFontFamilies = [];
    private readonly ObservableCollection<FontFamilyOptionViewModel> monospaceFontFamilies = [];
    private string selectedThemePresetId = ThemePresetCatalog.DefaultPresetId;
    private string selectedUiFontFamily = string.Empty;
    private string selectedMonospaceFontFamily = string.Empty;
    private bool liveApplyEnabled;
    private bool isApplyingSnapshot;

    public SettingsAppearanceViewModel(
        IAppLocalizer localizer,
        IThemeApplicationService? themeApplicationService = null,
        IFontFamilyCatalog? fontFamilyCatalog = null,
        IFontApplicationService? fontApplicationService = null)
    {
        this.localizer = localizer;
        this.themeApplicationService = themeApplicationService;
        this.fontFamilyCatalog = fontFamilyCatalog;
        this.fontApplicationService = fontApplicationService;
        ReplaceThemePresets();
        ReplaceFontFamilies();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<ThemePresetOptionViewModel> ThemePresets => themePresets;

    public int SelectedThemePresetIndex
    {
        get => IndexOf(themePresets, option => string.Equals(option.Id, selectedThemePresetId, StringComparison.Ordinal));
        set
        {
            if (value >= 0 && value < themePresets.Count)
            {
                SetSelectedThemePresetId(themePresets[value].Id);
            }
        }
    }

    public ThemePresetOptionViewModel SelectedThemePreset =>
        themePresets.First(option => string.Equals(option.Id, selectedThemePresetId, StringComparison.Ordinal));

    public string ThemePreviewAutomationName =>
        localizer.Format(
            "Settings.Appearance.PreviewFor",
            new Dictionary<string, object?> { ["name"] = SelectedThemePreset.DisplayName });

    public IReadOnlyList<FontFamilyOptionViewModel> UiFontFamilies => uiFontFamilies;

    public IReadOnlyList<FontFamilyOptionViewModel> MonospaceFontFamilies => monospaceFontFamilies;

    public int SelectedUiFontFamilyIndex
    {
        get => IndexOf(uiFontFamilies, option => string.Equals(option.FamilyName, selectedUiFontFamily, StringComparison.Ordinal));
        set
        {
            if (value >= 0 && value < uiFontFamilies.Count)
            {
                SetSelectedUiFontFamily(uiFontFamilies[value].FamilyName);
            }
        }
    }

    public int SelectedMonospaceFontFamilyIndex
    {
        get => IndexOf(
            monospaceFontFamilies,
            option => string.Equals(option.FamilyName, selectedMonospaceFontFamily, StringComparison.Ordinal));
        set
        {
            if (value >= 0 && value < monospaceFontFamilies.Count)
            {
                SetSelectedMonospaceFontFamily(monospaceFontFamilies[value].FamilyName);
            }
        }
    }

    public FontFamilyOptionViewModel SelectedUiFontFamily =>
        uiFontFamilies.First(option => string.Equals(option.FamilyName, selectedUiFontFamily, StringComparison.Ordinal));

    public FontFamilyOptionViewModel SelectedMonospaceFontFamily =>
        monospaceFontFamilies.First(option =>
            string.Equals(option.FamilyName, selectedMonospaceFontFamily, StringComparison.Ordinal));

    public string FontPreviewText => localizer.GetString("Settings.Appearance.FontPreviewSample");

    public string UiFontPreviewAutomationName =>
        FontPreviewAutomationName("Settings.Appearance.UiFontPreviewFor", SelectedUiFontFamily.DisplayName);

    public string MonospaceFontPreviewAutomationName => FontPreviewAutomationName(
        "Settings.Appearance.MonospaceFontPreviewFor",
        SelectedMonospaceFontFamily.DisplayName);

    public ThemeSettings CurrentThemeSettings() => new(selectedThemePresetId);

    public FontSettings CurrentFontSettings() => new(selectedUiFontFamily, selectedMonospaceFontFamily);

    public void SetLiveApplyEnabled(bool enabled) => liveApplyEnabled = enabled;

    public void BeginSnapshot() => isApplyingSnapshot = true;

    public void EndSnapshot() => isApplyingSnapshot = false;

    public void ApplyThemeSettings(ThemeSettings settings) =>
        SetSelectedThemePresetId(ThemePresetCatalog.Resolve(settings.PresetId).Id, apply: false);

    public void ApplyFontSettings(FontSettings settings)
    {
        var resolved = ResolveFontSettings(settings);
        SetSelectedUiFontFamily(resolved.UiFontFamily, apply: false);
        SetSelectedMonospaceFontFamily(resolved.MonospaceFontFamily, apply: false);
    }

    public void ApplyToServices(ThemeSettings theme, FontSettings font)
    {
        themeApplicationService?.Apply(theme);
        fontApplicationService?.Apply(font);
    }

    public void ApplyCurrentToServices() =>
        ApplyToServices(CurrentThemeSettings(), CurrentFontSettings());

    public FontSettings ResolveFontSettings(FontSettings settings) =>
        fontApplicationService?.Resolve(settings)
        ?? (fontFamilyCatalog is null
            ? FontSettings.Normalize(settings)
            : FontSettingsResolver.Resolve(settings, fontFamilyCatalog));

    public void RefreshLocalizedOptions()
    {
        ReplaceThemePresets();
        ReplaceFontFamilies();
    }

    private void SetSelectedUiFontFamily(string familyName, bool apply = true)
    {
        var resolved = ResolveFontSettings(new FontSettings(familyName, selectedMonospaceFontFamily));
        if (!SetProperty(ref selectedUiFontFamily, resolved.UiFontFamily, nameof(SelectedUiFontFamily)))
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedUiFontFamilyIndex));
        OnPropertyChanged(nameof(UiFontPreviewAutomationName));
        ApplyLiveFontSettings(apply);
    }

    private void SetSelectedMonospaceFontFamily(string familyName, bool apply = true)
    {
        var resolved = ResolveFontSettings(new FontSettings(selectedUiFontFamily, familyName));
        if (!SetProperty(ref selectedMonospaceFontFamily, resolved.MonospaceFontFamily, nameof(SelectedMonospaceFontFamily)))
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedMonospaceFontFamilyIndex));
        OnPropertyChanged(nameof(MonospaceFontPreviewAutomationName));
        ApplyLiveFontSettings(apply);
    }

    private void ApplyLiveFontSettings(bool apply)
    {
        if (apply && liveApplyEnabled && !isApplyingSnapshot)
        {
            fontApplicationService?.Apply(CurrentFontSettings());
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetSelectedThemePresetId(string presetId, bool apply = true)
    {
        var normalized = ThemePresetCatalog.Resolve(presetId).Id;
        if (!SetProperty(ref selectedThemePresetId, normalized, nameof(SelectedThemePreset)))
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedThemePresetIndex));
        OnPropertyChanged(nameof(ThemePreviewAutomationName));
        if (apply && liveApplyEnabled && !isApplyingSnapshot)
        {
            themeApplicationService?.Apply(CurrentThemeSettings());
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ReplaceThemePresets()
    {
        var selectedId = selectedThemePresetId;
        themePresets.Clear();
        foreach (var preset in ThemePresetCatalog.All)
        {
            themePresets.Add(new ThemePresetOptionViewModel(
                preset.Id,
                localizer.GetString(preset.DisplayNameKey),
                preset.Palette.PreviewSwatches.Select(static color => new ThemeSwatchViewModel(color)).ToArray()));
        }

        selectedThemePresetId = ThemePresetCatalog.Resolve(selectedId).Id;
        OnPropertyChanged(nameof(ThemePresets));
        OnPropertyChanged(nameof(SelectedThemePreset));
        OnPropertyChanged(nameof(SelectedThemePresetIndex));
        OnPropertyChanged(nameof(ThemePreviewAutomationName));
    }

    private void ReplaceFontFamilies()
    {
        var uiSelection = selectedUiFontFamily;
        var monospaceSelection = selectedMonospaceFontFamily;
        var families = fontFamilyCatalog?.Families ?? [];

        uiFontFamilies.Clear();
        uiFontFamilies.Add(new FontFamilyOptionViewModel(
            string.Empty,
            () => localizer.GetString("Settings.Appearance.SystemUiFont"),
            true,
            FontFamily.Default));
        monospaceFontFamilies.Clear();
        monospaceFontFamilies.Add(new FontFamilyOptionViewModel(
            string.Empty,
            () => localizer.GetString("Settings.Appearance.SystemMonospaceFont"),
            true,
            FontFamily.Parse(AvaloniaFontApplicationService.DefaultMonospaceFontFamily)));
        var cultureName = localizer.CurrentCultureName;
        foreach (var family in families)
        {
            var option = new FontFamilyOptionViewModel(
                family.FamilyName,
                () => family.GetDisplayName(cultureName),
                false,
                FontFamily.Parse(family.FamilyName));
            uiFontFamilies.Add(option);
            monospaceFontFamilies.Add(option);
        }

        var resolved = ResolveFontSettings(new FontSettings(uiSelection, monospaceSelection));
        selectedUiFontFamily = resolved.UiFontFamily;
        selectedMonospaceFontFamily = resolved.MonospaceFontFamily;
        OnPropertyChanged(nameof(UiFontFamilies));
        OnPropertyChanged(nameof(MonospaceFontFamilies));
        OnPropertyChanged(nameof(SelectedUiFontFamily));
        OnPropertyChanged(nameof(SelectedMonospaceFontFamily));
        OnPropertyChanged(nameof(SelectedUiFontFamilyIndex));
        OnPropertyChanged(nameof(SelectedMonospaceFontFamilyIndex));
        OnPropertyChanged(nameof(FontPreviewText));
        OnPropertyChanged(nameof(UiFontPreviewAutomationName));
        OnPropertyChanged(nameof(MonospaceFontPreviewAutomationName));
    }

    private string FontPreviewAutomationName(string key, string displayName) =>
        localizer.Format(key, new Dictionary<string, object?> { ["name"] = displayName });

    private static int IndexOf<T>(IReadOnlyList<T> items, Func<T, bool> predicate)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (predicate(items[index]))
            {
                return index;
            }
        }

        return -1;
    }
}

public sealed record ThemePresetOptionViewModel(
    string Id,
    string DisplayName,
    IReadOnlyList<ThemeSwatchViewModel> PreviewSwatches);

public sealed record ThemeSwatchViewModel(string Color);

public sealed class FontFamilyOptionViewModel(
    string familyName,
    Func<string> displayNameFactory,
    bool isDefault,
    FontFamily previewFontFamily)
{
    private readonly Lazy<string> displayName = new(displayNameFactory, LazyThreadSafetyMode.ExecutionAndPublication);

    public string FamilyName { get; } = familyName;

    public string DisplayName => displayName.Value;

    public bool IsDefault { get; } = isDefault;

    public FontFamily PreviewFontFamily { get; } = previewFontFamily;
}
