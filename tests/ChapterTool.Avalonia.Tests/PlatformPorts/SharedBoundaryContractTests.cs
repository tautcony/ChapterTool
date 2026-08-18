using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.Tests.PlatformPorts;

public sealed class SharedBoundaryContractTests
{
    [Fact]
    public void RuntimeCapabilitiesExposeSemanticHostModes()
    {
        IRuntimeCapabilities desktop = new RuntimeCapabilities(
            RuntimeSourceMode.LocalPath,
            RuntimeOutputMode.Directory,
            RuntimeSecondarySurfaceMode.NativeWindow,
            CanReadClipboard: true,
            CanWriteClipboard: true,
            CanConfigureExternalTools: true,
            CanRunExternalProcesses: true,
            CanOpenLocalPaths: true);

        Assert.Equal(RuntimeSourceMode.LocalPath, desktop.SourceMode);
        Assert.Equal(RuntimeOutputMode.Directory, desktop.OutputMode);
        Assert.True(desktop.CanRunExternalProcesses);
    }

    [Fact]
    public void BrowserCapabilitiesDisableDesktopActions()
    {
        IRuntimeCapabilities browser = new RuntimeCapabilities(
            RuntimeSourceMode.BufferedPortable,
            RuntimeOutputMode.BrowserDownload,
            RuntimeSecondarySurfaceMode.InView,
            CanReadClipboard: false,
            CanWriteClipboard: false,
            CanConfigureExternalTools: false,
            CanRunExternalProcesses: false,
            CanOpenLocalPaths: false);

        Assert.Equal(RuntimeSourceMode.BufferedPortable, browser.SourceMode);
        Assert.Equal(RuntimeOutputMode.BrowserDownload, browser.OutputMode);
        Assert.False(browser.CanConfigureExternalTools);
        Assert.False(browser.CanRunExternalProcesses);
        Assert.False(browser.CanOpenLocalPaths);
    }

    [Fact]
    public void BufferedSourceRejectsNoContentAtContractBoundary()
    {
        var source = new ChapterSourceReadResult(null, [new ChapterDiagnostic(
            DiagnosticSeverity.Error,
            ChapterDiagnosticCode.UnsupportedInput,
            "The source is empty.")]);

        Assert.False(source.Success);
        Assert.Null(source.Source);
        Assert.Single(source.Diagnostics);
    }

    [Fact]
    public async Task BrowserSourcePolicyRejectsOversizedAndEmptyFilesBeforeRead()
    {
        var access = new FakeBrowserFileAccess
        {
            Picked = new BrowserFileHandle("large.txt", 101, new object())
        };
        var picker = new BrowserChapterSourcePicker(access, maxBytes: 100);

        var large = await picker.ReadSourceAsync(
            () => access.PickAsync(CancellationToken.None),
            CancellationToken.None);

        Assert.False(large.Success);
        Assert.Equal(ChapterDiagnosticCode.InputTooLarge, large.Diagnostics.Single().Code);
        Assert.Equal(0, access.ReadCount);

        access.Picked = new BrowserFileHandle("empty.txt", 0, new object());
        var empty = await picker.ReadSourceAsync(
            () => access.PickAsync(CancellationToken.None),
            CancellationToken.None);

        Assert.False(empty.Success);
        Assert.Equal(ChapterDiagnosticCode.InputEmpty, empty.Diagnostics.Single().Code);
        Assert.Equal(0, access.ReadCount);
    }

    [Fact]
    public async Task BrowserSourcePolicyReturnsBufferedSourceAndReportsBlockedRead()
    {
        var access = new FakeBrowserFileAccess
        {
            Picked = new BrowserFileHandle("chapters.txt", 3, new object()),
            Bytes = [1, 2, 3]
        };
        var picker = new BrowserChapterSourcePicker(access);

        var result = await picker.ReadSourceAsync(
            () => access.PickAsync(CancellationToken.None),
            CancellationToken.None);

        var source = Assert.IsType<BufferedChapterSource>(result.Source);
        Assert.Equal("chapters.txt", source.DisplayName);
        Assert.Equal(1, access.ReadCount);

        access.ReadException = new UnauthorizedAccessException("denied");
        var blocked = await picker.ReadSourceAsync(
            () => access.PickAsync(CancellationToken.None),
            CancellationToken.None);

        Assert.False(blocked.Success);
        Assert.Equal(ChapterDiagnosticCode.InputReadFailed, blocked.Diagnostics.Single().Code);
    }

    [Fact]
    public void BrowserSettingsCodecPreservesVersionedCamelCaseShape()
    {
        var settings = new ChapterToolSettings
        {
            Application = new AppSettings(SavingPath: "/desktop-only", Language: "zh-CN"),
            Theme = new ThemeSettings("solarized-dark"),
            Font = new FontSettings("system-ui", "ui-monospace")
        };

        var json = BrowserSettingsCodec.Serialize(settings);

        Assert.Contains("\"schemaVersion\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"application\"", json, StringComparison.Ordinal);
        Assert.Contains("\"theme\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("desktop-only", json, StringComparison.Ordinal);
        Assert.True(BrowserSettingsCodec.TryDeserialize(json, out var loaded));
        Assert.Equal("zh-CN", loaded.Application.Language);
        Assert.Equal("solarized-dark", loaded.Theme.PresetId);
    }

    private sealed class FakeBrowserFileAccess : IBrowserFileAccess
    {
        public BrowserFileHandle? Picked { get; set; }

        public byte[] Bytes { get; set; } = [];

        public Exception? ReadException { get; set; }

        public int ReadCount { get; private set; }

        public ValueTask<BrowserFileHandle?> PickAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Picked);

        public ValueTask<BrowserFileHandle?> FromDropAsync(object drop, CancellationToken cancellationToken) => ValueTask.FromResult(Picked);

        public ValueTask<byte[]> ReadAsync(BrowserFileHandle file, CancellationToken cancellationToken)
        {
            ReadCount++;
            return ReadException is null
                ? ValueTask.FromResult(Bytes)
                : ValueTask.FromException<byte[]>(ReadException);
        }
    }

}
