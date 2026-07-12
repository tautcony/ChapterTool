using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.WasmDemo.Services;

/// <summary>
/// Browser-side workspace that mirrors Avalonia main-window load / grid / frames / expression / save flow.
/// </summary>
public sealed class DemoWorkspace
{
    private const decimal FrameAccuracyTolerance = 0.15m;

    private readonly ChapterDemoService demoService;
    private readonly FrameRateService frameRateService = new();
    private readonly ChapterOutputProjectionService projectionService = new();

    private ChapterImportResult? importResult;
    private ChapterSet? baseChapterSet;
    private List<ChapterRowModel> rows = [];
    private int selectedFrameRateIndex;

    public DemoWorkspace(ChapterDemoService demoService)
    {
        this.demoService = demoService;
        SaveFormatIndex = 0;
        ChapterNameModeIndex = 0;
        XmlLanguage = demoService.XmlLanguages.Contains("eng", StringComparer.OrdinalIgnoreCase)
            ? "eng"
            : demoService.XmlLanguages.FirstOrDefault() ?? "und";
        Expression = "t";
        RoundFrames = true;
        selectedFrameRateIndex = 0;
        StatusText = "Ready — load a chapter file.";
    }

    public string SourcePath { get; private set; } = string.Empty;

    public string StatusText { get; private set; }

    public double Progress { get; private set; }

    public bool IsBusy { get; private set; }

    public bool CanSave => baseChapterSet is not null && rows.Count > 0 && !IsBusy;

    public bool IsChapterGridEmpty => rows.Count == 0;

    public IReadOnlyList<ChapterRowModel> Rows => rows;

    public IReadOnlyList<ClipOption> ClipOptions { get; private set; } = [];

    public string? SelectedClipId { get; private set; }

    public bool IsClipSelectionVisible => ClipOptions.Count > 1;

    public IReadOnlyList<SaveFormatOption> SaveFormats => demoService.SaveFormats;

    public IReadOnlyList<string> ChapterNameModes => demoService.ChapterNameModes;

    public IReadOnlyList<string> XmlLanguages => demoService.XmlLanguages;

    public IReadOnlyList<FrameRateChoice> FrameRateChoices { get; private set; } = [];

    public int SaveFormatIndex { get; set; }

    public int ChapterNameModeIndex { get; set; }

    public int OrderShift { get; set; }

    public string XmlLanguage { get; set; }

    public bool ApplyExpression { get; set; }

    public string Expression { get; set; }

    public bool RoundFrames { get; set; }

    public int SelectedFrameRateIndex
    {
        get => selectedFrameRateIndex;
        set
        {
            var clamped = Math.Clamp(value, 0, Math.Max(0, FrameRateChoices.Count - 1));
            if (selectedFrameRateIndex == clamped)
            {
                return;
            }

            selectedFrameRateIndex = clamped;
            RefreshDisplay(updateStatus: true, statusMessage: null);
        }
    }

    public double FramesPerSecond { get; private set; }

    public string FramesPerSecondDisplay =>
        FramesPerSecond > 0
            ? FramesPerSecond.ToString("0.######")
            : "—";

    public bool IsXmlLanguageEnabled =>
        demoService.FormatAt(SaveFormatIndex) == ChapterExportFormat.Xml;

    public IReadOnlyList<DiagnosticView> Diagnostics { get; private set; } = [];

    public event Action? Changed;

