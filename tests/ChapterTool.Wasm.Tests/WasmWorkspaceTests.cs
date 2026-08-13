using System.Runtime.Versioning;
using ChapterTool.Core.Models;
using ChapterTool.Wasm.Services;

namespace ChapterTool.Wasm.Tests;

[SupportedOSPlatform("browser")]
public sealed class WasmWorkspaceTests
{
    [Fact]
    public async Task LoadAndReloadRestoresLastSuccessfulSource()
    {
        var workspace = CreateWorkspace();
        var first = """
                    CHAPTER01=00:00:00.000
                    CHAPTER01NAME=Opening
                    CHAPTER02=00:01:00.000
                    CHAPTER02NAME=Middle
                    """u8.ToArray();
        await workspace.LoadAsync("first.txt", first);
        Assert.Equal(2, workspace.Rows.Count);
        Assert.True(workspace.CanReload);

        workspace.UpdateRow(0, null, "Edited");
        Assert.Equal("Edited", workspace.Rows[0].Name);

        await workspace.ReloadAsync();
        Assert.Equal("Opening", workspace.Rows[0].Name);
        Assert.Equal("first.txt", workspace.SourcePath);
    }

    [Fact]
    public async Task SelectAndCombineMplsClipsUsesSharedSessionTransitions()
    {
        var workspace = CreateWorkspace();
        var path = LocateFixture("Importing", "Disc", "Mpls", "00001_Hidan_no_Aria_AA.mpls");
        await workspace.LoadAsync(path, await File.ReadAllBytesAsync(path));

        Assert.Equal(2, workspace.ClipOptions.Count);
        Assert.False(workspace.IsClipCombined);
        var secondClip = workspace.ClipOptions[1];

        workspace.SelectClip(secondClip.Id);

        Assert.Equal(secondClip.Id, workspace.SelectedClipId);
        Assert.Contains(secondClip.DisplayText, workspace.StatusText, StringComparison.Ordinal);
        Assert.Equal(6, workspace.Rows.Count);

        workspace.ToggleClipCombine();

        Assert.True(workspace.IsClipCombined);
        Assert.Single(workspace.ClipOptions);
        Assert.Equal(12, workspace.Rows.Count);
    }

