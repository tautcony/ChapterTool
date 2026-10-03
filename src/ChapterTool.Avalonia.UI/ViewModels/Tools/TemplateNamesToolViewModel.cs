using ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;
using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.UI.ViewModels.Tools;

public sealed class TemplateNamesToolViewModel : ObservableViewModel
{
    private readonly INamingPreferencePort namingPreferences;
    private readonly IChapterContentOperationPort? contentOperations;
    private ChapterContentPreview? pendingPreview;

    public TemplateNamesToolViewModel(INamingPreferencePort namingPreferences, IChapterContentOperationPort? contentOperations = null)
    {
        this.namingPreferences = namingPreferences;
        this.contentOperations = contentOperations;
        UseTemplateNames = namingPreferences.UseTemplateNames;
        ApplyCommand = new UiCommand(async (parameter, _) =>
        {
            if (parameter is not TemplateNamesToolViewModel viewModel)
            {
                return;
            }

            this.namingPreferences.AutoGenerateNames = false;
            this.namingPreferences.UseTemplateNames = viewModel.UseTemplateNames;
            if (this.contentOperations is null)
            {
                return;
            }

            viewModel.pendingPreview = this.contentOperations.PrepareTemplateNames(autoGenerateNames: false, useTemplateNames: viewModel.UseTemplateNames);
            viewModel.OnPropertyChanged(nameof(IsPreviewPending));
            viewModel.OnPropertyChanged(nameof(CanPreparePreview));
            viewModel.OnPropertyChanged(nameof(CanApplyPreview));
            viewModel.StatusText = viewModel.pendingPreview.IsValid
                ? viewModel.pendingPreview.Differences.IsEmpty
                    ? "No chapter names will change."
                    : string.Join("; ", viewModel.pendingPreview.Differences.Take(4).Select(static difference =>
                        $"{difference.Before} → {difference.After}"))
                : string.Join("; ", viewModel.pendingPreview.Errors);
            viewModel.ConfirmApplyCommand!.RaiseCanExecuteChanged();
            viewModel.CancelPreviewCommand!.RaiseCanExecuteChanged();
        });
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
                ? "Chapter names updated."
                : string.Join("; ", outcome.Errors);
            ConfirmApplyCommand!.RaiseCanExecuteChanged();
            CancelPreviewCommand!.RaiseCanExecuteChanged();
        }, _ => CanApplyPreview);
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

    public bool UseTemplateNames
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public bool IsPreviewPending => pendingPreview is not null;

    public bool CanPreparePreview => pendingPreview is null;

    public bool CanApplyPreview => pendingPreview?.IsValid == true;

    public UiCommand ApplyCommand { get; }

    public UiCommand ConfirmApplyCommand { get; }

    public UiCommand CancelPreviewCommand { get; }
}
