using ChapterTool.Contracts.Configuration;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Owns saved and draft settings snapshots and their edit lifecycle.</summary>
internal sealed class SettingsSnapshotCoordinator
{
    public SettingsSnapshotCoordinator(ChapterToolSettings initial)
    {
        var normalized = ChapterToolSettings.Normalize(initial);
        Saved = normalized;
        Draft = normalized;
    }

    public ChapterToolSettings Saved { get; private set; }

    public ChapterToolSettings Draft { get; private set; }

    public bool LiveApplyEnabled { get; private set; }

    public bool IsApplyingSnapshot { get; private set; }

    public bool LoadFailed { get; private set; }

    public bool HasUnsavedChanges => Draft != Saved;

    public void BeginLoad()
    {
        LoadFailed = false;
        LiveApplyEnabled = false;
    }

    public void SetLoaded(ChapterToolSettings settings)
    {
        var normalized = ChapterToolSettings.Normalize(settings);
        Saved = normalized;
        Draft = normalized;
    }

    public void UpdateDraft(ChapterToolSettings settings) => Draft = ChapterToolSettings.Normalize(settings);

    public void Commit(ChapterToolSettings settings)
    {
        var normalized = ChapterToolSettings.Normalize(settings);
        Saved = normalized;
        Draft = normalized;
        LoadFailed = false;
    }

    public void CaptureDraftAsSaved() => Saved = Draft;

    public void SetLoadFailed(bool value) => LoadFailed = value;

    public void EnableLiveApply() => LiveApplyEnabled = true;

    public void ResetDraft() => Draft = ChapterToolSettings.Normalize(ChapterToolSettings.Default);

    public void DiscardDraft() => Draft = Saved;

    public void BeginSnapshot() => IsApplyingSnapshot = true;

    public void EndSnapshot() => IsApplyingSnapshot = false;
}