    [Fact]
    public async Task SingleMplsClipKeepsClipSelectorVisible()
    {
        var workspace = CreateWorkspace();
        var path = LocateFixture("Importing", "Disc", "Mpls", "00000_HEVC.mpls");
        await workspace.LoadAsync(path, await File.ReadAllBytesAsync(path));

        Assert.Single(workspace.ClipOptions);
        Assert.True(workspace.IsClipSelectionVisible);
        Assert.Contains(".m2ts", workspace.ClipOptions[0].DisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AppendMplsRejectsNonMplsWithoutClearingSession()
    {
        var workspace = CreateWorkspace();
        await workspace.LoadAsync("sample.txt", [
            .. """
               CHAPTER01=00:00:00.000
               CHAPTER01NAME=Opening
               CHAPTER02=00:01:00.000
               CHAPTER02NAME=Middle
               """u8
        ]);
        Assert.False(workspace.CanAppendMpls);
        var beforeCount = workspace.Rows.Count;
        await workspace.AppendMplsAsync("not-mpls.txt", [.. "CHAPTER01=00:00:00.000\nCHAPTER01NAME=X\n"u8]);
        Assert.Equal(beforeCount, workspace.Rows.Count);
        Assert.False(string.IsNullOrWhiteSpace(workspace.SourcePath));
    }

    [Fact]
    public async Task AppendMplsMergesPlaylistGroups()
    {
        var workspace = CreateWorkspace();
        var firstPath = LocateFixture("Importing", "Disc", "Mpls", "00011_24_Eva.mpls");
        var secondPath = LocateFixture("Importing", "Disc", "Mpls", "00020_Terminator2.mpls");
        await workspace.LoadAsync(firstPath, await File.ReadAllBytesAsync(firstPath));
        var firstClips = workspace.ClipOptions.Count;
        var firstRows = workspace.Rows.Count;
        Assert.True(workspace.CanAppendMpls);

        await workspace.AppendMplsAsync(secondPath, await File.ReadAllBytesAsync(secondPath));

        Assert.True(workspace.ClipOptions.Count >= firstClips);
        Assert.True(workspace.Rows.Count >= firstRows);
    }

    [Fact]
    public async Task TemplateModeProjectsNamesThroughExportOptions()
    {
        var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();
        Assert.True(workspace.SetChapterNameTemplate("names.txt", "Alpha\nBeta\nGamma"));
        Assert.Equal(2, workspace.ChapterNameModeIndex);
        Assert.Equal("names.txt", workspace.ChapterNameTemplateStatus);
        Assert.Equal("Alpha", workspace.Rows[0].Name);
        Assert.Equal("Beta", workspace.Rows[1].Name);
        Assert.Equal("Gamma", workspace.Rows[2].Name);

        var previous = workspace.ChapterNameTemplateText;
        Assert.False(workspace.SetChapterNameTemplate("empty.txt", "   "));
        Assert.Equal(previous, workspace.ChapterNameTemplateText);
    }

    [Fact]
    public async Task MultiSelectDeleteAndZonesOperateOnSelection()
    {
        var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();
        workspace.SelectRow(0);
        workspace.SelectRow(2, ctrl: true);
        Assert.Equal(2, workspace.SelectedRowIndexes.Count);
        Assert.True(workspace.IsRowSelected(0));
        Assert.True(workspace.IsRowSelected(2));

        workspace.SelectedFrameRateIndex = 1;
        workspace.ApplyOptionsAndRefresh();
        Assert.True(workspace.FramesPerSecond > 0);
        var zones = workspace.CreateZonesForSelection();
        Assert.StartsWith("--zones ", zones, StringComparison.Ordinal);
        Assert.Contains(",", zones, StringComparison.Ordinal);

        workspace.DeleteSelectedRows();
        Assert.Single(workspace.Rows);
        Assert.Equal("Act 1", workspace.Rows[0].Name);
    }

    [Fact]
    public async Task ShiftFramesForwardMovesChapterTimes()
    {
        var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();

        // Force a known FPS via fixed frame rate option when present.
        if (workspace.FrameRateChoices.Count > 1)
        {
            workspace.SelectedFrameRateIndex = workspace.FrameRateChoices.First(choice => choice is { Index: > 0, Option.IsValid: true }).Index;
        }

        var before = workspace.Rows[1].TimeText;
        workspace.ShiftFramesForward(1);
        var after = workspace.Rows[1].TimeText;
        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task PreviewUsesSameExportPathAsSave()
    {
        var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();
        var preview = workspace.Preview();
        var save = workspace.Save();
        Assert.True(preview.Success);
        Assert.True(save.Success);
        Assert.Equal(save.Content, preview.Content);
        Assert.Equal(save.FileName, preview.FileName);
    }

    [Fact]
    public async Task AutoGenerateNamesModeRewritesDisplayedNames()
    {
        var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();
        workspace.ChapterNameModeIndex = 1;
        workspace.ApplyOptionsAndRefresh();
        Assert.All(workspace.Rows, row => Assert.StartsWith("Chapter ", row.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void LocalizerCoversRequiredCultures()
    {
        var localizer = new WasmLocalizer();
        foreach (var culture in new[] { "en-US", "zh-CN", "ja-JP" })
        {
            localizer.SetCulture(culture);
            Assert.False(string.IsNullOrWhiteSpace(localizer.T("Action.Load")));
            Assert.Equal(3, localizer.ChapterNameModes.Count);
        }
    }

    [Fact]
    public async Task ChangingCultureRefreshesLocalizedWorkspaceStatus()
    {
        var localizer = new WasmLocalizer();
        using var workspace = new WasmWorkspace(new WasmChapterService(), localizer);

        await workspace.LoadSampleAsync();
        workspace.SelectRow(0);
        Assert.Contains("Selected", workspace.StatusText, StringComparison.Ordinal);

        localizer.SetCulture("zh-CN");

        Assert.Contains("选择", workspace.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsLoadsAboveMaxByteLimitAndKeepsEmptySession()
    {
        // Use a tiny limit so the test does not allocate a 64 MiB buffer.
        var workspace = new WasmWorkspace(new WasmChapterService(), maxLoadBytes: 8);
        await workspace.LoadAsync("huge.bin", [.. "0123456789"u8]);

        Assert.Empty(workspace.Rows);
        Assert.False(workspace.CanSave);
        Assert.Contains("64", workspace.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsAppendsAboveMaxByteLimitBeforeImport()
    {
        var workspace = new WasmWorkspace(new WasmChapterService(), maxLoadBytes: 512);
        var sourcePath = LocateFixture("Importing", "Disc", "Mpls", "00000_HEVC.mpls");
        await workspace.LoadAsync(sourcePath, await File.ReadAllBytesAsync(sourcePath));
        Assert.True(workspace.CanAppendMpls);
        var before = workspace.Rows.Select(row => row.Name).ToArray();

        await workspace.AppendMplsAsync("huge.mpls", new byte[513]);

        Assert.Equal(before, workspace.Rows.Select(row => row.Name).ToArray());
        Assert.Contains("64", workspace.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpressionModeProjectsTimesThroughPreview()
    {
        var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();
        workspace.ApplyExpression = true;
        workspace.Expression = "t + 1";
        workspace.ApplyOptionsAndRefresh();

        var preview = workspace.Preview();
        Assert.True(preview.Success);
        Assert.Contains("00:00:01", preview.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpressionPresetAppliesCoreEngineScriptAndProjectsRows()
    {
        var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();

        Assert.NotEmpty(workspace.ExpressionPresets);
        var offset = Assert.Single(workspace.ExpressionPresets, preset => preset.Id == "offset-seconds");
        Assert.True(workspace.ApplyExpressionPreset(offset.Id));
        Assert.True(workspace.ApplyExpression);
        Assert.Equal(offset.Id, workspace.ExpressionPresetId);
        Assert.Equal(offset.ScriptText, workspace.Expression);

        var preview = workspace.Preview();
        Assert.True(preview.Success);

        // offset-seconds default adds 1 second to t=0
        Assert.Contains("00:00:01", preview.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidExpressionSurfacesCoreDiagnosticToStatusAndDiagnostics()
    {
        var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();
        workspace.ApplyExpression = true;
        workspace.Expression = "return bad()";
        workspace.ApplyOptionsAndRefresh();

        Assert.True(workspace.HasDiagnostics);
        Assert.Contains(workspace.Diagnostics, diagnostic =>
            diagnostic.Code.Contains("Expression", StringComparison.OrdinalIgnoreCase)
            || diagnostic.Code.Contains("Lua", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrWhiteSpace(workspace.StatusText));
        Assert.Contains(workspace.Diagnostics[0].Message, workspace.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WasmChapterServiceRoutesXplContentThroughSharedImportPath()
    {
        var service = new WasmChapterService();
        var path = LocateFixture("Importing", "Disc", "Xpl", "VPLST001.XPL");
        var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        var result = await service.ImportAsync("VPLST001.XPL", bytes, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(ChapterImportFormat.HdDvdXpl, result.Groups.Single().Entries.Single().ChapterSet.ImportFormat);

        var workspace = CreateWorkspace();
        await workspace.LoadAsync("VPLST001.XPL", bytes);
        Assert.True(workspace.CanSave);
        Assert.NotEmpty(workspace.Rows);
    }

    [Fact]
    public async Task WasmChapterServiceRoutesFlacEmbeddedCueThroughSharedImportPath()
    {
        var service = new WasmChapterService();
        var cue = """
                  TITLE "Album"
                  FILE "audio.flac" WAVE
                    TRACK 01 AUDIO
                      TITLE "Track 1"
                      INDEX 01 00:00:00
                  """;
        var content = CreateFlacWithVorbisCue(cue);

        var result = await service.ImportAsync("music.flac", content, TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Equal("Track 1", result.Groups.Single().Entries.Single().ChapterSet.Chapters.Single().Name);

        var workspace = CreateWorkspace();
        await workspace.LoadAsync("music.flac", content);
        Assert.Single(workspace.Rows);
        Assert.Equal("Track 1", workspace.Rows[0].Name);
    }

    [Fact]
    public async Task InsertAndDuplicateAffectRowCount()
    {
        var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();
        var before = workspace.Rows.Count;

        workspace.InsertBefore(0);
        Assert.Equal(before + 1, workspace.Rows.Count);

        workspace.DuplicateRow(0);
        Assert.Equal(before + 2, workspace.Rows.Count);
    }

    [Fact]
    public async Task DeleteSelectedWithoutSelectionDoesNotChangeRows()
    {
        var workspace = CreateWorkspace();
        await workspace.LoadSampleAsync();
        var before = workspace.Rows.Count;
        Assert.False(workspace.HasRowSelection);
        workspace.DeleteSelectedRows();
        Assert.Equal(before, workspace.Rows.Count);
    }

    [Fact]
    public void LocalizerTablesShareTheSameEnglishKeySetAcrossCultures()
    {
        var localizer = new WasmLocalizer();
        foreach (var culture in new[] { "en-US", "zh-CN", "ja-JP" })
        {
            localizer.SetCulture(culture);
            foreach (var key in WasmLocalizer.EnglishKeys)
            {
                var value = localizer.T(key);
                Assert.False(string.IsNullOrWhiteSpace(value), $"Missing/blank translation for {key} in {culture}");
                Assert.NotEqual(key, value);
            }
        }
    }

    private static WasmWorkspace CreateWorkspace() => new(new WasmChapterService());

    private static string LocateFixture(params string[] segments) =>
        ChapterTool.TestSupport.TestRepository.CoreFixture(segments);

    private static byte[] CreateFlacWithVorbisCue(string cue)
    {
        using var stream = new MemoryStream();
        stream.Write("fLaC"u8);
        var comment = System.Text.Encoding.UTF8.GetBytes("cuesheet=" + cue);
        var vendor = "ChapterTool"u8.ToArray();
        using var body = new MemoryStream();
        WriteLe32(body, vendor.Length);
        body.Write(vendor);
        WriteLe32(body, 1);
        WriteLe32(body, comment.Length);
        body.Write(comment);
        var payload = body.ToArray();
        stream.WriteByte(0x84);
        stream.WriteByte((byte)((payload.Length >> 16) & 0xFF));
        stream.WriteByte((byte)((payload.Length >> 8) & 0xFF));
        stream.WriteByte((byte)(payload.Length & 0xFF));
        stream.Write(payload);
        return stream.ToArray();
    }

    private static void WriteLe32(Stream stream, int value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)((value >> 8) & 0xFF));
        stream.WriteByte((byte)((value >> 16) & 0xFF));
        stream.WriteByte((byte)((value >> 24) & 0xFF));
    }

}
