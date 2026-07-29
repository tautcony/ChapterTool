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
    public void OutputDocumentRetainsEncodedPayloadAndDiagnostics()
    {
        var diagnostic = new ChapterDiagnostic(
            DiagnosticSeverity.Warning,
            ChapterDiagnosticCode.Saved,
            "saved");
        var document = new ChapterOutputDocument("chapters.txt", "text/plain", [0xEF, 0xBB, 0xBF], [diagnostic]);

        Assert.Equal("chapters.txt", document.FileName);
        Assert.Equal("text/plain", document.MediaType);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, document.Content);
        Assert.Same(diagnostic, document.Diagnostics[0]);
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
    public void OutputFactoryAppliesEncodingBomAndMediaType()
    {
        var info = new ChapterSet(
            "movie",
            "movie.txt",
            ChapterImportFormat.Ogm,
            24,
            TimeSpan.FromSeconds(1),
            [new Chapter(1, TimeSpan.Zero, "Intro")]);
        var options = new ChapterExportOptions(
            ChapterExportFormat.Xml,
            TextEncoding: OutputTextEncoding.Utf16LittleEndian,
            EmitBom: true);
        var export = new ChapterExportResult(true, "<Chapter />", ".xml", []);

        var document = ChapterOutputDocumentFactory.Create(info, options, export);

        Assert.Equal("movie.xml", document.FileName);
        Assert.Equal("application/xml", document.MediaType);
        Assert.Equal(new byte[] { 0xFF, 0xFE }, document.Content[..2]);
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

    [Fact]
    public async Task UnavailableExternalActionDoesNotExecute()
    {
        var action = new UnavailableExternalActionPort();

        Assert.False(action.IsAvailable);
        Assert.False(await action.ExecuteAsync("open", null, CancellationToken.None));
        Assert.False(action.Executed);
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

    private sealed class UnavailableExternalActionPort : IExternalActionPort
    {
        public bool IsAvailable => false;

        public bool Executed { get; private set; }

        public ValueTask<bool> ExecuteAsync(string actionId, object? parameter, CancellationToken cancellationToken)
        {
            if (!IsAvailable)
            {
                return ValueTask.FromResult(false);
            }

            Executed = true;
            return ValueTask.FromResult(false);
        }
    }
}
