using System.ComponentModel;
using System.Runtime.CompilerServices;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.UI.ViewModels;

public sealed class ChapterRowViewModel(
    Chapter chapter,
    IChapterTimeFormatter formatter,
    int? number = null,
    string? name = null) : INotifyPropertyChanged
{
    private ExpressionChapterComparison? previewComparison;
    private string previewTimeText = string.Empty;
    private string previewBeforeTimeText = string.Empty;
    private string previewTimeDelta = string.Empty;
    private string previewName = string.Empty;
    private string previewNumber = string.Empty;
    private string previewFrames = string.Empty;
    private string previewBeforeFrames = string.Empty;
    private string previewTimeAccessibleName = string.Empty;
    private string previewNameAccessibleName = string.Empty;
    private string previewNumberAccessibleName = string.Empty;
    private string previewFramesAccessibleName = string.Empty;
    private string previewFrameBaseline = string.Empty;
    private FrameAccuracy previewFrameBaselineAccuracy = FrameAccuracy.Neutral;
    private FrameAccuracy previewCandidateFrameAccuracy = FrameAccuracy.Neutral;
    private decimal previewBeforeFramesPerSecond;
    private bool isNarrowPreviewLayout;
    private FramePresentationInput? framePresentationInput;
    private FramePresentationInput? previewBeforeFramePresentationInput;
    private FramePresentationInput? previewCandidateFramePresentationInput;
    private FramePresentationPolicy framePresentationPolicy = new(false, true, 3);

    public Chapter Chapter { get; } = chapter;

    public int Number { get; } = number ?? chapter.DisplayNumber;

    public string TimeText { get; set; } = chapter.IsSeparator ? string.Empty : formatter.Format(chapter.StartTime);

    public string Name { get; set; } = name ?? chapter.Name;

    public string FramesInfo { get; set; } = chapter.FramesInfo;

    public FramePresentationParts? FramePresentation { get; private set; }

    public FramePresentationParts? PreviewBeforeFramePresentation { get; private set; }

    public FramePresentationParts? PreviewCandidateFramePresentation { get; private set; }

    public bool IsFrameAccurate { get; } = chapter.FrameAccuracy == FrameAccuracy.Accurate;

    public bool IsFrameInexact { get; } = chapter.FrameAccuracy == FrameAccuracy.Inexact;

    public bool IsFrameNeutral { get; } = chapter.FrameAccuracy == FrameAccuracy.Neutral;

    public ChapterTrackId? TrackId { get; internal set; }

    public ChapterId? ChapterId { get; internal set; }

    public ExpressionChapterComparison? PreviewComparison
    {
        get => previewComparison;
        internal set
        {
            if (previewComparison == value)
            {
                return;
            }

            previewComparison = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPreviewTimeChange));
            OnPropertyChanged(nameof(HasPreviewNameChange));
            OnPropertyChanged(nameof(HasPreviewNumberChange));
            OnPropertyChanged(nameof(HasPreviewFramesChange));
            OnPropertyChanged(nameof(IsPreviewingWide));
            OnPropertyChanged(nameof(IsPreviewingNarrow));
            NotifyCellTemplateStates();
        }
    }

    public bool HasPreviewTimeChange
    {
        get
        {
            if (((PreviewComparison?.Changes ?? ExpressionChapterChangeKind.None) & ExpressionChapterChangeKind.Time) == ExpressionChapterChangeKind.None
                || PreviewComparison?.Before is not { } before
                || PreviewComparison.Candidate is not { } candidate)
            {
                return false;
            }

            return formatter.Format(TimeSpan.FromTicks(before.StartTicks))
                != formatter.Format(TimeSpan.FromTicks(candidate.StartTicks));
        }
    }

    public bool HasPreviewNameChange => PreviewComparison is { Changes: var changes, Before: var before, Candidate: var candidate }
        && (changes & ExpressionChapterChangeKind.OtherProperties) != ExpressionChapterChangeKind.None
        && before?.Name != candidate?.Name;

    public bool HasPreviewNumberChange => PreviewComparison is { Changes: var changes, Before: var before, Candidate: var candidate }
        && (changes & ExpressionChapterChangeKind.OtherProperties) != ExpressionChapterChangeKind.None
        && before?.DisplayNumber != candidate?.DisplayNumber;

    public bool HasPreviewFramesChange =>
        ((PreviewComparison?.Changes ?? ExpressionChapterChangeKind.None) & ExpressionChapterChangeKind.FrameInformation) != ExpressionChapterChangeKind.None
        && PreviewComparison?.CandidateFrames is { } candidateFrames
        && !string.Equals(previewFrameBaseline, candidateFrames.Text, StringComparison.Ordinal);

    public string PreviewTimeText { get => previewTimeText; internal set => SetProperty(ref previewTimeText, value); }

    public string PreviewBeforeTimeText { get => previewBeforeTimeText; internal set => SetProperty(ref previewBeforeTimeText, value); }

    public string PreviewTimeDelta { get => previewTimeDelta; internal set => SetProperty(ref previewTimeDelta, value); }

    public string PreviewName { get => previewName; internal set => SetProperty(ref previewName, value); }

    public string PreviewNumber { get => previewNumber; internal set => SetProperty(ref previewNumber, value); }

    public string PreviewFrames { get => previewFrames; internal set => SetProperty(ref previewFrames, value); }

    public string PreviewBeforeFrames { get => previewBeforeFrames; internal set => SetProperty(ref previewBeforeFrames, value); }

    public bool IsPreviewBeforeFrameAccurate => previewFrameBaselineAccuracy == FrameAccuracy.Accurate;

    public bool IsPreviewBeforeFrameInexact => previewFrameBaselineAccuracy == FrameAccuracy.Inexact;

    public bool IsPreviewBeforeFrameNeutral => previewFrameBaselineAccuracy == FrameAccuracy.Neutral;

    public bool IsPreviewCandidateFrameAccurate => previewCandidateFrameAccuracy == FrameAccuracy.Accurate;

    public bool IsPreviewCandidateFrameInexact => previewCandidateFrameAccuracy == FrameAccuracy.Inexact;

    public bool IsPreviewCandidateFrameNeutral => previewCandidateFrameAccuracy == FrameAccuracy.Neutral;

    internal FrameAccuracy PreviewBeforeFrameAccuracy => previewFrameBaselineAccuracy;

    internal FrameAccuracy PreviewCandidateFrameAccuracy => previewCandidateFrameAccuracy;

    public string PreviewTimeAccessibleName { get => previewTimeAccessibleName; internal set => SetProperty(ref previewTimeAccessibleName, value); }

    public string PreviewNameAccessibleName { get => previewNameAccessibleName; internal set => SetProperty(ref previewNameAccessibleName, value); }

    public string PreviewNumberAccessibleName { get => previewNumberAccessibleName; internal set => SetProperty(ref previewNumberAccessibleName, value); }

    public string PreviewFramesAccessibleName { get => previewFramesAccessibleName; internal set => SetProperty(ref previewFramesAccessibleName, value); }

    internal decimal PreviewBeforeFramesPerSecond
    {
        get => previewBeforeFramesPerSecond;
        private set => previewBeforeFramesPerSecond = value;
    }

    internal string PreviewFrameBaseline => previewFrameBaseline;

    internal void UpdatePreviewComparison(
        ExpressionChapterComparison? comparison,
        decimal displayFrameRate,
        string missingFramesText)
    {
        if (previewComparison is null && comparison is not null)
        {
            previewFrameBaseline = string.IsNullOrEmpty(FramesInfo) ? missingFramesText : FramesInfo;
            previewFrameBaselineAccuracy = IsFrameAccurate
                ? FrameAccuracy.Accurate
                : IsFrameInexact
                    ? FrameAccuracy.Inexact
                    : FrameAccuracy.Neutral;
            PreviewBeforeFramesPerSecond = displayFrameRate;
            NotifyBeforeFrameAccuracy();
        }

        var candidateAccuracy = comparison?.CandidateFrames?.Accuracy ?? FrameAccuracy.Neutral;
        if (previewCandidateFrameAccuracy != candidateAccuracy)
        {
            previewCandidateFrameAccuracy = candidateAccuracy;
            NotifyCandidateFrameAccuracy();
        }

        PreviewComparison = comparison;
    }

    internal void SetFramePresentationInput(FramePresentationInput input, FramePresentationPolicy policy)
    {
        framePresentationInput = input;
        framePresentationPolicy = policy;
        RefreshFramePresentation();
    }

    internal void SetPreviewFramePresentationInputs(
        FramePresentationInput? before,
        FramePresentationInput? candidate,
        FramePresentationPolicy policy)
    {
        previewBeforeFramePresentationInput = before;
        previewCandidateFramePresentationInput = candidate;
        framePresentationPolicy = policy;
        RefreshFramePresentation();
    }

    internal void RefreshFramePresentation(FramePresentationPolicy? policy = null)
    {
        if (policy is not null)
        {
            framePresentationPolicy = policy;
        }

        FramePresentation = framePresentationInput is { } input
            ? FrameValueFormatter.Format(input, framePresentationPolicy)
            : null;
        OnPropertyChanged(nameof(FramePresentation));
        PreviewBeforeFramePresentation = previewBeforeFramePresentationInput is { } before
            ? FrameValueFormatter.Format(before, framePresentationPolicy)
            : null;
        OnPropertyChanged(nameof(PreviewBeforeFramePresentation));
        PreviewCandidateFramePresentation = previewCandidateFramePresentationInput is { } candidate
            ? FrameValueFormatter.Format(candidate, framePresentationPolicy)
            : null;
        OnPropertyChanged(nameof(PreviewCandidateFramePresentation));
    }

    private void NotifyBeforeFrameAccuracy()
    {
        OnPropertyChanged(nameof(IsPreviewBeforeFrameAccurate));
        OnPropertyChanged(nameof(IsPreviewBeforeFrameInexact));
        OnPropertyChanged(nameof(IsPreviewBeforeFrameNeutral));
    }

    private void NotifyCandidateFrameAccuracy()
    {
        OnPropertyChanged(nameof(IsPreviewCandidateFrameAccurate));
        OnPropertyChanged(nameof(IsPreviewCandidateFrameInexact));
        OnPropertyChanged(nameof(IsPreviewCandidateFrameNeutral));
    }

    public bool IsNarrowPreviewLayout
    {
        get => isNarrowPreviewLayout;
        internal set
        {
            if (isNarrowPreviewLayout == value)
            {
                return;
            }

            isNarrowPreviewLayout = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPreviewingWide));
            OnPropertyChanged(nameof(IsPreviewingNarrow));
            NotifyCellTemplateStates();
        }
    }

    public bool IsPreviewingWide => PreviewComparison is not null && !IsNarrowPreviewLayout;

    public bool IsPreviewingNarrow => PreviewComparison is not null && IsNarrowPreviewLayout;

    public bool ShowNormalTime => !HasPreviewTimeChange;

    public bool ShowWideTimePreview => HasPreviewTimeChange && !IsNarrowPreviewLayout;

    public bool ShowNarrowTimePreview => HasPreviewTimeChange && IsNarrowPreviewLayout;

    public bool ShowNormalName => !HasPreviewNameChange;

    public bool ShowWideNamePreview => HasPreviewNameChange && !IsNarrowPreviewLayout;

    public bool ShowNarrowNamePreview => HasPreviewNameChange && IsNarrowPreviewLayout;

    public bool ShowNormalNumber => !HasPreviewNumberChange;

    public bool ShowWideNumberPreview => HasPreviewNumberChange && !IsNarrowPreviewLayout;

    public bool ShowNarrowNumberPreview => HasPreviewNumberChange && IsNarrowPreviewLayout;

    public bool ShowNormalFrames => !HasPreviewFramesChange;

    public bool ShowWideFramesPreview => HasPreviewFramesChange && !IsNarrowPreviewLayout;

    public bool ShowNarrowFramesPreview => HasPreviewFramesChange && IsNarrowPreviewLayout;

    public string FormatPreviewTime(TimeSpan value) => formatter.Format(value);

    private void SetProperty(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyCellTemplateStates()
    {
        OnPropertyChanged(nameof(ShowNormalTime));
        OnPropertyChanged(nameof(ShowWideTimePreview));
        OnPropertyChanged(nameof(ShowNarrowTimePreview));
        OnPropertyChanged(nameof(ShowNormalName));
        OnPropertyChanged(nameof(ShowWideNamePreview));
        OnPropertyChanged(nameof(ShowNarrowNamePreview));
        OnPropertyChanged(nameof(ShowNormalNumber));
        OnPropertyChanged(nameof(ShowWideNumberPreview));
        OnPropertyChanged(nameof(ShowNarrowNumberPreview));
        OnPropertyChanged(nameof(ShowNormalFrames));
        OnPropertyChanged(nameof(ShowWideFramesPreview));
        OnPropertyChanged(nameof(ShowNarrowFramesPreview));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
