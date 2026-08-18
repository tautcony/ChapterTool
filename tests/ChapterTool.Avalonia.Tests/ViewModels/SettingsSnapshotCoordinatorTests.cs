using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Contracts.Configuration;

namespace ChapterTool.Avalonia.Tests.ViewModels;

public sealed class SettingsSnapshotCoordinatorTests
{
    [Fact]
    public void SavedAndDraftSnapshotsRemainDistinctUntilCommit()
    {
        var coordinator = new SettingsSnapshotCoordinator(ChapterToolSettings.Default);
        var draft = ChapterToolSettings.Default with
        {
            Application = ChapterToolSettings.Default.Application with { Language = "zh-CN" }
        };

        coordinator.UpdateDraft(draft);

        Assert.True(coordinator.HasUnsavedChanges);
        Assert.Equal("zh-CN", coordinator.Draft.Application.Language);
        Assert.NotEqual(coordinator.Draft, coordinator.Saved);

        coordinator.Commit(draft);

        Assert.False(coordinator.HasUnsavedChanges);
        Assert.Equal(coordinator.Saved, coordinator.Draft);
    }

    [Fact]
    public void LifecycleFlagsDescribeLoadAndSnapshotApplication()
    {
        var coordinator = new SettingsSnapshotCoordinator(ChapterToolSettings.Default);

        coordinator.BeginLoad();
        coordinator.SetLoadFailed(true);
        coordinator.EnableLiveApply();
        coordinator.BeginSnapshot();

        Assert.True(coordinator.LoadFailed);
        Assert.True(coordinator.LiveApplyEnabled);
        Assert.True(coordinator.IsApplyingSnapshot);

        coordinator.EndSnapshot();

        Assert.False(coordinator.IsApplyingSnapshot);
    }

    [Fact]
    public void DiscardAndResetChangeOnlyTheDraftSnapshot()
    {
        var saved = ChapterToolSettings.Default with
        {
            Application = ChapterToolSettings.Default.Application with { Language = "zh-CN" }
        };
        var coordinator = new SettingsSnapshotCoordinator(saved);
        coordinator.UpdateDraft(saved with
        {
            Application = saved.Application with { Language = "en-US" }
        });

        coordinator.DiscardDraft();

        Assert.Equal(coordinator.Saved, coordinator.Draft);

        coordinator.ResetDraft();

        Assert.Equal("zh-CN", coordinator.Saved.Application.Language);
        Assert.Equal(ChapterToolSettings.Default, coordinator.Draft);
        Assert.True(coordinator.HasUnsavedChanges);
    }
}