    public async Task LoadAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        BeginBusy("Loading…");
        try
        {
            Progress = 0.2;
            Notify();

            var result = await demoService.ImportAsync(fileName, content, cancellationToken);
            Progress = 0.8;
            Notify();

            if (!result.Success || result.Groups.Count == 0)
            {
                ClearSession();
                Diagnostics = ToDiagnostics(result.Diagnostics);
                StatusText = FirstError(result.Diagnostics) ?? "Load failed.";
                return;
            }

            importResult = result;
            SourcePath = fileName;
            ClipOptions = BuildClipOptions(result);
            SelectedClipId = ClipOptions.FirstOrDefault()?.Id;
            selectedFrameRateIndex = 0;
            LoadBaseFromSelectedClip();
            RefreshDisplay(
                updateStatus: true,
                statusMessage: $"Loaded {baseChapterSet?.Chapters.Count ?? 0} chapter(s) from {Path.GetFileName(fileName)}.");
            Progress = 1;
        }
        catch (Exception ex)
        {
            ClearSession();
            StatusText = ex.Message;
            Diagnostics = [];
        }
        finally
        {
            EndBusy();
        }
    }

    public void SelectClip(string? clipId)
    {
        if (string.Equals(SelectedClipId, clipId, StringComparison.Ordinal))
        {
            return;
        }

        SelectedClipId = clipId;
        LoadBaseFromSelectedClip();
        RefreshDisplay(
            updateStatus: true,
            statusMessage: $"Selected clip: {ClipOptions.FirstOrDefault(c => c.Id == clipId)?.DisplayText ?? clipId}");
    }

    /// <summary>
    /// Applies current option state (round frames, expression, order shift, naming) and refreshes the grid.
    /// </summary>
    public void ApplyOptionsAndRefresh()
    {
        if (baseChapterSet is null)
        {
            Notify();
            return;
        }

        RefreshDisplay(updateStatus: false, statusMessage: null);
    }

    public SaveResult Save()
    {
        if (!CanSave || baseChapterSet is null)
        {
            return new SaveResult(false, "Nothing to save.");
        }

        try
        {
            // Ensure frames/FPS on the base set are current before export projection.
            var framed = ApplyFrames(baseChapterSet);
            baseChapterSet = framed.Info;
            FramesPerSecond = baseChapterSet.FramesPerSecond;

            var format = demoService.FormatAt(SaveFormatIndex);
            var options = CreateExportOptions();
            var export = demoService.Export(baseChapterSet, options);
            Diagnostics = ToDiagnostics(export.Diagnostics);
            if (!export.Success)
            {
                StatusText = FirstError(export.Diagnostics) ?? "Save failed.";
                Notify();
                return new SaveResult(false, StatusText);
            }

            var baseName = Path.GetFileNameWithoutExtension(SourcePath);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "chapters";
            }

            var fileName = baseName + (export.FileExtension.StartsWith('.')
                ? export.FileExtension
                : demoService.FormatExtension(format));
            StatusText = $"Saved {demoService.FormatDisplayName(format)} → {fileName}";
            Notify();
            return new SaveResult(true, StatusText, export.Content, fileName);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            Notify();
            return new SaveResult(false, ex.Message);
        }
    }

    public void UpdateRow(int index, string? timeText, string? name)
    {
        if (baseChapterSet is null || index < 0 || index >= baseChapterSet.Chapters.Count)
        {
            return;
        }

        var chapters = baseChapterSet.Chapters.ToList();
        var chapter = chapters[index];
        if (chapter.IsSeparator)
        {
            if (name is not null)
            {
                chapters[index] = chapter with { Name = name };
                baseChapterSet = baseChapterSet with { Chapters = chapters };
                RefreshDisplay(updateStatus: false, statusMessage: null);
            }

            return;
        }

        if (timeText is not null)
        {
            var start = demoService.TimeFormatter.ParseOrZero(timeText);
            chapter = chapter with { StartTime = start };
        }

        if (name is not null)
        {
            chapter = chapter with { Name = name };
        }

        chapters[index] = chapter;
        baseChapterSet = baseChapterSet with { Chapters = chapters };
        RefreshDisplay(updateStatus: false, statusMessage: null);
    }

    public Task LoadSampleAsync(CancellationToken cancellationToken = default)
    {
        var sample = """
            CHAPTER01=00:00:00.000
            CHAPTER01NAME=Opening
            CHAPTER02=00:01:23.456
            CHAPTER02NAME=Act 1
            CHAPTER03=00:12:34.567
            CHAPTER03NAME=Credits
            """u8.ToArray();
        return LoadAsync("sample.txt", sample, cancellationToken);
    }

    private void LoadBaseFromSelectedClip()
    {
        if (importResult is null || ClipOptions.Count == 0)
        {
            ClearSession(keepPath: true);
            return;
        }

        var clip = ClipOptions.FirstOrDefault(option => option.Id == SelectedClipId) ?? ClipOptions[0];
        SelectedClipId = clip.Id;
        baseChapterSet = importResult.Groups[clip.GroupIndex].Entries[clip.EntryIndex].ChapterSet;
        RebuildFrameRateChoices(baseChapterSet);
    }

    private void RefreshDisplay(bool updateStatus, string? statusMessage)
    {
        if (baseChapterSet is null)
        {
            rows = [];
            FramesPerSecond = 0;
            if (updateStatus && statusMessage is not null)
            {
                StatusText = statusMessage;
            }

            Notify();
            return;
        }

        var framed = ApplyFrames(baseChapterSet);
        baseChapterSet = framed.Info;
        FramesPerSecond = baseChapterSet.FramesPerSecond;
        RebuildFrameRateChoices(baseChapterSet);

        var projection = projectionService.Project(baseChapterSet, CreateExportOptions());
        rows = projection.Info.Chapters
            .Select(chapter => ToRow(chapter, demoService.TimeFormatter))
            .ToList();

        var projectionDiagnostics = ToDiagnostics(projection.Diagnostics);
        Diagnostics = projectionDiagnostics;

        if (updateStatus && statusMessage is not null)
        {
            StatusText = statusMessage;
        }
        else if (projectionDiagnostics.Count > 0)
        {
            var first = projectionDiagnostics[0];
            StatusText = $"{first.Severity}: {first.Message}";
        }
        else if (ApplyExpression)
        {
            StatusText = $"Expression applied · {FramesPerSecondDisplay} fps · {rows.Count} row(s)";
        }
        else if (updateStatus)
        {
            StatusText = $"Frames updated · {FramesPerSecondDisplay} fps · {rows.Count} row(s)";
        }

        Notify();
    }

    private FrameInfoResult ApplyFrames(ChapterSet info)
    {
        var option = ResolveSelectedFrameRateOption();
        // Auto (LegacyMplsCode == 0): detect when rounding, otherwise still need a valid option for fps.
        return frameRateService.UpdateFrames(info, option, RoundFrames, FrameAccuracyTolerance);
    }

    private FrameRateOption ResolveSelectedFrameRateOption()
    {
        var options = frameRateService.Options;
        if (selectedFrameRateIndex <= 0 || selectedFrameRateIndex >= options.Count)
        {
            return options[0]; // Auto
        }

        return options[selectedFrameRateIndex];
    }

    private void RebuildFrameRateChoices(ChapterSet info)
    {
        var options = frameRateService.Options;
        var choices = new List<FrameRateChoice>(options.Count);
        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            if (i == 0)
            {
                choices.Add(new FrameRateChoice(i, "Auto", option));
                continue;
            }

            if (!option.IsValid && option.LegacyMplsCode == 5)
            {
                // Skip reserved placeholder (matches Avalonia combo useful entries).
                continue;
            }

            choices.Add(new FrameRateChoice(i, option.DisplayName, option));
        }

        FrameRateChoices = choices;

        // Keep selected index on a still-valid option.
        if (choices.All(choice => choice.Index != selectedFrameRateIndex))
        {
            selectedFrameRateIndex = 0;
        }

        // Prefer matching detected/source fps when Auto is not forced and source has fps.
        _ = info;
    }

    private ChapterExportOptions CreateExportOptions() =>
        new(
            Format: demoService.FormatAt(SaveFormatIndex),
            XmlLanguage: XmlLanguage,
            SourceFileName: SourcePath,
            AutoGenerateNames: ChapterNameModeIndex == 1,
            OrderShift: OrderShift,
            ApplyExpression: ApplyExpression,
            Expression: string.IsNullOrWhiteSpace(Expression) ? "t" : Expression.Trim(),
            ProjectOutput: true);

    private void ClearSession(bool keepPath = false)
    {
        importResult = null;
        baseChapterSet = null;
        rows = [];
        ClipOptions = [];
        SelectedClipId = null;
        FramesPerSecond = 0;
        FrameRateChoices = [];
        if (!keepPath)
        {
            SourcePath = string.Empty;
        }
    }

    private static List<ClipOption> BuildClipOptions(ChapterImportResult result)
    {
        var options = new List<ClipOption>();
        for (var groupIndex = 0; groupIndex < result.Groups.Count; groupIndex++)
        {
            var group = result.Groups[groupIndex];
            for (var entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
            {
                var entry = group.Entries[entryIndex];
                var id = $"{groupIndex}:{entryIndex}:{entry.Id}";
                var display = string.IsNullOrWhiteSpace(entry.DisplayName)
                    ? $"Entry {entryIndex + 1}"
                    : entry.DisplayName;
                if (result.Groups.Count > 1)
                {
                    display = $"{Path.GetFileName(group.SourcePath)} · {display}";
                }

                options.Add(new ClipOption(id, display, groupIndex, entryIndex));
            }
        }

        return options;
    }

    private static ChapterRowModel ToRow(Chapter chapter, IChapterTimeFormatter formatter) =>
        new()
        {
            Number = chapter.DisplayNumber,
            TimeText = chapter.IsSeparator ? string.Empty : formatter.Format(chapter.StartTime),
            Name = chapter.Name,
            FramesInfo = chapter.FramesInfo,
            IsSeparator = chapter.IsSeparator,
            IsFrameAccurate = chapter.FrameAccuracy == FrameAccuracy.Accurate,
            IsFrameInexact = chapter.FrameAccuracy == FrameAccuracy.Inexact
        };

    private static IReadOnlyList<DiagnosticView> ToDiagnostics(IEnumerable<ChapterDiagnostic> diagnostics) =>
        diagnostics.Select(static diagnostic => new DiagnosticView(
            diagnostic.Severity.ToString(),
            diagnostic.DisplayCode,
            diagnostic.Message,
            diagnostic.Details)).ToArray();

    private static string? FirstError(IEnumerable<ChapterDiagnostic> diagnostics) =>
        diagnostics.FirstOrDefault(static d => d.Severity == DiagnosticSeverity.Error)?.Message
        ?? diagnostics.FirstOrDefault()?.Message;

    private void BeginBusy(string status)
    {
        IsBusy = true;
        Progress = 0;
        StatusText = status;
        Notify();
    }

    private void EndBusy()
    {
        IsBusy = false;
        if (Progress >= 1)
        {
            Progress = 0;
        }

        Notify();
    }

    private void Notify() => Changed?.Invoke();
}

public sealed record FrameRateChoice(int Index, string DisplayName, FrameRateOption Option);

public sealed record DiagnosticView(
    string Severity,
    string Code,
    string Message,
    string? Details);
