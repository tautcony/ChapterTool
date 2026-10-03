using ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;
using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.UI.ViewModels.Tools;

public sealed class ForwardShiftToolViewModel : ObservableViewModel
{
    private readonly IChapterEditPort chapterEdit;
    private readonly IChapterContentOperationPort? contentOperations;
    private ChapterContentPreview? pendingPreview;

    public ForwardShiftToolViewModel(
        IChapterEditPort chapterEdit,
        Func<Exception, ValueTask>? errorHandler = null,
        IChapterContentOperationPort? contentOperations = null)
    {
        this.chapterEdit = chapterEdit;
        this.contentOperations = contentOperations;
        ApplyCommand = new UiCommand(async (parameter, token) =>
        {
            if (parameter is ForwardShiftToolViewModel viewModel)
            {
                if (this.contentOperations is null)
                {
                    await this.chapterEdit.ShiftFramesForwardAsync((int)viewModel.Frames, token);
                    return;
                }

                viewModel.pendingPreview = this.contentOperations.PrepareFrameShift((int)viewModel.Frames);
                viewModel.OnPropertyChanged(nameof(IsPreviewPending));
                viewModel.OnPropertyChanged(nameof(CanPreparePreview));
                viewModel.OnPropertyChanged(nameof(CanApplyPreview));
                viewModel.StatusText = PreviewSummary(viewModel.pendingPreview);
                viewModel.ConfirmApplyCommand!.RaiseCanExecuteChanged();
                viewModel.CancelPreviewCommand!.RaiseCanExecuteChanged();
            }
        })
        {
            ErrorHandler = errorHandler
        };
        ConfirmApplyCommand = new UiCommand(async (_, token) =>
        {
            if (this.contentOperations is null || this.pendingPreview is not { IsValid: true } preview)
            {
                return;
            }

            this.pendingPreview = null;
            OnPropertyChanged(nameof(IsPreviewPending));
            OnPropertyChanged(nameof(CanPreparePreview));
            OnPropertyChanged(nameof(CanApplyPreview));
            var outcome = await this.contentOperations.ApplyAsync(preview, token);
            StatusText = outcome.Kind is TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange
                ? "Chapter frame shift applied."
                : string.Join("; ", outcome.Errors);
            ConfirmApplyCommand!.RaiseCanExecuteChanged();
            CancelPreviewCommand!.RaiseCanExecuteChanged();
        })
        {
            ErrorHandler = errorHandler
        };
        CancelPreviewCommand = new UiCommand((_, _) =>
        {
            if (this.pendingPreview is { } preview)
            {
                this.contentOperations?.Cancel(preview);
                this.pendingPreview = null;
                StatusText = string.Empty;
                OnPropertyChanged(nameof(IsPreviewPending));
                OnPropertyChanged(nameof(CanPreparePreview));
                OnPropertyChanged(nameof(CanApplyPreview));
            }

            ConfirmApplyCommand!.RaiseCanExecuteChanged();
            CancelPreviewCommand!.RaiseCanExecuteChanged();
            return ValueTask.CompletedTask;
        }, _ => IsPreviewPending);
    }

    public decimal Frames
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsPreviewPending => pendingPreview is not null;

    public bool CanPreparePreview => pendingPreview is null;

    public bool CanApplyPreview => pendingPreview?.IsValid == true;

    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public UiCommand ApplyCommand { get; }

    public UiCommand ConfirmApplyCommand { get; }

    public UiCommand CancelPreviewCommand { get; }

    private static string PreviewSummary(ChapterContentPreview preview) => !preview.IsValid
        ? string.Join("; ", preview.Errors)
        : preview.Differences.IsEmpty
            ? "No chapter values will change."
            : string.Join("; ", preview.Differences.Take(4).Select(static difference =>
                $"{difference.Field}: '{difference.Before}' → '{difference.After}'"));
}
