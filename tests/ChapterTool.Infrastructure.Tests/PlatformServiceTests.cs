using ChapterTool.Core.Services;
using ChapterTool.Infrastructure.Platform;

namespace ChapterTool.Infrastructure.Tests;

public sealed class PlatformServiceTests
{
    [Fact]
    public async Task Non_windows_file_association_reports_unsupported()
    {
        var service = new UnsupportedFileAssociationService();

        var result = await service.RegisterAsync(
            ".mpls",
            "ChapterTool.MPLS",
            "ChapterTool",
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("UnsupportedPlatform", result.Diagnostics.Single().Code);
    }

    [Fact]
    public async Task Non_windows_file_association_unregister_reports_unsupported()
    {
        var service = new UnsupportedFileAssociationService();

        var result = await service.UnregisterAsync(
            ".mpls",
            "ChapterTool.MPLS",
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("UnsupportedPlatform", result.Diagnostics.Single().Code);
    }

    [Fact]
    public async Task Non_windows_file_association_is_registered_reports_unsupported()
    {
        var service = new UnsupportedFileAssociationService();

        var result = await service.IsRegisteredAsync(
            ".mpls",
            "ChapterTool.MPLS",
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("UnsupportedPlatform", result.Diagnostics.Single().Code);
    }

    [Fact]
    public async Task Native_dependency_service_reports_missing_dependency()
    {
        var service = new FileSystemNativeDependencyService([]);

        var result = await service.ResolveAsync("missing-tool", TestContext.Current.CancellationToken);

        Assert.False(result.Found);
        Assert.Equal("NativeLibraryMissing", result.DiagnosticCode);
    }

    [Fact]
    public async Task Memory_clipboard_dialog_localization_and_window_services_are_testable_skeletons()
    {
        var clipboard = new MemoryClipboardService();
        await clipboard.SetTextAsync("copied", TestContext.Current.CancellationToken);
        Assert.Equal("copied", await clipboard.GetTextAsync(TestContext.Current.CancellationToken));

        var dialogs = new ScriptedDialogService(new DialogResult(true, "accepted"));
        var dialogResult = await dialogs.ShowMessageAsync(
            new DialogRequest("title", "message", DialogKind.Confirmation),
            TestContext.Current.CancellationToken);
        Assert.True(dialogResult.Accepted);
        Assert.Equal("accepted", dialogResult.Text);

        var windows = new RecordingWindowService();
        await windows.ShowAsync("preview", "text", TestContext.Current.CancellationToken);
        await windows.HideAsync("preview", TestContext.Current.CancellationToken);
        Assert.Equal(["show:preview", "hide:preview"], windows.Calls);
    }
}
