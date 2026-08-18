using ChapterTool.Contracts.Configuration;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Owns saved and draft settings snapshots and their edit lifecycle.</summary>
internal sealed class SettingsSnapshotCoordinator
{
    private ChapterToolSettings saved;
    private ChapterToolSettings draft;

    public SettingsSnapshotCoordinator(ChapterToolSettings initial)
    {
        var normalized = ChapterToolSettings.Normalize(initial);
        saved = normalized;
        draft = normalized;
    }

    public ChapterToolSettings Saved => saved;

    public ChapterToolSettings Draft => draft;

    public bool LiveApplyEnabled { get; private set; }

    public bool IsApplyingSnapshot { get; private set; }

    public bool LoadFailed { get; private set; }

    public bool HasUnsavedChanges => draft != saved;

    public void BeginLoad()
    {
        LoadFailed = false;
        LiveApplyEnabled = false;
    }

    public void SetLoaded(ChapterToolSettings settings)
    {
        var normalized = ChapterToolSettings.Normalize(settings);
        saved = normalized;
        draft = normalized;
    }

    public void UpdateDraft(ChapterToolSettings settings) => draft = ChapterToolSettings.Normalize(settings);

    public void Commit(ChapterToolSettings settings)
    {
        var normalized = ChapterToolSettings.Normalize(settings);
        saved = normalized;
        draft = normalized;
        LoadFailed = false;
    }

    public void CaptureDraftAsSaved() => saved = draft;

    public void SetLoadFailed(bool value) => LoadFailed = value;

    public void EnableLiveApply() => LiveApplyEnabled = true;

    public void ResetDraft() => draft = ChapterToolSettings.Normalize(ChapterToolSettings.Default);

    public void DiscardDraft() => draft = saved;

    public void BeginSnapshot() => IsApplyingSnapshot = true;

    public void EndSnapshot() => IsApplyingSnapshot = false;
}
