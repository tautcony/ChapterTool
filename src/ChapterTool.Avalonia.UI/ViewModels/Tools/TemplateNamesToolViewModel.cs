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
        ApplyCommand = new UiCommand(ApplyTemplateNamesAsync);
        ConfirmApplyCommand = new UiCommand(ConfirmApplyAsync, _ => CanApplyPreview);
        CancelPreviewCommand = new UiCommand(CancelPreviewAsync, _ => IsPreviewPending);
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

    private ValueTask ApplyTemplateNamesAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (parameter is not TemplateNamesToolViewModel viewModel)
        {
            return ValueTask.CompletedTask;
        }

        namingPreferences.AutoGenerateNames = false;
        namingPreferences.UseTemplateNames = viewModel.UseTemplateNames;
        if (contentOperations is null)
        {
            return ValueTask.CompletedTask;
        }

        viewModel.pendingPreview = contentOperations.PrepareTemplateNames(
            autoGenerateNames: false,
            useTemplateNames: viewModel.UseTemplateNames);
        viewModel.OnPropertyChanged(nameof(IsPreviewPending));
        viewModel.OnPropertyChanged(nameof(CanPreparePreview));
        viewModel.OnPropertyChanged(nameof(CanApplyPreview));
        viewModel.StatusText = DescribePreview(viewModel.pendingPreview);
        viewModel.ConfirmApplyCommand.RaiseCanExecuteChanged();
        viewModel.CancelPreviewCommand.RaiseCanExecuteChanged();
        return ValueTask.CompletedTask;
    }

    private async ValueTask ConfirmApplyAsync(object? parameter, CancellationToken token)
    {
        if (contentOperations is null || pendingPreview is not { IsValid: true } preview)
        {
            return;
        }

        pendingPreview = null;
        RefreshPreviewCommands();
        var outcome = await contentOperations.ApplyAsync(preview, token);
        StatusText = outcome.Kind is TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange
            ? "Chapter names updated."
            : string.Join("; ", outcome.Errors);
        RaisePreviewCommands();
    }

    private ValueTask CancelPreviewAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (pendingPreview is { } preview)
        {
            contentOperations?.Cancel(preview);
            pendingPreview = null;
            StatusText = string.Empty;
            RefreshPreviewCommands();
        }

        RaisePreviewCommands();
        return ValueTask.CompletedTask;
    }

    private void RefreshPreviewCommands()
    {
        OnPropertyChanged(nameof(IsPreviewPending));
        OnPropertyChanged(nameof(CanPreparePreview));
        OnPropertyChanged(nameof(CanApplyPreview));
    }

    private void RaisePreviewCommands()
    {
        ConfirmApplyCommand.RaiseCanExecuteChanged();
        CancelPreviewCommand.RaiseCanExecuteChanged();
    }

    private static string DescribePreview(ChapterContentPreview preview) => !preview.IsValid
        ? string.Join("; ", preview.Errors)
        : preview.Differences.IsEmpty
            ? "No chapter names will change."
            : string.Join("; ", preview.Differences.Take(4).Select(static difference =>
                $"{difference.Before} → {difference.After}"));
}
